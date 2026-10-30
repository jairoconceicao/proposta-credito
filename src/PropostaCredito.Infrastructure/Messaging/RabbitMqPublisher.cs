using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace PropostaCredito.Infrastructure.Messaging;

public sealed class RabbitOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 56720;
    public string VirtualHost { get; set; } = "proposta-credito";
    public string User { get; set; } = "pc_worker";
    public string Password { get; set; } = "";
    public bool UseTls { get; set; }
    public ConnectionFactory Factory() => new()
    {
        HostName = Host, Port = Port, VirtualHost = VirtualHost, UserName = User, Password = Password,
        AutomaticRecoveryEnabled = false, RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
        ContinuationTimeout = TimeSpan.FromSeconds(5), RequestedHeartbeat = TimeSpan.FromSeconds(10),
        Ssl = new SslOption { Enabled = UseTls, ServerName = Host }
    };
}

public interface IMessagePublisher
{
    Task PublishAsync(CreditMessage message, string route, CancellationToken cancellationToken);
}

public sealed class RabbitMqPublisher(IOptions<RabbitOptions> options) : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private IConnection? connection;
    private IChannel? channel;

    public static async Task CheckTopologyAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclarePassiveAsync(CreditMessage.Exchange, ct);
        await channel.ExchangeDeclarePassiveAsync(CreditMessage.DeadLetterExchange, ct);
        foreach (var item in CreditMessage.Routes.Values)
        {
            await channel.QueueDeclarePassiveAsync(item.Queue, ct);
            await channel.QueueDeclarePassiveAsync(item.Queue + ".dlq", ct);
        }
    }

    public async Task PublishAsync(CreditMessage message, string route, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            if (connection is null || !connection.IsOpen || channel is null || !channel.IsOpen)
            {
                await ResetAsync();
                connection = await options.Value.Factory().CreateConnectionAsync(timeout.Token);
                channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), timeout.Token);
                await CheckTopologyAsync(channel, timeout.Token);
            }
            await channel.BasicPublishAsync(CreditMessage.Exchange, route, mandatory: true,
                new BasicProperties
                {
                    Persistent = true, ContentType = "application/json", Type = message.Type,
                    MessageId = message.MessageId, CorrelationId = message.CorrelationId
                }, message.Serialize(), timeout.Token);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally { gate.Release(); }
    }

    private async Task ResetAsync()
    {
        if (channel is not null) { await channel.DisposeAsync(); channel = null; }
        if (connection is not null) { await connection.DisposeAsync(); connection = null; }
    }
    public async ValueTask DisposeAsync() { await ResetAsync(); gate.Dispose(); }
}
