using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropostaCredito.Infrastructure.Health;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.Infrastructure;

public static class FoundationExtensions
{
    public static void AddFoundation(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<OracleOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString("Oracle") ?? "")
            .Validate(options => OracleSettings.IsValid(options.ConnectionString),
                "Configure ConnectionStrings:Oracle com Data Source, User Id e Password.")
            .ValidateOnStart();
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        // EF command/connection logs can reveal database details. Domain telemetry is added explicitly later.
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
        builder.Services.AddDbContextFactory<PlatformDbContext>((services, options) => options.UseOracle(
            services.GetRequiredService<IOptions<OracleOptions>>().Value.ConnectionString, oracle => oracle
                .MigrationsHistoryTable("__EFMigrationsHistory", PlatformDbContext.Owner)
                .CommandTimeout(5)));
        builder.Services.AddHealthChecks().AddCheck<OracleReadinessCheck>(
            "oracle-schema", tags: ["ready"], timeout: TimeSpan.FromSeconds(10));
    }

    public static void MapFoundationHealth(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        });
    }
}

public sealed class OracleOptions
{
    public string ConnectionString { get; set; } = "";
}
