using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;

namespace LoopCastsWeb.Services;

/// <summary>
/// Real payments via Stripe Checkout (test mode with an sk_test_ key). Replaces "check the bank account and mark paid
/// by hand". Replaced Mollie (#53, ADR 0003). Two calls to the Stripe REST API (https://docs.stripe.com/api/checkout/sessions):
///   CreateCheckoutAsync: POST /v1/checkout/sessions → Stripe's checkout URL for the customer
///   SyncAsync:           GET  /v1/checkout/sessions/{id} → if paid, PaymentService.MarkPaidAsync
/// The webhook body is NEVER trusted: we only take the session id from it and ask Stripe ourselves.
/// No key configured (Stripe:SecretKey in user-secrets) → Enabled is false and /Pay uses the mock checkout.
/// </summary>
public class StripeService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;
    private readonly ILogger<StripeService> _logger;

    public StripeService(HttpClient http, AppDbContext db, PaymentService payments, ILogger<StripeService> logger)
    {
        _http = http;
        _db = db;
        _payments = payments;
        _logger = logger;
    }

    public bool Enabled => _http.DefaultRequestHeaders.Authorization is not null;

    /// What the customer pays: price incl. VAT, the same calculation as the invoice.
    public static decimal Total(Order order) =>
        VatCalculator.Calculate(order.Price, order.Country, order.IsCompany, order.VatCheckResult).Total;

    // Stripe counts in cents: €180.29 → 18029
    private static long Cents(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    /// Creates the checkout session at Stripe, remembers its id on the order, returns the checkout URL.
    /// returnUrl is used for both "paid" and "cancelled": /Pay re-checks the status either way.
    public async Task<string> CreateCheckoutAsync(Order order, string returnUrl)
    {
        // Still an open session for this order (double click, second tab)? Reuse it: a new one would replace
        // PaymentId, and paying the old one would then be ignored (charged, but no invoice).
        if (IsSessionId(order.PaymentId))
        {
            using var existing = await _http.GetAsync($"checkout/sessions/{order.PaymentId}");
            if (existing.IsSuccessStatusCode)
            {
                var old = JsonNode.Parse(await existing.Content.ReadAsStringAsync())!;
                if ((string?)old["status"] == "open" && (string?)old["url"] is { } openUrl) return openUrl;
            }
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["success_url"] = returnUrl,
            ["cancel_url"] = returnUrl,
            ["customer_email"] = order.Email,
            ["client_reference_id"] = order.Id.ToString(),
            ["metadata[orderId]"] = order.Id.ToString(),
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = "eur",
            ["line_items[0][price_data][unit_amount]"] = Cents(Total(order)).ToString(),
            ["line_items[0][price_data][product_data][name]"] = $"LoopCasts order #{order.Id}",
        });

        using var response = await _http.PostAsync("checkout/sessions", form);
        response.EnsureSuccessStatusCode();
        var session = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        order.PaymentId = (string)session["id"]!;
        await _db.SaveChangesAsync();
        return (string)session["url"]!;
    }

    /// Asks Stripe for the real status of this session; marks the order paid only if everything matches.
    public async Task SyncAsync(string? sessionId)
    {
        if (!Enabled || !IsSessionId(sessionId)) return;

        using var response = await _http.GetAsync($"checkout/sessions/{sessionId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return;   // someone guessing ids: ignore
        response.EnsureSuccessStatusCode();                            // Stripe down → exception → webhook 500 → Stripe retries
        var session = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        if ((string?)session["payment_status"] != "paid") return;
        if (!int.TryParse((string?)session["metadata"]?["orderId"], out var orderId)) return;

        var order = await _db.Orders.FindAsync(orderId);
        if (order is null || order.PaymentId != sessionId)
        {
            _logger.LogWarning("Stripe session {SessionId} does not belong to order {OrderId}", sessionId, orderId);
            return;
        }
        if ((long?)session["amount_total"] != Cents(Total(order)) || (string?)session["currency"] != "eur")
        {
            _logger.LogWarning("Stripe session {SessionId}: amount differs from order {OrderId}", sessionId, orderId);
            return;
        }

        await _payments.MarkPaidAsync(orderId);   // idempotent: webhook + customer returning both arrive here
    }

    private static bool IsSessionId(string? id) => id is not null && Regex.IsMatch(id, "^cs_(test|live)_[A-Za-z0-9]+$");

    /// The session id from a webhook event ({"data":{"object":{"id":"cs_..."}}}), or null. Nothing else is used.
    public static string? SessionIdFromEvent(string json)
    {
        try { return (string?)JsonNode.Parse(json)?["data"]?["object"]?["id"]; }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException) { return null; }
    }
}
