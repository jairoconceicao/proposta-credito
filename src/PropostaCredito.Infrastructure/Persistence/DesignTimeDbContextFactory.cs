using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PropostaCredito.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString = OracleSettings.RequireValid(
            Environment.GetEnvironmentVariable("ConnectionStrings__OracleMigrations"));
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseOracle(connectionString, oracle => oracle
                .MigrationsHistoryTable("__EFMigrationsHistory", PlatformDbContext.Owner)
                .CommandTimeout(10))
            .Options;
        return new PlatformDbContext(options);
    }
}
