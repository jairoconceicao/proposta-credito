namespace PropostaCredito.Infrastructure.Modules.Shared;

public sealed class IdempotencyRecord
{
    public required string Scope { get; init; }
    public required string Operation { get; init; }
    public required string Key { get; init; }
    public required string BodyHash { get; init; }
    public string ResponseJson { get; set; } = "{}";
    public DateTime CreatedAt { get; init; }
}

public sealed class OutboxMessage
{
    public required string Id { get; init; }
    public required string MessageId { get; init; }
    public required string ProposalId { get; init; }
    public required string AnalysisId { get; init; }
    public string RoutingKey { get; init; } = "analise.solicitar";
    public required string Type { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public required string CorrelationId { get; init; }
    public required string CausationId { get; init; }
    public string? TraceParent { get; init; }
    public required string Payload { get; init; }
    public int Attempt { get; set; }
    public string ReplayId { get; set; } = "initial";
    public DateTime OccurredAt { get; init; }
    public DateTime AvailableAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? Error { get; set; }
}
