namespace PropostaCredito.Infrastructure.Modules.Credito;

public sealed record ProposalCreditSnapshot(decimal MonthlyRevenue, decimal Amount, int Months, string Currency);

public sealed class CreditAccount
{
    public required string CompanyId { get; init; }
    public decimal TotalLimit { get; init; } = CreditRules.Limit;
    public decimal Reserved { get; set; }
}

public sealed class CreditAnalysis
{
    public required string Id { get; init; }
    public required string ProposalId { get; init; }
    public required string RulesVersion { get; init; }
    public required string ProposalSnapshot { get; init; }
    public required string CurrentQueryId { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime DeadlineAt { get; init; }
    public string? Outcome { get; set; }
    public string? ReasonsJson { get; set; }
    public DateTime? EvaluatedAt { get; set; }
}

public sealed class BureauQuery
{
    public required string Id { get; init; }
    public required string AnalysisId { get; init; }
    public required string ProposalId { get; init; }
    public string State { get; set; } = "PENDENTE_COMANDO";
    public string Provider { get; init; } = "risco-lab-v1";
    public required string CorrelationId { get; init; }
    public required string CausationId { get; init; }
    public string? TraceParent { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ObtainedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public decimal? ExistingDebt { get; set; }
    public decimal? TotalLimit { get; set; }
}

public sealed class CreditDecision
{
    public required string AnalysisId { get; init; }
    public required string ProposalId { get; init; }
    public required string QueryId { get; init; }
    public required string RulesVersion { get; init; }
    public required string Outcome { get; init; }
    public required string ReasonsJson { get; init; }
    public required string InputsJson { get; init; }
    public string Author { get; init; } = "motor:lab-v1";
    public DateTime DecidedAt { get; init; }
}

public sealed class InboxEntry
{
    public required string Consumer { get; init; }
    public required string MessageId { get; init; }
    public required string ProposalId { get; init; }
    public required string LogicalHash { get; init; }
    public required string Result { get; init; }
    public DateTime CompletedAt { get; init; }
}
