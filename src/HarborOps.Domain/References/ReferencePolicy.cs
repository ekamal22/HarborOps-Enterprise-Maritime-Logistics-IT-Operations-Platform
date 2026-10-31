using System.Text.RegularExpressions;

namespace HarborOps.Domain.References;

public static class ReferencePolicy
{
    private static readonly Regex LocalCode = new("^[A-Z0-9]+(?:-[A-Z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CountryPattern = new("^[A-Z]{2}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LocodePattern = new("^[A-Z]{2}[A-Z2-9]{3}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ImoPattern = new("^[0-9]{7}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MmsiPattern = new("^[0-9]{9}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Code(string? value)
    {
        var result = (value ?? "").Trim().ToUpperInvariant();
        if (result.Length is < 3 or > 32 || !LocalCode.IsMatch(result))
            throw new ArgumentException("Invalid reference code");
        return result;
    }

    public static string Name(string? value)
    {
        var result = (value ?? "").Trim();
        if (result.Length is < 1 or > 160 || result.Any(char.IsControl))
            throw new ArgumentException("Invalid reference name");
        return result;
    }

    public static string Country(string? value)
    {
        var result = (value ?? "").Trim().ToUpperInvariant();
        if (!CountryPattern.IsMatch(result))
            throw new ArgumentException("Invalid country code");
        return result;
    }

    public static string? OptionalCountry(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Country(value);

    public static string? UnLocode(string? value, string country)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim().ToUpperInvariant();
        if (text.Length == 6 && text[2] == ' ')
            text = text[..2] + text[3..];
        if (!LocodePattern.IsMatch(text) || !text.StartsWith(country, StringComparison.Ordinal))
            throw new ArgumentException("Invalid UN/LOCODE");
        return text;
    }

    public static string? Imo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = value.Trim().ToUpperInvariant();
        if (digits.StartsWith("IMO", StringComparison.Ordinal)) digits = digits[3..].TrimStart();
        if (!ImoPattern.IsMatch(digits))
            throw new ArgumentException("Invalid IMO number");
        var check = 0;
        for (var i = 0; i < 6; i++)
            check += (digits[i] - '0') * (7 - i);
        if (check % 10 != digits[6] - '0')
            throw new ArgumentException("Invalid IMO checksum");
        return digits;
    }

    public static string? Mmsi(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        if (!MmsiPattern.IsMatch(result) || result.All(c => c == '0'))
            throw new ArgumentException("Invalid MMSI");
        return result;
    }

    public static string Status(string? value)
    {
        if (value is not ("Active" or "Archived"))
            throw new ArgumentException("Invalid reference status");
        return value;
    }
}
