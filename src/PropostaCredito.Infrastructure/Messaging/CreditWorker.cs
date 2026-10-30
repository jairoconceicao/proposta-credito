using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropostaCredito.Infrastructure.Modules.Credito;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PropostaCredito.Infrastructure.Messaging;

public sealed class CreditWorker(OutboxDispatcher dispatcher, SimulatedBureauJob simulator,
    CreditProcessor processor, IOptions<RabbitOptions> options, ILogger<CreditWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumers = CreditMessage.Routes.Keys.Where(type => type != "GerarContrato")
            .Select(type => ConsumeAsync(type, stoppingToken));
        try { await Task.WhenAll(consumers.Append(DispatchAsync(stoppingToken)).Append(SimulateAsync(stoppingToken))); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                for (var batch = 0; batch < 20 && await dispatcher.DispatchOneAsync(ct); batch++) { }
            }
            catch (Exception exception) when (!ct.IsCancellationRequested) { logger.LogWarning("Dispatcher indisponivel ({ErrorType}); pendencias Oracle preservadas.", exception.GetBaseException().GetType().Name); }
            await Task.Delay(500, ct);
        }
    }
    private async Task SimulateAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await simulator.RunAsync(ct); }
            catch (Exception exception) when (!ct.IsCancellationRequested) { logger.LogWarning("Simulador indisponivel ({ErrorType}); intencoes preservadas.", exception.GetBaseException().GetType().Name); }
            await Task.Delay(500, ct);
        }
    }
    private async Task ConsumeAsync(string type, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var connection = await options.Value.Factory().CreateConnectionAsync(ct);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
                await RabbitMqPublisher.CheckTopologyAsync(channel, ct);
                await channel.BasicQosAsync(0, 4, false, ct);
                var sessionEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                channel.ChannelShutdownAsync += (_, _) => { sessionEnded.TrySetResult(); return Task.CompletedTask; };
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        var message = CreditMessage.Parse(delivery.Body.Span, type);
                        await processor.HandleAsync(message, ct);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
                    }
                    catch (MessageContractException)
                    {
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, false, ct);
                        logger.LogWarning("Mensagem invalida na etapa {Stage}; enviada para quarentena.", type);
                    }
                    catch (Exception exception) when (!ct.IsCancellationRequested)
                    {
                        // No ACK: closing the session returns the delivery. Pause before reconnecting.
                        logger.LogWarning("Etapa {Stage} sem ACK ({ErrorType}); sessao sera retomada.", type, exception.GetBaseException().GetType().Name);
                        sessionEnded.TrySetResult();
                    }
                };
                await channel.BasicConsumeAsync(CreditMessage.Routes[type].Queue, false, consumer, ct);
                await sessionEnded.Task.WaitAsync(ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested) { logger.LogWarning("Consumo indisponivel na etapa {Stage}; reconexao com pausa.", type); }
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}
