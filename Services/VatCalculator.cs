using LoopCastsWeb.Models;

namespace LoopCastsWeb.Services;

/// <summary>
/// LoopCasts VAT rules from the handbook (chapter 1), so nobody needs a phone calculator:
///  - Belgium: 21%
///  - other EU, private person: 21%
///  - other EU, company WITH a VAT number: 0% + "VAT reverse charged" on the invoice
///  - outside the EU: 0%
/// </summary>
public static class VatCalculator
{
    public record Result(decimal Rate, decimal VatAmount, decimal Total, bool ReverseCharge);

    public static Result Calculate(decimal amountExclVat, string? countryCode, bool isCompany, string? vatNumber)
    {
        var country = Countries.Find(countryCode);
        decimal rate;
        var reverseCharge = false;

        if (country is null || country.Code == "BE")
        {
            rate = 0.21m;                       // unknown country (old free-text orders): charge Belgian VAT, the safe choice
        }
        else if (country.InEu)
        {
            if (isCompany && !string.IsNullOrWhiteSpace(vatNumber))
            {
                rate = 0m;
                reverseCharge = true;
            }
            else
            {
                rate = 0.21m;
            }
        }
        else
        {
            rate = 0m;                          // outside the EU
        }

        var vat = Math.Round(amountExclVat * rate, 2, MidpointRounding.AwayFromZero);
        return new Result(rate, vat, amountExclVat + vat, reverseCharge);
    }
}
