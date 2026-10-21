using LoopCastsWeb.Services;

namespace LoopCastsWeb.Tests;

public class VatNumberTests
{
    [Theory]
    [InlineData(" be 0123.456.749 ", "BE", "BE0123456749")]   // spaces, dots, lowercase
    [InlineData("0123456749", "BE", "BE0123456749")]          // prefix added
    [InlineData("nl123456789b01", "NL", "NL123456789B01")]
    [InlineData("123456789", "GR", "EL123456789")]            // Greece: EL, not GR
    [InlineData("GR123456789", "GR", "EL123456789")]
    [InlineData("che-123.456.789", "CH", "CHE123456789")]     // outside the EU: only cleaned, no prefix
    public void Normalise_CleansAndAddsPrefix(string input, string country, string expected)
    {
        Assert.Equal(expected, VatNumber.Normalise(input, country));
    }

    // Built by hand: 01234567 mod 97 = 48 → 97 - 48 = 49. 10000000 mod 97 = 76 → 21.
    [Theory]
    [InlineData("0123456749")]
    [InlineData("1000000021")]
    public void BelgianChecksum_Valid(string digits) => Assert.True(VatNumber.BelgianChecksumOk(digits));

    [Theory]
    [InlineData("0123456748")]   // last digit typo
    [InlineData("0123456789")]   // the example on the form: right shape, wrong checksum
    [InlineData("0213456749")]   // two digits swapped
    public void BelgianChecksum_Broken(string digits) => Assert.False(VatNumber.BelgianChecksumOk(digits));

    [Theory]
    [InlineData("BE0123456749", "BE")]
    [InlineData("NL123456789B01", "NL")]
    [InlineData("DE123456789", "DE")]
    [InlineData("FRXX123456789", "FR")]
    [InlineData("ATU12345678", "AT")]
    [InlineData("EL123456789", "GR")]
    [InlineData("CHE123456789", "CH")]
    public void Error_NullForWellFormedNumbers(string number, string country) =>
        Assert.Null(VatNumber.Error(number, country));

    [Theory]
    [InlineData("BE012345678", "BE", "Enter a VAT number like BE0123456789.")] // 9 digits
    [InlineData("BE2123456749", "BE", "Enter a VAT number like BE0123456789.")] // must start with 0 or 1
    [InlineData("NL123456789", "NL", "Enter a VAT number like NL123456789B01.")]
    [InlineData("NLBE0123456749", "NL", "Enter a VAT number like NL123456789B01.")] // Belgian number, Dutch country
    public void Error_WrongFormat(string number, string country, string expected) =>
        Assert.Equal(expected, VatNumber.Error(number, country));

    [Fact]
    public void Error_TeachersInput_Refused() =>   // teacher feedback: "B@ddsfsd" was accepted as a VAT number
        Assert.Equal("Enter a VAT number like BE0123456789.", VatNumber.Error(VatNumber.Normalise("B@ddsfsd", "BE"), "BE"));

    [Fact]
    public void Error_BelgianChecksum() =>
        Assert.Contains("doesn't exist", VatNumber.Error("BE0123456789", "BE"));

    [Fact]
    public void Error_OutsideEu_LettersAndDigitsOnly() =>
        Assert.NotNull(VatNumber.Error("B@DDSFSD", "US"));

    [Fact]
    public void Error_TestNumbers_OnlyAgainstTheTestService()
    {
        Assert.Null(VatNumber.Error("NL100", "NL", allowTestNumbers: true));
        Assert.NotNull(VatNumber.Error("NL100", "NL"));
    }

    [Theory]
    [InlineData("ann@example.com", true)]
    [InlineData(" ann@example.com ", true)]
    [InlineData("B@ddsfsd", false)]   // one @ is not enough: [EmailAddress] accepts this
    [InlineData("ann@", false)]
    [InlineData("a b@example.com", false)]
    [InlineData(null, false)]
    public void Input_IsEmail(string? email, bool expected) => Assert.Equal(expected, Input.IsEmail(email));
}
