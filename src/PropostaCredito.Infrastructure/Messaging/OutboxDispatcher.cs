using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropostaCredito.Infrastructure.Modules.Shared;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.Infrastructure.Messaging;

public interface IAfterPublishHook
{
    Task ConfirmedAsync(OutboxMessage row, CancellationToken cancellationToken);
}
public sealed class NoOpAfterPublishHook : IAfterPublishHook
{
    public Task ConfirmedAsync(OutboxMessage row, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class OutboxDispatcher(IDbContextFactory<PlatformDbContext> factory, IMessagePublisher publisher,
    IAfterPublishHook hook, ILogger<OutboxDispatcher> logger)
{
    public async Task<OutboxMessage?> ClaimAsync(CancellationToken ct, string? onlyId = null)
    {
        var now = DateTime.UtcNow;
        await using var database = await factory.CreateDbContextAsync(ct);
        var ids = await database.Outbox.AsNoTracking().Where(row => row.PublishedAt == null && row.AvailableAt <= now
            && (row.LeaseUntil == null || row.LeaseUntil < now) && (onlyId == null || row.Id == onlyId))
            .OrderBy(row => row.AvailableAt).Select(row => row.Id).Take(20).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            var token = Guid.NewGuid().ToString("N");
            var claimed = await database.Outbox.Where(row => row.Id == id && row.PublishedAt == null && row.AvailableAt <= now
                && (row.LeaseUntil == null || row.LeaseUntil < now)).ExecuteUpdateAsync(update => update
                    .SetProperty(row => row.LeaseToken, token).SetProperty(row => row.LeaseUntil, now.AddSeconds(30)), ct);
            if (claimed == 1) return await database.Outbox.AsNoTracking().SingleAsync(row => row.Id == id, ct);
        }
        return null;
    }

    public async Task<bool> MarkPublishedAsync(OutboxMessage row, CancellationToken ct)
    {
        await using var database = await factory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        return await database.Outbox.Where(item => item.Id == row.Id && item.PublishedAt == null
            && item.LeaseToken == row.LeaseToken && item.LeaseUntil > now).ExecuteUpdateAsync(update => update
                .SetProperty(item => item.PublishedAt, now).SetProperty(item => item.LeaseUntil, (DateTime?)null)
                .SetProperty(item => item.LeaseToken, (string?)null).SetProperty(item => item.Error, (string?)null), ct) == 1;
    }

    public async Task<bool> DispatchOneAsync(CancellationToken ct, string? onlyId = null)
    {
        var row = await ClaimAsync(ct, onlyId);
        if (row is null) return false;
        try
        {
            await publisher.PublishAsync(CreditMessage.FromOutbox(row), row.RoutingKey, ct);
            await hook.ConfirmedAsync(row, ct);
            var marked = await MarkPublishedAsync(row, ct);
            logger.LogInformation("Outbox confirmada {OutboxId} {MessageId} {CorrelationId}; token vigente: {Marked}", row.Id, row.MessageId, row.CorrelationId, marked);
            return marked;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            await using var database = await factory.CreateDbContextAsync(ct);
            await database.Outbox.Where(item => item.Id == row.Id && item.LeaseToken == row.LeaseToken && item.LeaseUntil > DateTime.UtcNow)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.AvailableAt, DateTime.UtcNow.AddSeconds(2))
                    .SetProperty(item => item.LeaseUntil, (DateTime?)null).SetProperty(item => item.LeaseToken, (string?)null)
                    .SetProperty(item => item.Error, "PUBLICACAO_PENDENTE"), ct);
            logger.LogWarning("Publicacao pendente {OutboxId} {MessageId} {CorrelationId} ({ErrorType})", row.Id, row.MessageId, row.CorrelationId, exception.GetBaseException().GetType().Name);
            return false;
        }
    }
}
