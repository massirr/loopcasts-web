using System.Text.RegularExpressions;

namespace LoopCastsWeb.Services;

/// <summary>
/// Is this VAT number at least written correctly? (#33) Pure code, no network: the VIES register check is ViesClient.
/// Formats from the European Commission's VIES FAQ ("VAT number structure"); Belgium also gets its mod-97 checksum.
/// </summary>
public static class VatNumber
{
    // Part after the country prefix, keyed by OUR country code (Greece is GR in the list, EL in VAT numbers)
    private static readonly Dictionary<string, (string Pattern, string Example)> Eu = new()
    {
        ["AT"] = (@"U\d{8}", "ATU12345678"),
        ["BE"] = (@"[01]\d{9}", "BE0123456789"),
        ["BG"] = (@"\d{9,10}", "BG123456789"),
        ["CY"] = (@"\d{8}[A-Z]", "CY12345678X"),
        ["CZ"] = (@"\d{8,10}", "CZ12345678"),
        ["DE"] = (@"\d{9}", "DE123456789"),
        ["DK"] = (@"\d{8}", "DK12345678"),
        ["EE"] = (@"\d{9}", "EE123456789"),
        ["GR"] = (@"\d{9}", "EL123456789"),
        ["ES"] = (@"[A-Z0-9]\d{7}[A-Z0-9]", "ESX1234567X"),
        ["FI"] = (@"\d{8}", "FI12345678"),
        ["FR"] = (@"[A-Z0-9]{2}\d{9}", "FRXX123456789"),
        ["HR"] = (@"\d{11}", "HR12345678901"),
        ["HU"] = (@"\d{8}", "HU12345678"),
        ["IE"] = (@"\d{7}[A-W][A-I]?|\d[A-Z+*]\d{5}[A-W]", "IE1234567WA"),
        ["IT"] = (@"\d{11}", "IT12345678901"),
        ["LT"] = (@"\d{9}|\d{12}", "LT123456789"),
        ["LU"] = (@"\d{8}", "LU12345678"),
        ["LV"] = (@"\d{11}", "LV12345678901"),
        ["MT"] = (@"\d{8}", "MT12345678"),
        ["NL"] = (@"\d{9}B\d{2}", "NL123456789B01"),
        ["PL"] = (@"\d{10}", "PL1234567890"),
        ["PT"] = (@"\d{9}", "PT123456789"),
        ["RO"] = (@"[1-9]\d{1,9}", "RO1234567890"),
        ["SE"] = (@"\d{10}01", "SE123456789001"),
        ["SI"] = (@"\d{8}", "SI12345678"),
        ["SK"] = (@"\d{10}", "SK1234567890"),
    };

    /// The two letters VIES and invoices use: our country code, except Greece.
    public static string Prefix(string country) => country == "GR" ? "EL" : country;

    /// " be 0123.456.789 " → "BE0123456789". EU: country prefix added when missing. Outside the EU: only cleaned.
    public static string Normalise(string input, string country)
    {
        var s = Regex.Replace(input.ToUpperInvariant(), @"[\s.\-]", "");
        if (!Eu.ContainsKey(country)) return s;

        var prefix = Prefix(country);
        if (s.StartsWith(prefix)) s = s[prefix.Length..];
        else if (s.StartsWith(country)) s = s[country.Length..];   // Greece: "GR…" typed, the number is "EL…"
        return prefix + s;
    }

    /// Null when the (normalised) number is fine, else the message for under the field.
    /// allowTestNumbers: the VIES test service only knows the numbers 100/200/300/301, so let those through there.
    public static string? Error(string normalised, string country, bool allowTestNumbers = false)
    {
        if (!Eu.TryGetValue(country, out var rule))
            return Regex.IsMatch(normalised, "^[A-Z0-9]{4,20}$") ? null : "Enter your company's VAT or tax number (letters and digits only).";

        var body = normalised[Prefix(country).Length..];
        if (allowTestNumbers && Regex.IsMatch(body, @"^\d{3}$")) return null;
        if (!Regex.IsMatch(body, $"^(?:{rule.Pattern})$")) return $"Enter a VAT number like {rule.Example}.";
        if (country == "BE" && !BelgianChecksumOk(body)) return "This Belgian VAT number doesn't exist: check the digits for a typo.";
        return null;
    }

    /// Belgium: the last 2 digits are 97 minus (the first 8 digits mod 97). Catches almost every typo.
    public static bool BelgianChecksumOk(string tenDigits) =>
        97 - long.Parse(tenDigits[..8]) % 97 == int.Parse(tenDigits[8..]);
}
