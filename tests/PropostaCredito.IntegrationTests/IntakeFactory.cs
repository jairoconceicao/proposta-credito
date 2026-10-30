using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PropostaCredito.Api.Security;
using PropostaCredito.Infrastructure.Modules.Empresas;
using PropostaCredito.Infrastructure.Modules.Propostas;
using PropostaCredito.Infrastructure.Modules.Shared;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.IntegrationTests;

public sealed class IntakeFactory : WebApplicationFactory<Program>
{
    public string Prefix { get; } = "m1-" + Guid.NewGuid().ToString("N");
    public string[] Tokens { get; } = Enumerable.Range(0, 5).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(32))).ToArray();
    public IProposalCommitHook Hook { get; set; } = new NoOpProposalCommitHook();
    public IIdempotencyReservationProbe Probe { get; set; } = new NoOpReservationProbe();
    public string Subject(int index) => Prefix + "-" + index;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<DevelopmentIdentities>(options => options.Identities = Enumerable.Range(0, 5)
                .Select(index => new DevelopmentIdentity
                {
                    Subject = Subject(index), Token = Tokens[index],
                    Role = new[] { "Administrador", "Empresa", "Empresa", "Analista", "Operador" }[index]
                }).ToArray());
            services.RemoveAll<IProposalCommitHook>(); services.AddSingleton(Hook);
            services.RemoveAll<IIdempotencyReservationProbe>(); services.AddSingleton(Probe);
        });
    }

    public HttpClient Client(int identity)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Tokens[identity]);
        return client;
    }

    public async Task<CompanyResponse> CompanyAsync(int identity = 1, string? cnpj = null)
    {
        using var admin = Client(0);
        var response = await PostAsync(admin, "/empresas", new RegisterCompany(cnpj ?? FakeCnpj(), "Empresa ficticia", Subject(identity)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CompanyResponse>())!;
    }

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string? key = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return client.SendAsync(message);
    }

    public static string FakeCnpj()
    {
        var value = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        foreach (var weights in new[] { new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 }, new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 } })
        {
            var remainder = value.Select((character, index) => (character - '0') * weights[index]).Sum() % 11;
            value += remainder < 2 ? "0" : (11 - remainder).ToString();
        }
        return value;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await using var database = new DesignTimeDbContextFactory().CreateDbContext([]);
        var companyIds = await database.Memberships.Where(row => row.Subject.StartsWith(Prefix)).Select(row => row.CompanyId).ToArrayAsync();
        var proposalIds = await database.Proposals.Where(row => companyIds.Contains(row.CompanyId)).Select(row => row.Id).ToArrayAsync();
        await database.Outbox.Where(row => proposalIds.Contains(row.ProposalId)).ExecuteDeleteAsync();
        await database.ProposalHistory.Where(row => proposalIds.Contains(row.ProposalId)).ExecuteDeleteAsync();
        await database.Proposals.Where(row => companyIds.Contains(row.CompanyId)).ExecuteDeleteAsync();
        await database.Idempotency.Where(row => companyIds.Contains(row.Scope) || row.Scope == "admin:" + Subject(0)).ExecuteDeleteAsync();
        await database.Memberships.Where(row => row.Subject.StartsWith(Prefix)).ExecuteDeleteAsync();
        await database.Companies.Where(row => companyIds.Contains(row.Id)).ExecuteDeleteAsync();
    }
}

public sealed class ReservationBarrier(int participants) : IIdempotencyReservationProbe
{
    private int entered;
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task BeforeReservationAsync(string operation, CancellationToken cancellationToken)
    {
        if (operation != "POST /propostas") return;
        if (Interlocked.Increment(ref entered) == participants) release.TrySetResult();
        await release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
    }
}

public sealed class FailBeforeCommit : IProposalCommitHook
{
    public string? ProposalId { get; private set; }
    public Task BeforeCommitAsync(string proposalId, CancellationToken cancellationToken)
    {
        ProposalId = proposalId;
        throw new InvalidOperationException("fault-injection-sentinel");
    }
}
