using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Messaging;
using PropostaCredito.Infrastructure.Modules.Credito;
using PropostaCredito.Infrastructure.Modules.Propostas;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.IntegrationTests;

public sealed class CreditDecisionTests
{
    [Theory]
    [InlineData(1000, "APROVADA")]
    [InlineData(40000, "REJEITADA")]
    [InlineData(33000, "EM_ANALISE_MANUAL")]
    public async Task PersistsExplainableOutcomeAndOnlyApprovalSchedulesContract(int amount, string expected)
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var id = await CreditTestTools.SubmitAsync(api, company.Cnpj, amount);
        var result = await CreditTestTools.ResultAsync(id);
        await CreditTestTools.Processor().HandleAsync(result, CancellationToken.None);
        using var client = api.Client(1);
        var view = (await client.GetFromJsonAsync<ProposalResponse>("/propostas/" + id))!;
        Assert.Equal(expected, view.Estado); Assert.Equal(CreditRules.Version, view.VersaoRegras);
        Assert.Equal(3, view.Motivos.Length); Assert.NotNull(view.AvaliadaEm);
        Assert.Equal(expected != "EM_ANALISE_MANUAL", view.DecididaEm is not null);
        await using var database = CreditTestTools.Factory.CreateDbContext();
        Assert.Equal(expected == "EM_ANALISE_MANUAL" ? 0 : 1, await database.Decisions.CountAsync(row => row.ProposalId == id));
        Assert.Equal(expected == "APROVADA" ? 1 : 0, await database.Outbox.CountAsync(row => row.ProposalId == id && row.Type == "GerarContrato"));
        var snapshot = await database.Analyses.SingleAsync(row => row.Id == result.AnaliseId);
        Assert.Contains("MonthlyRevenue", snapshot.ProposalSnapshot);
    }

    [Fact]
    public async Task ConcurrentApprovalsCannotExceedSharedCompanyLimit()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var a = await CreditTestTools.SubmitAsync(api, company.Cnpj, 30000, 60);
        var b = await CreditTestTools.SubmitAsync(api, company.Cnpj, 30000, 60);
        var first = await CreditTestTools.ResultAsync(a); var second = await CreditTestTools.ResultAsync(b);
        await Task.WhenAll(CreditTestTools.Processor().HandleAsync(first, CancellationToken.None),
            CreditTestTools.Processor().HandleAsync(second, CancellationToken.None));
        await using var database = CreditTestTools.Factory.CreateDbContext();
        Assert.Equal(30000, (await database.CreditAccounts.SingleAsync(row => row.CompanyId == company.EmpresaId)).Reserved);
        var decisions = await database.Decisions.Where(row => row.ProposalId == a || row.ProposalId == b).ToArrayAsync();
        Assert.Single(decisions, row => row.Outcome == "APROVADA");
        Assert.Single(decisions, row => row.Outcome == "REJEITADA");
    }

    [Fact]
    public async Task DuplicateAndOldMessagesCannotChangeVersionSnapshotOrDecision()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var id = await CreditTestTools.SubmitAsync(api, company.Cnpj);
        var prepare = CreditMessage.FromOutbox(await CreditTestTools.OutboxAsync(id, "SolicitarAnalise"));
        await Task.WhenAll(CreditTestTools.Processor().HandleAsync(prepare, CancellationToken.None), CreditTestTools.Processor().HandleAsync(prepare, CancellationToken.None));
        await using (var owner = new DesignTimeDbContextFactory().CreateDbContext([]))
            await owner.Proposals.Where(row => row.Id == id).ExecuteUpdateAsync(update => update.SetProperty(row => row.Amount, 40000));
        var request = CreditMessage.FromOutbox(await CreditTestTools.OutboxAsync(id, "SolicitarBureau"));
        await CreditTestTools.Processor().HandleAsync(request, CancellationToken.None);
        await new SimulatedBureauJob(CreditTestTools.Factory).RunAsync(CancellationToken.None, request.QueryId);
        var result = CreditMessage.FromOutbox(await CreditTestTools.OutboxAsync(id, "ResultadoBureauRecebido"));
        await Task.WhenAll(CreditTestTools.Processor().HandleAsync(result, CancellationToken.None), CreditTestTools.Processor().HandleAsync(result, CancellationToken.None));
        await CreditTestTools.Processor().HandleAsync(result with { Attempt = 1, ReplayId = "replay" }, CancellationToken.None);
        var stale = result with { MessageId = Guid.NewGuid().ToString("N"), AnaliseId = Guid.NewGuid().ToString("N") };
        await CreditTestTools.Processor().HandleAsync(stale, CancellationToken.None);
        await using var database = CreditTestTools.Factory.CreateDbContext();
        var decision = await database.Decisions.SingleAsync(row => row.ProposalId == id);
        Assert.Equal("APROVADA", decision.Outcome);
        Assert.Contains("\"Amount\":1000", decision.InputsJson);
        Assert.Equal(1000, (await database.CreditAccounts.SingleAsync(row => row.CompanyId == company.EmpresaId)).Reserved);
        Assert.Equal(1, await database.BureauQueries.CountAsync(row => row.ProposalId == id));
        Assert.Equal(1, await database.Outbox.CountAsync(row => row.ProposalId == id && row.Type == "GerarContrato"));
        Assert.Equal("IGNORED_STALE", (await database.Inbox.SingleAsync(row => row.MessageId == stale.MessageId)).Result);
    }

    [Fact]
    public async Task ExpiredRiskCreatesNewQueryAndExpiredDeadlineGoesToManualReview()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var id = await CreditTestTools.SubmitAsync(api, company.Cnpj);
        var result = await CreditTestTools.ResultAsync(id);
        await using (var owner = new DesignTimeDbContextFactory().CreateDbContext([]))
            await owner.BureauQueries.Where(row => row.Id == result.QueryId).ExecuteUpdateAsync(update => update.SetProperty(row => row.ValidUntil, DateTime.UtcNow.AddMinutes(-1)));
        await CreditTestTools.Processor().HandleAsync(result, CancellationToken.None);
        var request = CreditMessage.FromOutbox(await CreditTestTools.OutboxAsync(id, "SolicitarBureau"));
        Assert.NotEqual(result.QueryId, request.QueryId);
        await CreditTestTools.Processor().HandleAsync(request, CancellationToken.None);
        await new SimulatedBureauJob(CreditTestTools.Factory).RunAsync(CancellationToken.None, request.QueryId);
        await using (var owner = new DesignTimeDbContextFactory().CreateDbContext([]))
            await owner.Analyses.Where(row => row.Id == result.AnaliseId).ExecuteUpdateAsync(update => update.SetProperty(row => row.DeadlineAt, DateTime.UtcNow.AddMinutes(-1)));
        var latest = CreditMessage.FromOutbox(await CreditTestTools.OutboxAsync(id, "ResultadoBureauRecebido"));
        await CreditTestTools.Processor().HandleAsync(latest, CancellationToken.None);
        await using var database = CreditTestTools.Factory.CreateDbContext();
        Assert.Equal("EM_ANALISE_MANUAL", (await database.Proposals.SingleAsync(row => row.Id == id)).State);
        Assert.False(await database.Decisions.AnyAsync(row => row.ProposalId == id));
        Assert.False(await database.Outbox.AnyAsync(row => row.ProposalId == id && row.Type == "GerarContrato"));
    }

    [Fact]
    public async Task FailureBeforeDecisionCommitRollsBackReservationInboxAndContract()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        var id = await CreditTestTools.SubmitAsync(api, company.Cnpj);
        var result = await CreditTestTools.ResultAsync(id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreditTestTools.Processor(new FailCreditCommit()).HandleAsync(result, CancellationToken.None));
        await using var database = CreditTestTools.Factory.CreateDbContext();
        Assert.Equal(0, (await database.CreditAccounts.SingleAsync(row => row.CompanyId == company.EmpresaId)).Reserved);
        Assert.False(await database.Inbox.AnyAsync(row => row.MessageId == result.MessageId));
        Assert.False(await database.Decisions.AnyAsync(row => row.ProposalId == id));
        Assert.False(await database.Outbox.AnyAsync(row => row.ProposalId == id && row.Type == "GerarContrato"));
        Assert.Equal("AGUARDANDO_BUREAU", (await database.Proposals.SingleAsync(row => row.Id == id)).State);
        await CreditTestTools.Processor().HandleAsync(result, CancellationToken.None);
        Assert.Equal(1, await database.Decisions.CountAsync(row => row.ProposalId == id));
    }

    private sealed class FailCreditCommit : ICreditCommitHook
    {
        public Task BeforeCommitAsync(CreditMessage message, CancellationToken cancellationToken) => throw new InvalidOperationException("credit-fault");
    }
}
