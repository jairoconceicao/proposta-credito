using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.Infrastructure.Health;

public sealed class OracleReadinessCheck(IDbContextFactory<PlatformDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var expected = database.Database.GetMigrations().ToArray();
            var applied = (await database.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            if (expected.Length == 0 || !expected.SequenceEqual(applied))
            {
                return HealthCheckResult.Unhealthy("Schema Oracle incompativel.");
            }

            var version = await database.Metadata.AsNoTracking()
                .Where(row => row.Key == "schema-version")
                .Select(row => row.Value)
                .SingleOrDefaultAsync(cancellationToken);
            return version == "1"
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Schema Oracle incompativel.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Do not attach provider exceptions: health responses/logs must not expose credentials.
            return HealthCheckResult.Unhealthy("Oracle indisponivel ou schema incompativel.");
        }
    }
}
