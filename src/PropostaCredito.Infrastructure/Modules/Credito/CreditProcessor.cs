using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using PropostaCredito.Infrastructure.Messaging;
using PropostaCredito.Infrastructure.Modules.Propostas;
using PropostaCredito.Infrastructure.Modules.Shared;
using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.Infrastructure.Modules.Credito;

public interface ICreditCommitHook
{
    Task BeforeCommitAsync(CreditMessage message, CancellationToken cancellationToken);
}

public sealed class CreditProcessor(IDbContextFactory<PlatformDbContext> factory, ILogger<CreditProcessor> logger, ICreditCommitHook? hook = null)
{
    public async Task HandleAsync(CreditMessage message, CancellationToken cancellationToken)
    {
        using var activity = new Activity("credito." + message.Type);
        if (message.TraceParent is not null) activity.SetParentId(message.TraceParent);
        activity.Start();
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["messageId"] = message.MessageId, ["propostaId"] = message.PropostaId,
            ["correlationId"] = message.CorrelationId, ["stage"] = message.Type
        });
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize transitions and inbox checks for one proposal, including duplicate consumers.
        using (var command = database.Database.GetDbConnection().CreateCommand())
        {
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT \"Version\" FROM PC_OWNER.PROPOSTAS WHERE \"Id\" = :proposalId FOR UPDATE";
            command.CommandTimeout = 10;
            var parameter = command.CreateParameter(); parameter.ParameterName = "proposalId"; parameter.Value = message.PropostaId;
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync(cancellationToken);
        }
        var proposal = await database.Proposals.SingleOrDefaultAsync(row => row.Id == message.PropostaId, cancellationToken);
        var consumer = message.Type;
        var previous = await database.Inbox.AsNoTracking().SingleOrDefaultAsync(row =>
            row.Consumer == consumer && row.MessageId == message.MessageId, cancellationToken);
        if (previous is not null)
        {
            if (previous.LogicalHash != message.LogicalHash) throw new MessageContractException();
            return;
        }
        var result = "IGNORED_STALE";
        if (proposal is not null && proposal.AnalysisId == message.AnaliseId)
        {
            result = message.Type switch
            {
                "SolicitarAnalise" => await PrepareAsync(database, proposal, message, cancellationToken),
                "SolicitarBureau" => await ReadyQueryAsync(database, proposal, message, cancellationToken),
                "ResultadoBureauRecebido" => await DecideAsync(database, proposal, message, cancellationToken),
                _ => throw new MessageContractException()
            };
        }
        database.Inbox.Add(new InboxEntry
        {
            Consumer = consumer, MessageId = message.MessageId, ProposalId = message.PropostaId,
            LogicalHash = message.LogicalHash, Result = result, CompletedAt = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        if (hook is not null) await hook.BeforeCommitAsync(message, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Etapa confirmada: {Result}", result);
    }

    private static Task<string> PrepareAsync(PlatformDbContext database, Proposal proposal, CreditMessage message, CancellationToken cancellationToken)
    {
        if (proposal.State != "RECEBIDA") return Task.FromResult("IGNORED_STALE");
        var now = DateTime.UtcNow;
        var queryId = Guid.NewGuid().ToString("N");
        var analysis = new CreditAnalysis
        {
            Id = proposal.AnalysisId, ProposalId = proposal.Id, RulesVersion = CreditRules.Version,
            ProposalSnapshot = JsonSerializer.Serialize(new ProposalCreditSnapshot(proposal.MonthlyRevenue, proposal.Amount, proposal.Months, proposal.Currency)),
            CurrentQueryId = queryId, CreatedAt = now, DeadlineAt = now.AddMinutes(15)
        };
        database.Analyses.Add(analysis);
        database.BureauQueries.Add(NewQuery(proposal, message, queryId, now));
        Transition(database, proposal, "EM_ANALISE", now);
        Transition(database, proposal, "AGUARDANDO_BUREAU", now.AddTicks(1));
        database.Outbox.Add(Command(proposal, "SolicitarBureau", message.CorrelationId, message.MessageId, queryId));
        return Task.FromResult("ANALISE_PREPARADA");
    }

    private static async Task<string> ReadyQueryAsync(PlatformDbContext database, Proposal proposal, CreditMessage message, CancellationToken ct)
    {
        if (proposal.State != "AGUARDANDO_BUREAU") return "IGNORED_STALE";
        var analysis = await database.Analyses.SingleAsync(row => row.Id == proposal.AnalysisId, ct);
        if (analysis.CurrentQueryId != message.QueryId) return "IGNORED_STALE";
        var query = await database.BureauQueries.SingleAsync(row => row.Id == message.QueryId, ct);
        if (query.State == "PENDENTE_COMANDO") query.State = "PRONTA_ENVIO";
        return "INTENCAO_DURAVEL";
    }

    private static async Task<string> DecideAsync(PlatformDbContext database, Proposal proposal, CreditMessage message, CancellationToken ct)
    {
        if (proposal.State != "AGUARDANDO_BUREAU") return "IGNORED_STALE";
        var analysis = await database.Analyses.SingleAsync(row => row.Id == proposal.AnalysisId, ct);
        if (analysis.CurrentQueryId != message.QueryId) return "IGNORED_STALE";
        var query = await database.BureauQueries.SingleAsync(row => row.Id == message.QueryId, ct);
        if (query.State != "CONCLUIDA" || query.ValidUntil is null || query.ExistingDebt is null || query.TotalLimit is null)
            throw new MessageContractException();
        var now = DateTime.UtcNow;
        if (analysis.DeadlineAt <= now)
        {
            analysis.Outcome = "EM_ANALISE_MANUAL"; analysis.ReasonsJson = JsonSerializer.Serialize(new[] { "PRAZO_ANALISE_ESGOTADO" });
            analysis.EvaluatedAt = now; Transition(database, proposal, analysis.Outcome, now);
            return "PRAZO_ANALISE_ESGOTADO";
        }
        if (query.ValidUntil <= now)
        {
            var queryId = Guid.NewGuid().ToString("N");
            analysis.CurrentQueryId = queryId;
            database.BureauQueries.Add(NewQuery(proposal, message, queryId, now));
            database.Outbox.Add(Command(proposal, "SolicitarBureau", message.CorrelationId, message.MessageId, queryId));
            return "RISCO_VENCIDO_RECONSULTA";
        }
        if (analysis.RulesVersion != CreditRules.Version) throw new InvalidOperationException("Versao de regras nao implementada.");
        var snapshot = JsonSerializer.Deserialize<ProposalCreditSnapshot>(analysis.ProposalSnapshot)
            ?? throw new InvalidOperationException("Snapshot da proposta ausente.");
        var account = await database.CreditAccounts.AsNoTracking().SingleAsync(row => row.CompanyId == proposal.CompanyId, ct);
        var available = Math.Max(0, Math.Min(account.TotalLimit, query.TotalLimit.Value) - account.Reserved);
        var verdict = CreditRules.Evaluate(snapshot.MonthlyRevenue, snapshot.Amount, snapshot.Months, query.ExistingDebt.Value, available);
        if (verdict.State == "APROVADA")
        {
            var reserved = await database.CreditAccounts.Where(row => row.CompanyId == proposal.CompanyId
                && row.Reserved + snapshot.Amount <= row.TotalLimit && row.Reserved + snapshot.Amount <= query.TotalLimit.Value)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Reserved, row => row.Reserved + snapshot.Amount), ct);
            if (reserved == 0)
            {
                account = await database.CreditAccounts.AsNoTracking().SingleAsync(row => row.CompanyId == proposal.CompanyId, ct);
                available = Math.Max(0, Math.Min(account.TotalLimit, query.TotalLimit.Value) - account.Reserved);
                verdict = verdict with { State = "REJEITADA", Reasons = [.. verdict.Reasons.Where(reason => reason != "LIMITE_OK"), "LIMITE_INSUFICIENTE"] };
            }
        }
        analysis.Outcome = verdict.State; analysis.ReasonsJson = JsonSerializer.Serialize(verdict.Reasons); analysis.EvaluatedAt = now;
        if (verdict.State != "EM_ANALISE_MANUAL")
            database.Decisions.Add(new CreditDecision
            {
                AnalysisId = analysis.Id, ProposalId = proposal.Id, QueryId = query.Id, RulesVersion = analysis.RulesVersion,
                Outcome = verdict.State, ReasonsJson = analysis.ReasonsJson, DecidedAt = now,
                InputsJson = JsonSerializer.Serialize(new { snapshot.MonthlyRevenue, snapshot.Amount, snapshot.Months,
                    query.ExistingDebt, query.TotalLimit, query.ObtainedAt, query.ValidUntil, Available = available, verdict.Installment })
            });
        Transition(database, proposal, "EM_ANALISE", now);
        Transition(database, proposal, verdict.State, now.AddTicks(1));
        if (verdict.State == "APROVADA") database.Outbox.Add(Command(proposal, "GerarContrato", message.CorrelationId, message.MessageId));
        return verdict.State;
    }

    private static BureauQuery NewQuery(Proposal proposal, CreditMessage message, string id, DateTime now) => new()
    {
        Id = id, AnalysisId = proposal.AnalysisId, ProposalId = proposal.Id, CreatedAt = now,
        CorrelationId = message.CorrelationId, CausationId = message.MessageId, TraceParent = Activity.Current?.Id
    };

    internal static OutboxMessage Command(Proposal proposal, string type, string correlation, string causation, string? queryId = null) => new()
    {
        Id = Guid.NewGuid().ToString("N"), MessageId = Guid.NewGuid().ToString("N"), ProposalId = proposal.Id, AnalysisId = proposal.AnalysisId,
        Type = type, RoutingKey = CreditMessage.Routes[type].Route, CorrelationId = correlation, CausationId = causation,
        OccurredAt = DateTime.UtcNow, AvailableAt = DateTime.UtcNow, TraceParent = Activity.Current?.Id,
        Payload = JsonSerializer.Serialize(queryId is null ? new Dictionary<string, string> { ["propostaId"] = proposal.Id }
            : new Dictionary<string, string> { ["propostaId"] = proposal.Id, ["consultaId"] = queryId })
    };

    private static void Transition(PlatformDbContext database, Proposal proposal, string state, DateTime at)
    {
        proposal.State = state; proposal.Version++;
        database.ProposalHistory.Add(new ProposalHistory
        {
            Id = Guid.NewGuid().ToString("N"), ProposalId = proposal.Id, State = state, Actor = "worker:lab-v1", OccurredAt = at
        });
    }
}
