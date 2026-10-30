using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PropostaCredito.Infrastructure.Messaging;
using PropostaCredito.Infrastructure.Modules.Credito;
using PropostaCredito.Infrastructure.Modules.Propostas;
using PropostaCredito.Infrastructure.Modules.Shared;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.IntegrationTests;

internal sealed class RuntimeCreditFactory : IDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext() => new(new DbContextOptionsBuilder<PlatformDbContext>().UseOracle(
        OracleSettings.RequireValid(Environment.GetEnvironmentVariable("ConnectionStrings__Oracle")),
        options => options.MigrationsHistoryTable("__EFMigrationsHistory", PlatformDbContext.Owner).CommandTimeout(10)).Options);
}

internal static class CreditTestTools
{
    public static readonly RuntimeCreditFactory Factory = new();
    public static CreditProcessor Processor(ICreditCommitHook? hook = null) => new(Factory, NullLogger<CreditProcessor>.Instance, hook);
    public static IOptions<RabbitOptions> Rabbit() => Options.Create(new RabbitOptions
    {
        Host = Environment.GetEnvironmentVariable("Rabbit__Host") ?? "localhost",
        Port = int.Parse(Environment.GetEnvironmentVariable("Rabbit__Port") ?? "56720"),
        Password = Environment.GetEnvironmentVariable("Rabbit__Password") ?? throw new InvalidOperationException("Configure Rabbit para integrar.")
    });
    public static OutboxDispatcher Dispatcher(IMessagePublisher publisher, IAfterPublishHook? hook = null) =>
        new(Factory, publisher, hook ?? new NoOpAfterPublishHook(), NullLogger<OutboxDispatcher>.Instance);
    public static async Task<string> SubmitAsync(IntakeFactory api, string cnpj, decimal amount = 1000, int months = 12)
    {
        using var client = api.Client(1);
        var response = await IntakeFactory.PostAsync(client, "/propostas", new SubmitProposal(cnpj, 10000, amount, months, "BRL"));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProposalAccepted>())!.PropostaId;
    }
    public static async Task<OutboxMessage> OutboxAsync(string proposalId, string type)
    {
        await using var database = Factory.CreateDbContext();
        return await database.Outbox.AsNoTracking().Where(row => row.ProposalId == proposalId && row.Type == type)
            .OrderByDescending(row => row.OccurredAt).FirstAsync();
    }
    public static async Task<CreditMessage> ResultAsync(string proposalId)
    {
        var processor = Processor();
        await processor.HandleAsync(CreditMessage.FromOutbox(await OutboxAsync(proposalId, "SolicitarAnalise")), CancellationToken.None);
        var request = CreditMessage.FromOutbox(await OutboxAsync(proposalId, "SolicitarBureau"));
        await processor.HandleAsync(request, CancellationToken.None);
        Assert.Equal(1, await new SimulatedBureauJob(Factory).RunAsync(CancellationToken.None, request.QueryId));
        return CreditMessage.FromOutbox(await OutboxAsync(proposalId, "ResultadoBureauRecebido"));
    }
}
