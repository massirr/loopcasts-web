namespace LoopCastsWeb.Models;

public record Country(string Code, string Name, bool InEu);

/// <summary>
/// Fixed country list for the order form (a dropdown instead of free text:
/// the old orders sheet has "BE", "Belgium", "België", "belgie" for the same country).
/// The VAT rules need to know: Belgium? other EU? outside EU?
/// </summary>
public static class Countries
{
    public static readonly IReadOnlyList<Country> All =
    [
        new("BE", "Belgium", true),
        new("NL", "Netherlands", true),
        new("FR", "France", true),
        new("DE", "Germany", true),
        new("LU", "Luxembourg", true),
        new("AT", "Austria", true),
        new("BG", "Bulgaria", true),
        new("HR", "Croatia", true),
        new("CY", "Cyprus", true),
        new("CZ", "Czechia", true),
        new("DK", "Denmark", true),
        new("EE", "Estonia", true),
        new("FI", "Finland", true),
        new("GR", "Greece", true),
        new("HU", "Hungary", true),
        new("IE", "Ireland", true),
        new("IT", "Italy", true),
        new("LV", "Latvia", true),
        new("LT", "Lithuania", true),
        new("MT", "Malta", true),
        new("PL", "Poland", true),
        new("PT", "Portugal", true),
        new("RO", "Romania", true),
        new("SK", "Slovakia", true),
        new("SI", "Slovenia", true),
        new("ES", "Spain", true),
        new("SE", "Sweden", true),
        new("GB", "United Kingdom", false),
        new("CH", "Switzerland", false),
        new("NO", "Norway", false),
        new("US", "United States", false),
        new("CA", "Canada", false),
        new("XX", "Other country (outside the EU)", false),
    ];

    public static Country? Find(string? code) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
}
