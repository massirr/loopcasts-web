using LoopCastsWeb.Services;

namespace LoopCastsWeb.Tests;

public class VatCalculatorTests
{
    [Theory]
    [InlineData("BE", false, null)]          // Belgium, private
    [InlineData("BE", true, "BE0123456789")] // Belgium, company: still 21%
    [InlineData("NL", false, null)]          // other EU, private
    [InlineData("NL", true, null)]           // other EU, company WITHOUT VAT number
    [InlineData("NL", true, "  ")]           // blank VAT number counts as none
    [InlineData(null, false, null)]          // unknown country (old free-text orders)
    [InlineData("ZZ", false, null)]
    public void Calculate_Charges21Percent(string? country, bool isCompany, string? vatNumber)
    {
        var r = VatCalculator.Calculate(149m, country, isCompany, vatNumber);

        Assert.Equal(0.21m, r.Rate);
        Assert.Equal(31.29m, r.VatAmount);
        Assert.Equal(180.29m, r.Total);
        Assert.False(r.ReverseCharge);
    }

    [Fact]
    public void Calculate_EuCompanyWithVatNumber_ReverseCharge()
    {
        var r = VatCalculator.Calculate(599m, "NL", true, "NL123456789B01");

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
        Assert.True(VatCalculator.Calculate(10m, " nl ", true, "NL1").ReverseCharge);
    }

    [Fact]
    public void Calculate_RoundsMidpointAwayFromZero()
    {
        // 0.50 * 21% = 0.105 → 0.11 (banker's rounding would give 0.10)
        Assert.Equal(0.11m, VatCalculator.Calculate(0.50m, "BE", false, null).VatAmount);
    }
}
