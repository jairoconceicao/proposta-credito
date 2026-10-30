namespace PropostaCredito.Infrastructure.Modules.Credito;

public sealed record RiskSnapshot(decimal ExistingDebt, decimal TotalLimit, DateTime ObtainedAt, DateTime ValidUntil);
public sealed record CreditVerdict(string State, string[] Reasons, decimal Installment);

public static class CreditRules
{
    public const string Version = "lab-v1";
    public const decimal Limit = 50_000m;
    public static CreditVerdict Evaluate(decimal revenue, decimal amount, int months, decimal debt, decimal available)
    {
        if (revenue <= 0 || amount <= 0 || months <= 0 || debt < 0 || available < 0)
            throw new ArgumentOutOfRangeException(nameof(revenue), "Snapshot de credito invalido.");
        var installment = decimal.Round(amount / months, 2, MidpointRounding.AwayFromZero);
        var revenueCode = installment > revenue * .30m ? "RECEITA_EXCEDIDA" : installment > revenue * .25m ? "RECEITA_REVISAO" : "RECEITA_OK";
        var debtCode = debt > revenue * .50m ? "DIVIDA_EXCEDIDA" : debt > revenue * .40m ? "DIVIDA_REVISAO" : "DIVIDA_OK";
        var limitCode = amount > available ? "LIMITE_INSUFICIENTE" : "LIMITE_OK";
        var rejected = revenueCode == "RECEITA_EXCEDIDA" || debtCode == "DIVIDA_EXCEDIDA" || limitCode == "LIMITE_INSUFICIENTE";
        var manual = revenueCode == "RECEITA_REVISAO" || debtCode == "DIVIDA_REVISAO";
        return new CreditVerdict(rejected ? "REJEITADA" : manual ? "EM_ANALISE_MANUAL" : "APROVADA", [revenueCode, debtCode, limitCode], installment);
    }
}
