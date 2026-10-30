using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Messaging;
using PropostaCredito.Infrastructure.Modules.Credito;
using PropostaCredito.Infrastructure.Modules.Shared;
using PropostaCredito.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace PropostaCredito.IntegrationTests;

public sealed class OutboxBrokerTests
{
    [Fact]
    public async Task UnsupportedEnvelopeIsQuarantinedByConfiguredDeadLetterPolicy()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var proposal = await CreditTestTools.SubmitAsync(api, company.Cnpj);
        var row = await CreditTestTools.OutboxAsync(proposal, "SolicitarAnalise");
        await using var publisher = new RabbitMqPublisher(CreditTestTools.Rabbit());
        await publisher.PublishAsync(CreditMessage.FromOutbox(row) with { SchemaVersion = 2 }, row.RoutingKey, CancellationToken.None);
        await using var connection = await CreditTestTools.Rabbit().Value.Factory().CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var queue = CreditMessage.Routes[row.Type].Queue;
        var delivery = await ReceiveAsync(channel, queue, row.MessageId);
        Assert.Throws<MessageContractException>(() => CreditMessage.Parse(delivery.Body.Span, row.Type));
        await channel.BasicNackAsync(delivery.DeliveryTag, false, false);
        var quarantined = await ReceiveAsync(channel, queue + ".dlq", row.MessageId);
        Assert.NotNull(quarantined.BasicProperties.Headers);
        Assert.True(quarantined.BasicProperties.Headers.ContainsKey("x-death"));
        await channel.BasicAckAsync(quarantined.DeliveryTag, false);
        await using var database = CreditTestTools.Factory.CreateDbContext();
        Assert.False(await database.Inbox.AnyAsync(item => item.MessageId == row.MessageId));
    }

    [Fact]
    public async Task ConcurrentDispatchersClaimOnceAndExpiredLeaseCannotMarkSent()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var proposal = await CreditTestTools.SubmitAsync(api, company.Cnpj);
        var row = await CreditTestTools.OutboxAsync(proposal, "SolicitarAnalise");
        var dispatcher = CreditTestTools.Dispatcher(new NoOpPublisher());
        var claims = await Task.WhenAll(dispatcher.ClaimAsync(CancellationToken.None, row.Id), dispatcher.ClaimAsync(CancellationToken.None, row.Id));
        var claimed = Assert.Single(claims, item => item is not null)!;
        await using var owner = new DesignTimeDbContextFactory().CreateDbContext([]);
        await owner.Outbox.Where(item => item.Id == row.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.LeaseUntil, DateTime.UtcNow.AddMinutes(-1)));
        Assert.False(await dispatcher.MarkPublishedAsync(claimed, CancellationToken.None));
        var next = await dispatcher.ClaimAsync(CancellationToken.None, row.Id);
        Assert.NotNull(next); Assert.NotEqual(claimed.LeaseToken, next.LeaseToken);
        Assert.False(await dispatcher.MarkPublishedAsync(claimed, CancellationToken.None));
        Assert.True(await dispatcher.MarkPublishedAsync(next, CancellationToken.None));
    }

    [Fact]
    public async Task RealPublisherRejectsUnroutableMessageAndRecoversPendingOutbox()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var proposal = await CreditTestTools.SubmitAsync(api, company.Cnpj);
        var row = await CreditTestTools.OutboxAsync(proposal, "SolicitarAnalise");
        await using var owner = new DesignTimeDbContextFactory().CreateDbContext([]);
        await owner.Outbox.Where(item => item.Id == row.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RoutingKey, "sem.rota." + row.Id));
        await using var publisher = new RabbitMqPublisher(CreditTestTools.Rabbit());
        var dispatcher = CreditTestTools.Dispatcher(publisher);
        Assert.False(await dispatcher.DispatchOneAsync(CancellationToken.None, row.Id));
        var pending = await owner.Outbox.AsNoTracking().SingleAsync(item => item.Id == row.Id);
        Assert.Null(pending.PublishedAt); Assert.Equal("PUBLICACAO_PENDENTE", pending.Error);
        await owner.Outbox.Where(item => item.Id == row.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.RoutingKey, "analise.solicitar").SetProperty(item => item.AvailableAt, DateTime.UtcNow.AddMinutes(-1)));
        Assert.True(await dispatcher.DispatchOneAsync(CancellationToken.None, row.Id));
        await using var connection = await CreditTestTools.Rabbit().Value.Factory().CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var received = await ReceiveAsync(channel, CreditMessage.Routes[row.Type].Queue, row.MessageId);
        Assert.True(received.BasicProperties.Persistent);
        await channel.BasicAckAsync(received.DeliveryTag, false);
    }

    [Fact]
    public async Task FailureAfterConfirmCanRepublishButConsumerEffectsStayIdempotent()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var proposal = await CreditTestTools.SubmitAsync(api, company.Cnpj);
        var row = await CreditTestTools.OutboxAsync(proposal, "SolicitarAnalise");
        await using var publisher = new RabbitMqPublisher(CreditTestTools.Rabbit());
        Assert.False(await CreditTestTools.Dispatcher(publisher, new FailAfterConfirm()).DispatchOneAsync(CancellationToken.None, row.Id));
        await using (var owner = new DesignTimeDbContextFactory().CreateDbContext([]))
            await owner.Outbox.Where(item => item.Id == row.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.AvailableAt, DateTime.UtcNow.AddMinutes(-1)));
        Assert.True(await CreditTestTools.Dispatcher(publisher).DispatchOneAsync(CancellationToken.None, row.Id));
        await using var connection = await CreditTestTools.Rabbit().Value.Factory().CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        for (var index = 0; index < 2; index++)
        {
            var received = await ReceiveAsync(channel, CreditMessage.Routes[row.Type].Queue, row.MessageId);
            await CreditTestTools.Processor().HandleAsync(CreditMessage.Parse(received.Body.Span, row.Type), CancellationToken.None);
            await channel.BasicAckAsync(received.DeliveryTag, false);
        }
        await using var database = CreditTestTools.Factory.CreateDbContext();
        Assert.Equal(1, await database.Analyses.CountAsync(item => item.ProposalId == proposal));
        Assert.Equal(1, await database.BureauQueries.CountAsync(item => item.ProposalId == proposal));
        Assert.Equal(1, await database.Outbox.CountAsync(item => item.ProposalId == proposal && item.Type == "SolicitarBureau"));
    }

    [Fact]
    public async Task RedeliveryAfterCommitBeforeAckPreservesOneDecisionAcrossRealQueues()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var proposal = await CreditTestTools.SubmitAsync(api, company.Cnpj);
        await using var publisher = new RabbitMqPublisher(CreditTestTools.Rabbit());
        var dispatcher = CreditTestTools.Dispatcher(publisher);
        foreach (var type in new[] { "SolicitarAnalise", "SolicitarBureau", "ResultadoBureauRecebido" })
        {
            var row = await CreditTestTools.OutboxAsync(proposal, type);
            Assert.True(await dispatcher.DispatchOneAsync(CancellationToken.None, row.Id));
            // Commit the application effect and deliberately close without ACK.
            await using (var connection = await CreditTestTools.Rabbit().Value.Factory().CreateConnectionAsync())
            {
                await using var channel = await connection.CreateChannelAsync();
                var delivery = await ReceiveAsync(channel, CreditMessage.Routes[type].Queue, row.MessageId);
                await CreditTestTools.Processor().HandleAsync(CreditMessage.Parse(delivery.Body.Span, type), CancellationToken.None);
            }
            await using (var connection = await CreditTestTools.Rabbit().Value.Factory().CreateConnectionAsync())
            {
                await using var channel = await connection.CreateChannelAsync();
                var duplicate = await ReceiveAsync(channel, CreditMessage.Routes[type].Queue, row.MessageId);
                Assert.True(duplicate.Redelivered);
                await CreditTestTools.Processor().HandleAsync(CreditMessage.Parse(duplicate.Body.Span, type), CancellationToken.None);
                await channel.BasicAckAsync(duplicate.DeliveryTag, false);
            }
            if (type == "SolicitarBureau")
            {
                var message = CreditMessage.FromOutbox(row);
                Assert.Equal(1, await new SimulatedBureauJob(CreditTestTools.Factory).RunAsync(CancellationToken.None, message.QueryId));
            }
        }
        await using var database = CreditTestTools.Factory.CreateDbContext();
        Assert.Equal(1, await database.Decisions.CountAsync(row => row.ProposalId == proposal));
        Assert.Equal(1, await database.Outbox.CountAsync(row => row.ProposalId == proposal && row.Type == "GerarContrato"));
    }

    private static async Task<BasicGetResult> ReceiveAsync(IChannel channel, string queue, string messageId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            var delivery = await channel.BasicGetAsync(queue, false, timeout.Token);
            if (delivery is not null)
            {
                Assert.Equal(messageId, delivery.BasicProperties.MessageId);
                return delivery;
            }
            await Task.Delay(50, timeout.Token);
        }
        throw new InvalidOperationException("Mensagem nao entregue no prazo.");
    }
    private sealed class NoOpPublisher : IMessagePublisher
    {
        public Task PublishAsync(CreditMessage message, string route, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class FailAfterConfirm : IAfterPublishHook
    {
        public Task ConfirmedAsync(OutboxMessage row, CancellationToken cancellationToken) => throw new InvalidOperationException("after-confirm-fault");
    }
}
