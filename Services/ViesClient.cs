using System.Text.Json;
using System.Text.Json.Nodes;
using LoopCastsWeb.Models;

namespace LoopCastsWeb.Services;

/// <summary>
/// Asks the EU VIES register whether a VAT number exists (#33), so reverse charge (0%) is only given with proof.
/// Official REST contract: https://ec.europa.eu/assets/taxud/vow-information/swagger_publicVAT.yaml
///   POST {countryCode, vatNumber} → {valid, name, address, ...}
/// VIES reports its own errors as HTTP 200 + {actionSucceed:false, errorWrappers:[{error:"MS_UNAVAILABLE"}]}.
/// Vies:BaseUrl is the endpoint: check-vat-number (Production) or check-vat-test-service (Development).
/// Never throws: anything unexpected is NotVerified, and the order then pays 21%.
/// </summary>
public class ViesClient
{
    public record Answer(VatCheck Result, string? Name = null);

    private readonly HttpClient _http;
    private readonly ILogger<ViesClient> _logger;

    public ViesClient(HttpClient http, ILogger<ViesClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// The test service only knows the numbers 100 (valid), 200 (invalid), 300/301 (down).
    public bool IsTestService => _http.BaseAddress?.AbsolutePath.EndsWith("test-service") == true;

    /// normalised = "NL123456789B01" (prefix included, see VatNumber.Normalise).
    public async Task<Answer> CheckAsync(string normalised)
    {
        var body = new { countryCode = normalised[..2], vatNumber = normalised[2..] };
        try
        {
            using var response = await _http.PostAsJsonAsync("", body);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("VIES answered HTTP {Status} for a {Country} number", (int)response.StatusCode, body.countryCode);
                return new(VatCheck.NotVerified);
            }

            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            if (json?["valid"]?.GetValueKind() is JsonValueKind.True)
                return new(VatCheck.Valid, Name((string?)json["name"]));
            if (json?["valid"]?.GetValueKind() is JsonValueKind.False)
                return new(VatCheck.Invalid);

            var error = (string?)json?["errorWrappers"]?[0]?["error"];
            if (error == "INVALID_INPUT") return new(VatCheck.Invalid);   // VIES says the number can't even exist
            _logger.LogWarning("VIES could not check a {Country} number: {Error}", body.countryCode, error);
            return new(VatCheck.NotVerified);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // TaskCanceledException = the 5 s timeout
            _logger.LogWarning(ex, "VIES did not answer for a {Country} number", body.countryCode);
            return new(VatCheck.NotVerified);
        }
    }

    // Some countries don't share the name: VIES then sends "---"
    private static string? Name(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim() == "---" ? null : name.Trim();
}
