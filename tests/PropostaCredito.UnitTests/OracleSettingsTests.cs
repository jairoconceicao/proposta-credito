using PropostaCredito.Infrastructure.Persistence;

namespace PropostaCredito.UnitTests;

public sealed class OracleSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Password=confidential")]
    [InlineData("Data Source=localhost/FREEPDB1;User Id=PC_APP")]
    [InlineData("unknown=confidential")]
    public void RejectsIncompleteOrMalformedConfigurationWithoutExposingSecrets(string? connectionString)
    {
        Assert.False(OracleSettings.IsValid(connectionString));
        var error = Assert.Throws<InvalidOperationException>(() => OracleSettings.RequireValid(connectionString));
        Assert.DoesNotContain("confidential", error.Message);
    }

    [Fact]
    public void AcceptsExplicitOracleCredentials()
    {
        const string connectionString = "Data Source=localhost:15210/FREEPDB1;User Id=PC_APP;Password=fictional";
        Assert.Equal(connectionString, OracleSettings.RequireValid(connectionString));
    }
}
