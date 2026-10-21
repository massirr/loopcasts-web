using System.Net;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoopCastsWeb.Tests;

/// VIES never really called: FakeMollie answers like VIES did when we tried the official test service.
public class ViesClientTests
{
    private const string TestService = "https://ec.europa.eu/taxation_customs/vies/rest-api/check-vat-test-service";

    private static ViesClient Client(HttpMessageHandler handler, string url = TestService) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(url) }, NullLogger<ViesClient>.Instance);

    [Fact]
    public async Task Check_Valid_ReturnsNameAndSendsCountrySeparately()
    {
        var fake = new FakeMollie(HttpStatusCode.OK, """{"countryCode":"NL","vatNumber":"100","valid":true,"name":"John Doe","address":"123 Main St"}""");

        var answer = await Client(fake).CheckAsync("NL100");

        Assert.Equal(new ViesClient.Answer(VatCheck.Valid, "John Doe"), answer);
        Assert.Equal("/taxation_customs/vies/rest-api/check-vat-test-service", fake.Requests.Single().Path);
        Assert.Contains("\"countryCode\":\"NL\"", fake.Requests.Single().Body);
        Assert.Contains("\"vatNumber\":\"100\"", fake.Requests.Single().Body);
    }

    [Fact]
    public async Task Check_ValidWithoutSharedName_NameIsNull()
    {
        var fake = new FakeMollie(HttpStatusCode.OK, """{"valid":true,"name":"---"}""");
        Assert.Equal(new ViesClient.Answer(VatCheck.Valid), await Client(fake).CheckAsync("DE123456789"));
    }

    [Theory]
    [InlineData("""{"countryCode":"NL","vatNumber":"200","valid":false,"name":"---","address":"---"}""")]
    [InlineData("""{"actionSucceed":false,"errorWrappers":[{"error":"INVALID_INPUT"}]}""")]
    public async Task Check_Invalid(string json)
    {
        Assert.Equal(VatCheck.Invalid, (await Client(new FakeMollie(HttpStatusCode.OK, json)).CheckAsync("NL200")).Result);
    }

    // VIES down: it says so with HTTP 200 (300 = SERVICE_UNAVAILABLE, 301 = MS_UNAVAILABLE on the test service),
    // or the server fails, or the answer isn't JSON
    [Theory]
    [InlineData(HttpStatusCode.OK, """{"actionSucceed":false,"errorWrappers":[{"error":"SERVICE_UNAVAILABLE"}]}""")]
    [InlineData(HttpStatusCode.OK, """{"actionSucceed":false,"errorWrappers":[{"error":"MS_UNAVAILABLE"}]}""")]
    [InlineData(HttpStatusCode.InternalServerError, """{"actionSucceed":false}""")]
    [InlineData(HttpStatusCode.OK, "<html>maintenance</html>")]
    public async Task Check_Down_NotVerified(HttpStatusCode status, string json)
    {
        Assert.Equal(VatCheck.NotVerified, (await Client(new FakeMollie(status, json)).CheckAsync("NL301")).Result);
    }

    [Fact]
    public async Task Check_Timeout_NotVerified()
    {
        Assert.Equal(VatCheck.NotVerified, (await Client(new Throws(new TaskCanceledException())).CheckAsync("NL123456789B01")).Result);
    }

    [Fact]
    public async Task Check_NetworkDown_NotVerified()
    {
        Assert.Equal(VatCheck.NotVerified, (await Client(new Throws(new HttpRequestException("no route"))).CheckAsync("NL123456789B01")).Result);
    }

    [Fact]
    public void IsTestService_OnlyForTheTestEndpoint()
    {
        Assert.True(Client(new Throws(new Exception())).IsTestService);
        Assert.False(Client(new Throws(new Exception()), "https://ec.europa.eu/taxation_customs/vies/rest-api/check-vat-number").IsTestService);
    }

    // HttpClient throws TaskCanceledException when its Timeout passes; this fake does it straight away
    private class Throws(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw ex;
    }
}
