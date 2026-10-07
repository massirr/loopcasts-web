using LoopCastsWeb.Models;

namespace LoopCastsWeb.Tests;

public class OrderTests
{
    [Theory]
    [InlineData("ABC123", "ABC123", true)]
    [InlineData("ABC123", "abc123", false)]
    [InlineData("ABC123", null, false)]
    [InlineData("", "", false)]      // old order without a key never opens
    public void Opens_OnlyWithTheExactKey(string accessKey, string? key, bool expected)
    {
        Assert.Equal(expected, new Order { AccessKey = accessKey }.Opens(key));
    }
}
