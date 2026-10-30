using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using PropostaCredito.Infrastructure.Modules.Credito;

namespace PropostaCredito.Infrastructure.Messaging;

public static class MessagingExtensions
{
    public static void AddCreditWorker(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<RabbitOptions>().Bind(builder.Configuration.GetSection("Rabbit"))
            .Validate(options => options.Port is > 0 and <= 65535 && options.Password.Length >= 16
                && !string.IsNullOrWhiteSpace(options.Host) && !string.IsNullOrWhiteSpace(options.User)
                && !string.IsNullOrWhiteSpace(options.VirtualHost), "Configure conexao RabbitMQ valida.")
            .Validate(options => builder.Environment.IsDevelopment() || options.UseTls, "TLS RabbitMQ obrigatorio fora de Development.")
            .ValidateOnStart();
        builder.Services.AddSingleton<RabbitMqPublisher>();
        builder.Services.AddSingleton<IMessagePublisher>(services => services.GetRequiredService<RabbitMqPublisher>());
        builder.Services.AddSingleton<IAfterPublishHook, NoOpAfterPublishHook>();
        builder.Services.AddSingleton<OutboxDispatcher>();
        builder.Services.AddSingleton<CreditProcessor>();
        builder.Services.AddSingleton<SimulatedBureauJob>();
        builder.Services.AddHostedService<CreditWorker>();
        builder.Services.AddHealthChecks().AddCheck<RabbitReadinessCheck>("rabbit-topology", tags: ["ready"], timeout: TimeSpan.FromSeconds(8));
    }
}

public sealed class RabbitReadinessCheck(IOptions<RabbitOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await options.Value.Factory().CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await RabbitMqPublisher.CheckTopologyAsync(channel, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { return HealthCheckResult.Unhealthy("RabbitMQ/topologia indisponivel."); }
    }
}
