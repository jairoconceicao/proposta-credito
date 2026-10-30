using System.Text;
using System.Text.Json;
using PropostaCredito.Infrastructure.Messaging;

namespace PropostaCredito.UnitTests;

public sealed class CreditMessageTests
{
    private static CreditMessage Message()
    {
        var proposal = Guid.NewGuid().ToString("N");
        return new CreditMessage(Guid.NewGuid().ToString("N"), "SolicitarAnalise", 1, proposal,
            Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), proposal, DateTime.UtcNow, 0, "initial",
            JsonSerializer.SerializeToElement(new { propostaId = proposal }), null);
    }

    [Fact]
    public void RoundTripsLogicalEnvelopeWithoutDerivedProperties()
    {
        var message = Message(); var body = message.Serialize();
        var result = CreditMessage.Parse(body, "SolicitarAnalise");
        Assert.Equal(message.MessageId, result.MessageId);
        Assert.Equal(message.LogicalHash, result.LogicalHash);
        Assert.DoesNotContain("logicalHash", Encoding.UTF8.GetString(body));
        Assert.Equal(message.LogicalHash, (result with { Attempt = 1, ReplayId = "replay" }).LogicalHash);
    }

    [Fact]
    public void RejectsUnsupportedSchemaWrongStageMalformedAndOversizedMessages()
    {
        var message = Message();
        Assert.Throws<MessageContractException>(() => CreditMessage.Parse((message with { SchemaVersion = 2 }).Serialize(), message.Type));
        Assert.Throws<MessageContractException>(() => CreditMessage.Parse(message.Serialize(), "SolicitarBureau"));
        Assert.Throws<MessageContractException>(() => CreditMessage.Parse("not json"u8, message.Type));
        Assert.Throws<MessageContractException>(() => CreditMessage.Parse(new byte[65537], message.Type));
        Assert.Throws<MessageContractException>(() => CreditMessage.Parse((message with { PropostaId = "forged" }).Serialize(), message.Type));
    }
}
