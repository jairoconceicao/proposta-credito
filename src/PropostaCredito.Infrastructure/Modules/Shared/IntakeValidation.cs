using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PropostaCredito.Infrastructure.Modules.Shared;

public sealed class IntakeException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public static partial class IntakeValidation
{
    public static string Cnpj(string? input)
    {
        var value = (input ?? "").Trim().ToUpperInvariant();
        if (FormattedCnpj().IsMatch(value))
            value = value.Replace(".", "").Replace("/", "").Replace("-", "");
        if (!RawCnpj().IsMatch(value) || value.Distinct().Count() == 1)
            throw Invalid("cnpj", "CNPJ invalido.");
        for (var length = 12; length <= 13; length++)
        {
            var sum = 0;
            for (var index = 0; index < length; index++)
                sum += (value[index] - 48) * ((length - index - 1) % 8 + 2);
            var remainder = sum % 11;
            if (value[length] - '0' != (remainder < 2 ? 0 : 11 - remainder))
                throw Invalid("cnpj", "CNPJ invalido.");
        }
        return value;
    }

    public static string Text(string? input, string field, int maximum)
    {
        var value = (input ?? "").Trim();
        if (value.Length == 0 || value.Length > maximum || value.Any(char.IsControl))
            throw Invalid(field, "Campo obrigatorio ou fora do limite permitido.");
        return value;
    }

    public static string IdempotencyKey(string? input)
    {
        if (input is null || !KeyPattern().IsMatch(input))
            throw Invalid("idempotency-key", "Idempotency-Key obrigatoria: 1 a 128 caracteres ASCII alfanumericos ou . _ : -.");
        return input;
    }

    public static decimal Money(decimal value, string field)
    {
        if (value <= 0 || value > 999_999_999_999.99m || decimal.Round(value, 2) != value)
            throw Invalid(field, "Valor deve ser positivo, com ate duas casas decimais e dentro de NUMBER(14,2).");
        return value;
    }

    public static string Hash(params string[] fields)
    {
        // Length-prefixed UTF-8 fields avoid ambiguous delimiters and culture-dependent hashes.
        var canonical = string.Concat(fields.Select(field => $"{Encoding.UTF8.GetByteCount(field)}:{field}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string CanonicalMoney(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    public static IntakeException Invalid(string field, string message) => new(400, field, message);
    [GeneratedRegex("^[A-Z0-9]{12}[0-9]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex RawCnpj();
    [GeneratedRegex("^[A-Z0-9]{2}\\.[A-Z0-9]{3}\\.[A-Z0-9]{3}/[A-Z0-9]{4}-[0-9]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormattedCnpj();
    [GeneratedRegex("^[A-Za-z0-9._:-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
