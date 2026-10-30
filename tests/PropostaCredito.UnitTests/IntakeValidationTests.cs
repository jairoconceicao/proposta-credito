using System.Globalization;
using PropostaCredito.Infrastructure.Modules.Shared;

namespace PropostaCredito.UnitTests;

public sealed class IntakeValidationTests
{
    [Theory]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData("12.abc.345/01de-35", "12ABC34501DE35")]
    [InlineData(" 12ABC34501DE35 ", "12ABC34501DE35")]
    public void NormalizesAndValidatesNumericAndAlphanumericCnpj(string input, string expected) =>
        Assert.Equal(expected, IntakeValidation.Cnpj(input));

    [Theory]
    [InlineData(null)] [InlineData("00000000000000")] [InlineData("11222333000180")]
    [InlineData("12ABC34501DE36")] [InlineData("11x222333000181")]
    [InlineData("12ABC34501DE3A")] [InlineData("12_ABC34501DE35")]
    public void RejectsInvalidCnpj(string? input) => Assert.Throws<IntakeException>(() => IntakeValidation.Cnpj(input));

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(0.001)] [InlineData(1000000000000)]
    public void RejectsMoneyOutsideStorageContract(double input) =>
        Assert.Throws<IntakeException>(() => IntakeValidation.Money((decimal)input, "amount"));

    [Fact]
    public void CanonicalHashIsIndependentOfDecimalScaleAndCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal(IntakeValidation.Hash(IntakeValidation.CanonicalMoney(10.0m)),
                IntakeValidation.Hash(IntakeValidation.CanonicalMoney(10.00m)));
            Assert.NotEqual(IntakeValidation.Hash("a", "bc"), IntakeValidation.Hash("ab", "c"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
