using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.IntegrationTests;

// Real Oracle only. Missing configuration or unavailable database fails the suite, never skips it.
public sealed class OracleFoundationTests
{
    private static PlatformDbContext CreateOwnerContext() => new DesignTimeDbContextFactory().CreateDbContext([]);

    [Fact]
    public async Task MigrationsCanBeAppliedTwiceWithoutChangingHistory()
    {
        await using var database = CreateOwnerContext();
        var before = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.NotEmpty(before);
        await database.Database.MigrateAsync();
        await database.Database.MigrateAsync();
        Assert.Equal(before, await database.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task CommitPersistsAndRollbackLeavesNoPartialEffect()
    {
        var committedKey = $"commit-{Guid.NewGuid():N}";
        var rolledBackKey = $"rollback-{Guid.NewGuid():N}";
        await using var database = CreateOwnerContext();
        try
        {
            await using (var transaction = await database.Database.BeginTransactionAsync())
            {
                database.Metadata.Add(new PlatformMetadata { Key = committedKey, Value = "fictional" });
                await database.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            await using (var transaction = await database.Database.BeginTransactionAsync())
            {
                database.Metadata.Add(new PlatformMetadata { Key = rolledBackKey, Value = "fictional" });
                await database.SaveChangesAsync();
                await transaction.RollbackAsync();
            }

            await using var freshConnection = CreateOwnerContext();
            Assert.True(await freshConnection.Metadata.AnyAsync(row => row.Key == committedKey));
            Assert.False(await freshConnection.Metadata.AnyAsync(row => row.Key == rolledBackKey));
        }
        finally
        {
            await database.Metadata.Where(row => row.Key == committedKey || row.Key == rolledBackKey).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task RuntimeUserCanReadSchemaButCannotChangeIt()
    {
        var runtime = OracleSettings.RequireValid(Environment.GetEnvironmentVariable("ConnectionStrings__Oracle"));
        await using var connection = new OracleConnection(runtime);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PC_OWNER.PLATFORM_METADATA WHERE \"KEY\" = 'schema-version'";
        Assert.Equal(1m, Convert.ToDecimal(await command.ExecuteScalarAsync()));
        command.CommandText = "UPDATE PC_OWNER.PLATFORM_METADATA SET \"VALUE\" = \"VALUE\" WHERE \"KEY\" = 'schema-version'";
        var error = await Assert.ThrowsAsync<OracleException>(() => command.ExecuteNonQueryAsync());
        Assert.Contains(error.Number, new[] { 1031, 41900 });
        command.CommandText = "SELECT COUNT(*) FROM SESSION_PRIVS WHERE PRIVILEGE IN ('CREATE TABLE', 'CREATE USER', 'DBA')";
        Assert.Equal(0m, Convert.ToDecimal(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task HealthySchemaMakesApiReady()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task IncompatibleSchemaMakesReadinessFailWithoutAffectingLiveness()
    {
        await using var database = CreateOwnerContext();
        try
        {
            await database.Metadata.Where(row => row.Key == "schema-version")
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Value, "incompatible"));
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        }
        finally
        {
            await database.Metadata.Where(row => row.Key == "schema-version")
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Value, "1"));
        }
    }

    [Fact]
    public async Task UnavailableOracleMakesReadinessFailButKeepsLivenessHealthy()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Oracle"] = "User Id=PC_APP;Password=do-not-expose;Data Source=localhost:1/FREEPDB1;Connection Timeout=1;"
                })));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.Equal("Unhealthy", await readiness.Content.ReadAsStringAsync());
    }
}
