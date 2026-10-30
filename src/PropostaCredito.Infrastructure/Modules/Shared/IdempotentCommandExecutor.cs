using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.Infrastructure.Modules.Shared;

public interface IIdempotencyReservationProbe
{
    Task BeforeReservationAsync(string operation, CancellationToken cancellationToken);
}

public sealed class NoOpReservationProbe : IIdempotencyReservationProbe
{
    public Task BeforeReservationAsync(string operation, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class IdempotentCommandExecutor(IDbContextFactory<PlatformDbContext> factory, IIdempotencyReservationProbe probe)
{
    public async Task<T> ExecuteAsync<T>(string scope, string operation, string key, string bodyHash,
        Func<PlatformDbContext, Task<T>> apply, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var existing = await FindAsync(database, scope, operation, key, cancellationToken);
        if (existing is not null)
            return Read<T>(existing, bodyHash);

        var reservation = new IdempotencyRecord
        {
            Scope = scope, Operation = operation, Key = key, BodyHash = bodyHash, CreatedAt = DateTime.UtcNow
        };
        database.Idempotency.Add(reservation);
        try
        {
            // Reserve the unique key before effects. A concurrent insert waits for this transaction.
            await probe.BeforeReservationAsync(operation, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            await using var reader = await factory.CreateDbContextAsync(cancellationToken);
            var winner = await FindAsync(reader, scope, operation, key, cancellationToken)
                ?? throw new InvalidOperationException("Reserva concorrente nao encontrada.");
            return Read<T>(winner, bodyHash);
        }

        var response = await apply(database);
        reservation.ResponseJson = JsonSerializer.Serialize(response);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private static Task<IdempotencyRecord?> FindAsync(PlatformDbContext database, string scope,
        string operation, string key, CancellationToken cancellationToken) =>
        database.Idempotency.AsNoTracking().SingleOrDefaultAsync(row =>
            row.Scope == scope && row.Operation == operation && row.Key == key, cancellationToken);

    private static T Read<T>(IdempotencyRecord record, string hash)
    {
        if (record.BodyHash != hash)
            throw new IntakeException(409, "idempotency-conflict", "Chave ja utilizada com outro corpo.");
        return JsonSerializer.Deserialize<T>(record.ResponseJson)
            ?? throw new InvalidOperationException("Resposta idempotente ausente.");
    }

    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is OracleException { Number: 1 };
}
