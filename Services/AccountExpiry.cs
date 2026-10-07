using LoopCastsWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Services;

/// <summary>
/// Switches off every active account whose end date has passed.
/// Replaces "When a subscription ends, I should set the account to inactive" (handbook ch. 3),
/// the step that was forgotten for 123 accounts.
/// </summary>
public static class AccountExpiry
{
    public static async Task<int> RunAsync(AppDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expired = await db.Accounts
            .Where(a => a.IsActive && a.EndsOn != null && a.EndsOn < today)
            .ToListAsync();

        foreach (var account in expired)
            account.IsActive = false;

        await db.SaveChangesAsync();
        return expired.Count;
    }
}

/// <summary>
/// Runs AccountExpiry once at startup and then every 24 hours, without anyone clicking anything.
/// A BackgroundService lives as long as the website runs.
/// </summary>
public class ExpireAccountsService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ExpireAccountsService> _logger;

    public ExpireAccountsService(IServiceScopeFactory scopes, ILogger<ExpireAccountsService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                // A background service has no request, so it creates its own scope to get a DbContext
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var count = await AccountExpiry.RunAsync(db);
                _logger.LogInformation("Account expiry check: {Count} account(s) deactivated", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account expiry check failed");   // log and try again next time, don't crash the site
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
