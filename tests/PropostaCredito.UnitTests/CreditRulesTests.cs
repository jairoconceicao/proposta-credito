using PropostaCredito.Infrastructure.Modules.Credito;

namespace PropostaCredito.UnitTests;

public sealed class CreditRulesTests
{
    [Theory]
    [InlineData(30000, 1000, 50000, "APROVADA")]
    [InlineData(30000.12, 1000, 50000, "EM_ANALISE_MANUAL")]
    [InlineData(36000, 1000, 50000, "EM_ANALISE_MANUAL")]
    [InlineData(36000.12, 1000, 50000, "REJEITADA")]
    [InlineData(1000, 4000, 50000, "APROVADA")]
    [InlineData(1000, 4000.01, 50000, "EM_ANALISE_MANUAL")]
    [InlineData(1000, 5000, 50000, "EM_ANALISE_MANUAL")]
    [InlineData(1000, 5000.01, 50000, "REJEITADA")]
    [InlineData(1000, 1000, 1000, "APROVADA")]
    [InlineData(1000, 1000, 999.99, "REJEITADA")]
    [InlineData(33000, 6000, 50000, "REJEITADA")]
    public void ImplementsInclusiveThresholdsAndRejectionPrecedence(double amount, double debt, double available, string expected)
    {
        var verdict = CreditRules.Evaluate(10000, (decimal)amount, 12, (decimal)debt, (decimal)available);
        Assert.Equal(expected, verdict.State);
        Assert.Equal(3, verdict.Reasons.Length);
    }

    [Fact]
    public void RoundsMidpointAwayFromZero() => Assert.Equal(1.01m, CreditRules.Evaluate(1000, 2.01m, 2, 0, 1000).Installment);

    [Fact]
    public void RejectsInvalidSnapshot() => Assert.Throws<ArgumentOutOfRangeException>(() => CreditRules.Evaluate(0, 10, 1, 0, 100));
}
