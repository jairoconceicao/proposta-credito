using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Modules.Empresas;
using PropostaCredito.Infrastructure.Modules.Propostas;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.IntegrationTests;

public sealed class CreditIntakeTests
{
    private static SubmitProposal Request(string cnpj, decimal amount = 1000) => new(cnpj, 10000, amount, 12, "BRL");

    [Fact]
    public async Task CompanyRegistrationIsIdempotentAndAccessIsScoped()
    {
        await using var api = new IntakeFactory();
        using var admin = api.Client(0);
        var request = new RegisterCompany(IntakeFactory.FakeCnpj(), "Empresa ficticia", api.Subject(1));
        var key = Guid.NewGuid().ToString("N");
        var first = await IntakeFactory.PostAsync(admin, "/empresas", request, key);
        var repeat = await IntakeFactory.PostAsync(admin, "/empresas", request, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await repeat.Content.ReadAsStringAsync());
        Assert.Equal(first.Headers.Location, repeat.Headers.Location);
        Assert.Equal(HttpStatusCode.Conflict, (await IntakeFactory.PostAsync(admin, "/empresas", request with { RazaoSocial = "Outro nome" }, key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await IntakeFactory.PostAsync(admin, "/empresas", request with { ResponsavelId = api.Subject(2) })).StatusCode);
        using var own = api.Client(1); using var other = api.Client(2);
        Assert.Equal(HttpStatusCode.OK, (await own.GetAsync(first.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(first.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await IntakeFactory.PostAsync(own, "/empresas", request)).StatusCode);
    }

    [Fact]
    public async Task ProposalPersistsHistoryAndOutboxWithCanonicalRepeat()
    {
        await using var api = new IntakeFactory();
        var company = await api.CompanyAsync();
        using var client = api.Client(1);
        var key = Guid.NewGuid().ToString("N");
        var first = await IntakeFactory.PostAsync(client, "/propostas", Request(company.Cnpj), key);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var accepted = (await first.Content.ReadFromJsonAsync<ProposalAccepted>())!;
        var formatted = $"{company.Cnpj[..2]}.{company.Cnpj[2..5]}.{company.Cnpj[5..8]}/{company.Cnpj[8..12]}-{company.Cnpj[12..]}";
        var raw = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = new StringContent($$"""{"moeda":"brl","prazoMeses":12,"valorSolicitado":1000.00,"faturamentoMensal":10000.0,"cnpj":"{{formatted.ToLowerInvariant()}}"}""", Encoding.UTF8, "application/json")
        };
        raw.Headers.Add("Idempotency-Key", key);
        var repeat = await client.SendAsync(raw);
        Assert.Equal(HttpStatusCode.Accepted, repeat.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await repeat.Content.ReadAsStringAsync());
        Assert.Equal(first.Headers.Location, repeat.Headers.Location);
        var view = (await client.GetFromJsonAsync<ProposalResponse>(first.Headers.Location))!;
        Assert.Equal("RECEBIDA", view.Estado); Assert.Equal("AGUARDANDO_PROCESSAMENTO", view.SituacaoOperacional);
        Assert.Single(view.Historico); Assert.Empty(view.Motivos); Assert.Null(view.DecididaEm);
        await using var database = new DesignTimeDbContextFactory().CreateDbContext([]);
        Assert.Equal(1, await database.Proposals.CountAsync(row => row.Id == accepted.PropostaId));
        Assert.Equal(1, await database.ProposalHistory.CountAsync(row => row.ProposalId == accepted.PropostaId));
        var message = await database.Outbox.SingleAsync(row => row.ProposalId == accepted.PropostaId);
        Assert.Equal("SolicitarAnalise", message.Type); Assert.Null(message.PublishedAt);
        Assert.Equal("analise.solicitar", message.RoutingKey); Assert.NotEmpty(message.AnalysisId);
        Assert.DoesNotContain(company.Cnpj, message.Payload);
    }

    [Fact]
    public async Task ConcurrentIdenticalRequestsHaveOneEffect()
    {
        await using var api = new IntakeFactory { Probe = new ReservationBarrier(8) };
        var company = await api.CompanyAsync(); using var client = api.Client(1);
        var key = Guid.NewGuid().ToString("N");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => IntakeFactory.PostAsync(client, "/propostas", Request(company.Cnpj), key)));
        Assert.All(results, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        var bodies = await Task.WhenAll(results.Select(response => response.Content.ReadAsStringAsync()));
        Assert.Single(bodies.Distinct());
        await using var database = new DesignTimeDbContextFactory().CreateDbContext([]);
        Assert.Equal(1, await database.Proposals.CountAsync(row => row.CompanyId == company.EmpresaId));
        Assert.Equal(1, await database.Idempotency.CountAsync(row => row.Scope == company.EmpresaId && row.Key == key));
        var id = (await results[0].Content.ReadFromJsonAsync<ProposalAccepted>())!.PropostaId;
        Assert.Equal(1, await database.Outbox.CountAsync(row => row.ProposalId == id));
        Assert.Equal(1, await database.ProposalHistory.CountAsync(row => row.ProposalId == id));
    }

    [Fact]
    public async Task ConcurrentDifferentBodiesConflictWithoutSecondEffect()
    {
        await using var api = new IntakeFactory { Probe = new ReservationBarrier(2) };
        var company = await api.CompanyAsync(); using var client = api.Client(1);
        var key = Guid.NewGuid().ToString("N");
        var results = await Task.WhenAll(IntakeFactory.PostAsync(client, "/propostas", Request(company.Cnpj), key),
            IntakeFactory.PostAsync(client, "/propostas", Request(company.Cnpj, 2000), key));
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Accepted);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var database = new DesignTimeDbContextFactory().CreateDbContext([]);
        Assert.Equal(1, await database.Proposals.CountAsync(row => row.CompanyId == company.EmpresaId));
    }

    [Fact]
    public async Task SameKeyInDifferentCompaniesIsIndependentAndCannotLeakData()
    {
        await using var api = new IntakeFactory();
        var a = await api.CompanyAsync(1); var b = await api.CompanyAsync(2);
        using var clientA = api.Client(1); using var clientB = api.Client(2);
        var key = Guid.NewGuid().ToString("N");
        var first = await IntakeFactory.PostAsync(clientA, "/propostas", Request(a.Cnpj), key);
        var second = await IntakeFactory.PostAsync(clientB, "/propostas", Request(b.Cnpj), key);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode); Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.NotEqual(first.Headers.Location, second.Headers.Location);
        clientB.DefaultRequestHeaders.Add("X-Empresa-Id", a.EmpresaId);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync(first.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await IntakeFactory.PostAsync(clientB, "/propostas", Request(a.Cnpj))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await IntakeFactory.PostAsync(clientA, "/propostas", Request(a.Cnpj, 1500), key)).StatusCode);
    }

    [Fact]
    public async Task FailureAfterWritesBeforeCommitRollsBackEveryRecordAndAllowsRetry()
    {
        var failure = new FailBeforeCommit();
        await using var api = new IntakeFactory { Hook = failure };
        var company = await api.CompanyAsync(); using var client = api.Client(1);
        var key = Guid.NewGuid().ToString("N");
        var failed = await IntakeFactory.PostAsync(client, "/propostas", Request(company.Cnpj), key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.DoesNotContain("fault-injection-sentinel", await failed.Content.ReadAsStringAsync());
        Assert.NotNull(failure.ProposalId);
        await using var database = new DesignTimeDbContextFactory().CreateDbContext([]);
        Assert.False(await database.Proposals.AnyAsync(row => row.Id == failure.ProposalId));
        Assert.False(await database.ProposalHistory.AnyAsync(row => row.ProposalId == failure.ProposalId));
        Assert.False(await database.Outbox.AnyAsync(row => row.ProposalId == failure.ProposalId));
        Assert.False(await database.Idempotency.AnyAsync(row => row.Scope == company.EmpresaId && row.Key == key));
        // Retry through a second host with the same authenticated identities and the normal hook.
        using var retryHost = api.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IProposalCommitHook, NoOpProposalCommitHook>(services);
        }));
        using var retry = retryHost.CreateClient(); retry.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.Tokens[1]);
        Assert.Equal(HttpStatusCode.Accepted, (await IntakeFactory.PostAsync(retry, "/propostas", Request(company.Cnpj), key)).StatusCode);
    }

    [Fact]
    public async Task InvalidBodiesKeysAndPermissionsHaveNoEffect()
    {
        await using var api = new IntakeFactory(); var company = await api.CompanyAsync();
        using var client = api.Client(1);
        foreach (var invalid in new[] { Request(company.Cnpj, 0), Request(company.Cnpj) with { PrazoMeses = 0 },
            Request(company.Cnpj) with { Moeda = "USD" }, Request(company.Cnpj) with { FaturamentoMensal = 1.001m } })
            Assert.Equal(HttpStatusCode.BadRequest, (await IntakeFactory.PostAsync(client, "/propostas", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/propostas", Request(company.Cnpj))).StatusCode);
        var unknownField = new { company.Cnpj, faturamentoMensal = 10000, valorSolicitado = 1000, prazoMeses = 12, moeda = "BRL", empresaId = "forged" };
        Assert.Equal(HttpStatusCode.BadRequest, (await IntakeFactory.PostAsync(client, "/propostas", unknownField)).StatusCode);
        using var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/propostas/anything")).StatusCode);
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/propostas/anything")).StatusCode);
        foreach (var identity in new[] { 0, 3, 4 })
        {
            using var forbidden = api.Client(identity);
            Assert.Equal(HttpStatusCode.Forbidden, (await IntakeFactory.PostAsync(forbidden, "/propostas", Request(company.Cnpj))).StatusCode);
        }
        using var unbound = api.Client(2);
        Assert.Equal(HttpStatusCode.Forbidden, (await IntakeFactory.PostAsync(unbound, "/propostas", Request(company.Cnpj))).StatusCode);
        await using var database = new DesignTimeDbContextFactory().CreateDbContext([]);
        Assert.False(await database.Proposals.AnyAsync(row => row.CompanyId == company.EmpresaId));
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void DevelopmentAuthenticationCannotStartOutsideDevelopment(string environment)
    {
        using var host = new IntakeFactory().WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        var exception = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("Development", exception.Message);
    }
}
