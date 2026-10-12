using System.Security.Cryptography;
using System.Text;
using LoopCastsWeb.Data;
using LoopCastsWeb.Services;

namespace LoopCastsWeb.Api;

/// <summary>
/// JSON endpoints for n8n (#21, docs/n8n.md). Only with the shared secret in the X-LoopCasts-Secret header
/// (same value as N8n:Secret); no secret configured = closed. Handlers are plain static methods so tests can call them.
/// </summary>
public static class AutomationApi
{
    public const string SecretHeader = "X-LoopCasts-Secret";

    public static void MapAutomationApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/automation");
        api.MapGet("/expiring", Expiring);
        api.MapPost("/reminded", Reminded);
    }

    public static bool Authorized(HttpRequest request, IConfiguration config)
    {
        if (config["N8n:Secret"] is not { Length: > 0 } secret) return false;
        var sent = request.Headers[SecretHeader].ToString();
        // constant-time compare: the answer time doesn't tell how much of the secret was right
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sent), Encoding.UTF8.GetBytes(secret));
    }

    /// GET /api/automation/expiring?days=14 → accounts to remind (see RenewalReminders.DueAsync)
    public static async Task<IResult> Expiring(HttpRequest request, IConfiguration config, AppDbContext db, int? days)
    {
        if (!Authorized(request, config)) return Results.Unauthorized();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var baseUrl = config["App:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:5180";
        return Results.Ok(await RenewalReminders.DueAsync(db, today, Math.Clamp(days ?? 14, 1, 60), baseUrl));
    }

    public record RemindedRequest(int AccountId, DateOnly EndsOn);

    /// POST /api/automation/reminded {"accountId":5,"endsOn":"2026-10-20"} → 204, or 404 for an unknown account
    public static async Task<IResult> Reminded(HttpRequest request, IConfiguration config, AppDbContext db, RemindedRequest body)
    {
        if (!Authorized(request, config)) return Results.Unauthorized();
        return await RenewalReminders.MarkAsync(db, body.AccountId, body.EndsOn) ? Results.NoContent() : Results.NotFound();
    }
}
