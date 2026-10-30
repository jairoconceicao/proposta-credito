using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PropostaCredito.Infrastructure.Modules.Shared;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.Infrastructure.Modules.Propostas;

public sealed class Proposal
{
    public required string Id { get; init; }
    public required string CompanyId { get; init; }
    public required string AnalysisId { get; init; }
    public required string Cnpj { get; init; }
    public decimal MonthlyRevenue { get; init; }
    public decimal Amount { get; init; }
    public int Months { get; init; }
    public string Currency { get; init; } = "BRL";
    public string State { get; set; } = "RECEBIDA";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; init; }
}

public sealed class ProposalHistory
{
    public required string Id { get; init; }
    public required string ProposalId { get; init; }
    public required string State { get; init; }
    public required string Actor { get; init; }
    public DateTime OccurredAt { get; init; }
}

public sealed record SubmitProposal(string? Cnpj, decimal FaturamentoMensal, decimal ValorSolicitado, int PrazoMeses, string? Moeda);
public sealed record ProposalAccepted(string PropostaId, string Estado);
public sealed record HistoryResponse(string Estado, DateTime Instante);
public sealed record ProposalResponse(string PropostaId, string EmpresaId, string Estado, int Versao,
    DateTime CriadaEm, string SituacaoOperacional, string[] Motivos, DateTime? DecididaEm, HistoryResponse[] Historico,
    string? VersaoRegras = null, DateTime? AvaliadaEm = null);

// Test seam is injected through DI only; no HTTP/config switch can trigger a pre-commit fault.
public interface IProposalCommitHook
{
    Task BeforeCommitAsync(string proposalId, CancellationToken cancellationToken);
}

public sealed class NoOpProposalCommitHook : IProposalCommitHook
{
    public Task BeforeCommitAsync(string proposalId, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ProposalService(IdempotentCommandExecutor commands,
    IDbContextFactory<PlatformDbContext> factory, IProposalCommitHook hook)
{
    public async Task<ProposalAccepted> SubmitAsync(SubmitProposal request, string subject, string key,
        string correlationId, CancellationToken cancellationToken)
    {
        var cnpj = IntakeValidation.Cnpj(request.Cnpj);
        var revenue = IntakeValidation.Money(request.FaturamentoMensal, "faturamento-mensal");
        var amount = IntakeValidation.Money(request.ValorSolicitado, "valor-solicitado");
        if (request.PrazoMeses is < 1 or > 60) throw IntakeValidation.Invalid("prazo-meses", "Prazo permitido: 1 a 60 meses.");
        var currency = (request.Moeda ?? "").Trim().ToUpperInvariant();
        if (currency != "BRL") throw IntakeValidation.Invalid("moeda", "Moeda suportada: BRL.");

        await using var reader = await factory.CreateDbContextAsync(cancellationToken);
        var companyId = await reader.Memberships.Where(row => row.Subject == subject).Select(row => row.CompanyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new IntakeException(403, "company-membership", "Identidade sem vinculo com empresa.");
        if (!await reader.Companies.AnyAsync(row => row.Id == companyId && row.Cnpj == cnpj, cancellationToken))
            throw IntakeValidation.Invalid("cnpj", "CNPJ deve corresponder a empresa da identidade.");
        var hash = IntakeValidation.Hash("proposal-v1", cnpj, IntakeValidation.CanonicalMoney(revenue),
            IntakeValidation.CanonicalMoney(amount), request.PrazoMeses.ToString(CultureInfo.InvariantCulture), currency);
        return await commands.ExecuteAsync(companyId, "POST /propostas", key, hash, async database =>
        {
            var now = DateTime.UtcNow;
            var proposal = new Proposal
            {
                Id = Guid.NewGuid().ToString("N"), CompanyId = companyId, Cnpj = cnpj, AnalysisId = Guid.NewGuid().ToString("N"),
                MonthlyRevenue = revenue, Amount = amount, Months = request.PrazoMeses, CreatedAt = now
            };
            database.Proposals.Add(proposal);
            database.ProposalHistory.Add(new ProposalHistory
            {
                Id = Guid.NewGuid().ToString("N"), ProposalId = proposal.Id, State = proposal.State,
                Actor = subject, OccurredAt = now
            });
            var messageId = Guid.NewGuid().ToString("N");
            database.Outbox.Add(new OutboxMessage
            {
                Id = Guid.NewGuid().ToString("N"), MessageId = messageId, ProposalId = proposal.Id, AnalysisId = proposal.AnalysisId,
                Type = "SolicitarAnalise", CorrelationId = correlationId, CausationId = proposal.Id,
                TraceParent = Activity.Current?.Id, OccurredAt = now, AvailableAt = now,
                Payload = JsonSerializer.Serialize(new { propostaId = proposal.Id })
            });
            await database.SaveChangesAsync(cancellationToken);
            await hook.BeforeCommitAsync(proposal.Id, cancellationToken);
            return new ProposalAccepted(proposal.Id, proposal.State);
        }, cancellationToken);
    }

    public async Task<ProposalResponse> GetAsync(string id, string subject, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var proposal = await (from row in database.Proposals.AsNoTracking()
                              join membership in database.Memberships on row.CompanyId equals membership.CompanyId
                              where row.Id == id && membership.Subject == subject select row).SingleOrDefaultAsync(cancellationToken)
            ?? throw new IntakeException(404, "not-found", "Proposta nao encontrada.");
        var history = await database.ProposalHistory.AsNoTracking().Where(row => row.ProposalId == id)
            .OrderBy(row => row.OccurredAt).Select(row => new HistoryResponse(row.State, row.OccurredAt)).ToArrayAsync(cancellationToken);
        var analysis = await database.Analyses.AsNoTracking().SingleOrDefaultAsync(row => row.Id == proposal.AnalysisId, cancellationToken);
        var decision = await database.Decisions.AsNoTracking().SingleOrDefaultAsync(row => row.AnalysisId == proposal.AnalysisId, cancellationToken);
        var situation = proposal.State switch
        {
            "APROVADA" => "CONTRATO_PENDENTE",
            "REJEITADA" => "CONCLUIDA",
            "EM_ANALISE_MANUAL" => "AGUARDANDO_ANALISTA",
            _ => "AGUARDANDO_PROCESSAMENTO"
        };
        return new ProposalResponse(proposal.Id, proposal.CompanyId, proposal.State, proposal.Version,
            DateTime.SpecifyKind(proposal.CreatedAt, DateTimeKind.Utc), situation,
            analysis?.ReasonsJson is { } reasons ? JsonSerializer.Deserialize<string[]>(reasons)! : [],
            decision is null ? null : DateTime.SpecifyKind(decision.DecidedAt, DateTimeKind.Utc),
            history.Select(row => row with { Instante = DateTime.SpecifyKind(row.Instante, DateTimeKind.Utc) }).ToArray(),
            analysis?.RulesVersion, analysis?.EvaluatedAt is { } evaluated ? DateTime.SpecifyKind(evaluated, DateTimeKind.Utc) : null);
    }
}
