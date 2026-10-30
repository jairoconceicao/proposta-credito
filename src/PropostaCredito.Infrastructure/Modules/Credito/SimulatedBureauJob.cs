using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.Infrastructure.Modules.Credito;

public sealed class SimulatedBureauJob(IDbContextFactory<PlatformDbContext> factory)
{
    public async Task<int> RunAsync(CancellationToken ct, string? onlyQueryId = null)
    {
        await using var reader = await factory.CreateDbContextAsync(ct);
        var ids = await reader.BureauQueries.AsNoTracking().Where(row => row.State == "PRONTA_ENVIO" && (onlyQueryId == null || row.Id == onlyQueryId))
            .OrderBy(row => row.CreatedAt).Select(row => row.Id).Take(20).ToArrayAsync(ct);
        var completed = 0;
        foreach (var id in ids)
        {
            await using var database = await factory.CreateDbContextAsync(ct);
            await using var transaction = await database.Database.BeginTransactionAsync(ct);
            var claimed = await database.BureauQueries.Where(row => row.Id == id && row.State == "PRONTA_ENVIO")
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.State, "PROCESSANDO"), ct);
            if (claimed == 0) continue;
            var query = await database.BureauQueries.SingleAsync(row => row.Id == id, ct);
            var proposal = await database.Proposals.AsNoTracking().SingleAsync(row => row.Id == query.ProposalId, ct);
            query.ExistingDebt = 1000; query.TotalLimit = CreditRules.Limit;
            query.ObtainedAt = DateTime.UtcNow; query.ValidUntil = query.ObtainedAt.Value.AddMinutes(5); query.State = "CONCLUIDA";
            database.Outbox.Add(CreditProcessor.Command(proposal, "ResultadoBureauRecebido", query.CorrelationId, query.CausationId, query.Id));
            await database.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            completed++;
        }
        return completed;
    }
}
