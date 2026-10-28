using LoopCastsWeb.Models;
using LoopCastsWeb.Services;

namespace LoopCastsWeb.Tests;

public class VatCalculatorTests
{
    [Theory]
    [InlineData("BE", false, null)]                    // Belgium, private
    [InlineData("BE", true, VatCheck.Valid)]           // Belgium, company: still 21%
    [InlineData("NL", false, null)]                    // other EU, private
    [InlineData("NL", true, null)]                     // other EU, company, number never checked
    [InlineData("NL", true, VatCheck.NotVerified)]     // #33: VIES down → no proof → never 0%
    [InlineData("NL", true, VatCheck.Invalid)]
    [InlineData("NL", false, VatCheck.Valid)]          // a valid check doesn't turn a private person into a company
    [InlineData(null, false, null)]                    // unknown country (old free-text orders)
    [InlineData("ZZ", true, VatCheck.Valid)]
    public void Calculate_Charges21Percent(string? country, bool isCompany, VatCheck? check)
    {
        var r = VatCalculator.Calculate(149m, country, isCompany, check);

        Assert.Equal(0.21m, r.Rate);
        Assert.Equal(31.29m, r.VatAmount);
        Assert.Equal(180.29m, r.Total);
        Assert.False(r.ReverseCharge);
    }

    [Fact]
    public void Calculate_EuCompanyWithValidVatCheck_ReverseCharge()
    {
        var r = VatCalculator.Calculate(599m, "NL", true, VatCheck.Valid);

        Assert.Equal(0m, r.Rate);
        Assert.Equal(0m, r.VatAmount);
        Assert.Equal(599m, r.Total);
        Assert.True(r.ReverseCharge);
    }

    [Theory]
    [InlineData("US", false)]
    [InlineData("GB", true)]
    [InlineData("XX", false)]
    public void Calculate_OutsideEu_ZeroWithoutReverseCharge(string country, bool isCompany)
    {
        var r = VatCalculator.Calculate(15m, country, isCompany, null);

        Assert.Equal(0m, r.Rate);
        Assert.Equal(15m, r.Total);
        Assert.False(r.ReverseCharge);
    }

    [Fact]
    public void Calculate_CountryCodeIsCaseAndSpaceInsensitive()
    {
        Assert.True(VatCalculator.Calculate(10m, " nl ", true, VatCheck.Valid).ReverseCharge);
    }

    [Fact]
    public void Calculate_RoundsMidpointAwayFromZero()
    {
        // 0.50 * 21% = 0.105 → 0.11 (banker's rounding would give 0.10)
        Assert.Equal(0.11m, VatCalculator.Calculate(0.50m, "BE", false, null).VatAmount);
    }
}
