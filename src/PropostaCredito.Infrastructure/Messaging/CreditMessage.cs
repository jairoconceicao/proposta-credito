using System.Text.Json;
using System.Text.Json.Serialization;
using PropostaCredito.Infrastructure.Modules.Shared;

namespace PropostaCredito.Infrastructure.Messaging;

public sealed class MessageContractException : Exception
{
    public MessageContractException() : base("Envelope de mensagem invalido ou nao suportado.") { }
}

public sealed record CreditMessage(string MessageId, string Type, int SchemaVersion, string PropostaId,
    string AnaliseId, string CorrelationId, string CausationId, DateTime OccurredAt, int Attempt,
    string ReplayId, JsonElement Payload, string? TraceParent)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static readonly IReadOnlyDictionary<string, (string Route, string Queue)> Routes =
        new Dictionary<string, (string, string)>
        {
            ["SolicitarAnalise"] = ("analise.solicitar", "fila-solicitacao-analise"),
            ["SolicitarBureau"] = ("bureau.solicitar", "fila-solicitacao-bureau"),
            ["ResultadoBureauRecebido"] = ("bureau.resultado", "fila-resultado-bureau"),
            ["GerarContrato"] = ("contrato.gerar", "fila-solicitacao-contrato")
        };
    public const string Exchange = "proposta-credito.credito.v1";
    public const string DeadLetterExchange = "proposta-credito.credito.dlx.v1";

    public static CreditMessage FromOutbox(OutboxMessage row) => new(row.MessageId, row.Type, row.SchemaVersion,
        row.ProposalId, row.AnalysisId, row.CorrelationId, row.CausationId,
        DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc), row.Attempt, row.ReplayId,
        JsonSerializer.Deserialize<JsonElement>(row.Payload), row.TraceParent);
    public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(this, Json);
    public static CreditMessage Parse(ReadOnlySpan<byte> body, string expectedType)
    {
        try
        {
            if (body.Length is 0 or > 65536) throw new MessageContractException();
            var message = JsonSerializer.Deserialize<CreditMessage>(body, Json) ?? throw new MessageContractException();
            if (message.SchemaVersion != 1 || message.Type != expectedType || !Routes.ContainsKey(message.Type)
                || !Guid.TryParseExact(message.MessageId, "N", out _) || !Guid.TryParseExact(message.PropostaId, "N", out _)
                || !Guid.TryParseExact(message.AnaliseId, "N", out _) || !SafeId(message.CorrelationId, 64)
                || !SafeId(message.CausationId, 64) || !SafeId(message.ReplayId, 32)
                || message.OccurredAt.Kind != DateTimeKind.Utc || message.Attempt is < 0 or > 100
                || message.Payload.ValueKind != JsonValueKind.Object
                || !message.Payload.TryGetProperty("propostaId", out var id) || id.GetString() != message.PropostaId)
                throw new MessageContractException();
            if (expectedType is "SolicitarBureau" or "ResultadoBureauRecebido" && !Guid.TryParseExact(message.QueryId, "N", out _))
                throw new MessageContractException();
            if (message.TraceParent is not null && !System.Diagnostics.ActivityContext.TryParse(message.TraceParent, null, out _))
                throw new MessageContractException();
            return message;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new MessageContractException();
        }
    }
    private static bool SafeId(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':');
    [JsonIgnore] public string? QueryId => Payload.TryGetProperty("consultaId", out var query) ? query.GetString() : null;
    [JsonIgnore] public string LogicalHash => IntakeValidation.Hash(Type, PropostaId, AnaliseId, QueryId ?? "");
}
