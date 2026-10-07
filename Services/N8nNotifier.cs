using System.Text;
using System.Text.Json.Nodes;
using LoopCastsWeb.Models;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace LoopCastsWeb.Services;

/// <summary>
/// Sends events to ONE n8n webhook (config N8n:WebhookUrl); the n8n workflow sends the e-mails (docs/n8n.md).
/// Replaces "make the invoice in Word and mail it on Friday" (handbook ch. 2).
///   invoice.created → invoice e-mail with a link to the invoice (+ set-password link for a new customer)
///   email           → any e-mail from the login system, e.g. "Forgot password" (this class is the IEmailSender)
/// n8n down or not configured? Logged, never thrown: a payment must never fail because an e-mail could not be sent.
/// </summary>
public class N8nNotifier : IEmailSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<N8nNotifier> _logger;

    public N8nNotifier(HttpClient http, IConfiguration config, ILogger<N8nNotifier> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    // Public address of the site, for links in e-mails (no HttpContext in a webhook or background job)
    private string BaseUrl => (_config["App:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:5180").TrimEnd('/');

    public Task InvoiceCreatedAsync(Invoice invoice, string email, string? passwordSetupCode) => SendAsync(new JsonObject
    {
        ["event"] = "invoice.created",
        ["to"] = email,
        ["name"] = invoice.BillingName,
        ["invoiceNumber"] = invoice.Number,
        ["description"] = invoice.Description,
        ["total"] = invoice.Total,
        ["invoiceUrl"] = $"{BaseUrl}/Invoice?number={Uri.EscapeDataString(invoice.Number)}",
        ["setPasswordUrl"] = passwordSetupCode is null ? null
            : $"{BaseUrl}/Identity/Account/ResetPassword?code={passwordSetupCode}",
    });

    public Task SendEmailAsync(string email, string subject, string htmlMessage) => SendAsync(new JsonObject
    {
        ["event"] = "email",
        ["to"] = email,
        ["subject"] = subject,
        ["html"] = htmlMessage,
    });

    private async Task SendAsync(JsonObject body)
    {
        if (_config["N8n:WebhookUrl"] is not { Length: > 0 } url) return;   // not configured (e.g. in class demo)

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                // A plain string body (with Content-Length) instead of JsonContent (chunked): works with every receiver
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            if (_config["N8n:Secret"] is { Length: > 0 } secret)
                request.Headers.Add("X-LoopCasts-Secret", secret);   // n8n webhook "Header Auth" checks this
            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)   // HttpRequestException, timeout, ...
        {
            _logger.LogWarning(ex, "n8n event {Event} for {To} could not be sent", body["event"], body["to"]);
        }
    }
}
