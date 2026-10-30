using Oracle.ManagedDataAccess.Client;

namespace PropostaCredito.Infrastructure.Persistence;

public static class OracleSettings
{
    public static bool IsValid(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var builder = new OracleConnectionStringBuilder(connectionString);
            return !string.IsNullOrWhiteSpace(builder.DataSource)
                && !string.IsNullOrWhiteSpace(builder.UserID)
                && !string.IsNullOrWhiteSpace(builder.Password);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (OracleException)
        {
            return false;
        }
    }

    public static string RequireValid(string? connectionString)
    {
        if (!IsValid(connectionString))
        {
            throw new InvalidOperationException("Configure uma conexao Oracle com Data Source, User Id e Password.");
        }

        return connectionString!;
    }
}
