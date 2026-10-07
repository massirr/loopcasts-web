using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;

namespace LoopCastsWeb.Services;

/// <summary>
/// Real payments via Mollie (test mode with a test_ key). Replaces "check the bank account and mark paid by hand".
/// Two calls to the Mollie REST API (https://docs.mollie.com/reference/payments-api):
///   CreatePaymentAsync: POST /v2/payments → Mollie's checkout URL for the customer
///   SyncAsync:          GET  /v2/payments/{id} → if paid, PaymentService.MarkPaidAsync
/// The webhook body only contains an id. We NEVER trust it: SyncAsync asks Mollie itself for the status.
/// No API key configured (Mollie:ApiKey in user-secrets) → Enabled is false and /Pay uses the mock checkout.
/// </summary>
public class MollieService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;
    private readonly IConfiguration _config;
    private readonly ILogger<MollieService> _logger;

    public MollieService(HttpClient http, AppDbContext db, PaymentService payments, IConfiguration config, ILogger<MollieService> logger)
    {
        _http = http;
        _db = db;
        _payments = payments;
        _config = config;
        _logger = logger;
    }

    public bool Enabled => _http.DefaultRequestHeaders.Authorization is not null;

    /// What the customer pays: price incl. VAT, the same calculation as the invoice.
    public static decimal Total(Order order) =>
        VatCalculator.Calculate(order.Price, order.Country, order.IsCompany, order.VatNumber).Total;

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// Creates the payment at Mollie, remembers its id on the order, returns the checkout URL.
    public async Task<string> CreatePaymentAsync(Order order, string redirectUrl)
    {
        var body = new JsonObject
        {
            ["amount"] = new JsonObject { ["currency"] = "EUR", ["value"] = Money(Total(order)) },
            ["description"] = $"LoopCasts order #{order.Id}",
            ["redirectUrl"] = redirectUrl,
            ["metadata"] = new JsonObject { ["orderId"] = order.Id.ToString() },
        };
        // Mollie can't call localhost: only send a webhook when a public URL (tunnel / Azure) is configured
        if (_config["Mollie:WebhookBaseUrl"] is { Length: > 0 } baseUrl)
            body["webhookUrl"] = baseUrl.TrimEnd('/') + "/webhooks/mollie";

        using var response = await _http.PostAsJsonAsync("payments", body);
        response.EnsureSuccessStatusCode();
        var payment = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        order.MolliePaymentId = (string)payment["id"]!;
        await _db.SaveChangesAsync();
        return (string)payment["_links"]!["checkout"]!["href"]!;
    }

    /// Asks Mollie for the real status of this payment; marks the order paid only if everything matches.
    public async Task SyncAsync(string? paymentId)
    {
        if (!Enabled || paymentId is null || !Regex.IsMatch(paymentId, "^tr_[A-Za-z0-9]+$")) return;

        using var response = await _http.GetAsync($"payments/{paymentId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return;   // someone guessing ids: ignore
        response.EnsureSuccessStatusCode();                            // Mollie down → exception → webhook returns 500 → Mollie retries
        var payment = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        if ((string?)payment["status"] != "paid") return;
        if (!int.TryParse((string?)payment["metadata"]?["orderId"], out var orderId)) return;

        var order = await _db.Orders.FindAsync(orderId);
        if (order is null || order.MolliePaymentId != paymentId)
        {
            _logger.LogWarning("Mollie payment {PaymentId} does not belong to order {OrderId}", paymentId, orderId);
            return;
        }
        if ((string?)payment["amount"]?["value"] != Money(Total(order)))
        {
            _logger.LogWarning("Mollie payment {PaymentId}: amount differs from order {OrderId}", paymentId, orderId);
            return;
        }

        await _payments.MarkPaidAsync(orderId);   // idempotent: webhook + customer returning both arrive here
    }
}
