using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Services;

/// <summary>
/// "Your access ends soon" reminders (#21). n8n asks which accounts are due, sends one e-mail per account and then
/// reports back; Account.RemindedFor remembers the end date a reminder was sent for, so nobody gets it twice.
/// After a renewal the end date changes, so the next period gets its own reminder again.
/// </summary>
public static class RenewalReminders
{
    public record Due(int AccountId, string Email, string Name, string Plan, DateOnly EndsOn, string EndsOnText, string RenewUrl);

    /// Active accounts that end between today and today + days, not reminded for this end date yet.
    /// Team colleagues are left out: only the buyer can renew a team licence. Lifetime accounts never end.
    public static async Task<List<Due>> DueAsync(AppDbContext db, DateOnly today, int days, string baseUrl)
    {
        var until = today.AddDays(days);
        var rows = await db.Accounts
            .Where(a => a.IsActive && a.EndsOn != null && a.EndsOn >= today && a.EndsOn <= until
                        && (a.RemindedFor == null || a.RemindedFor != a.EndsOn))
            .Select(a => new { a, Order = db.Orders.Where(o => o.Id == a.OrderId).Select(o => new { o.Email, o.PlanCode, o.Name }).FirstOrDefault() })
            .OrderBy(x => x.a.EndsOn)
            .ToListAsync();

        var due = new List<Due>();
        foreach (var x in rows)
        {
            var plan = Plans.Find(x.a.PlanCode ?? x.Order?.PlanCode ?? "");
            if (plan is null) continue;
            var colleague = x.Order is not null && !string.Equals(x.Order.Email, x.a.Email, StringComparison.OrdinalIgnoreCase);
            if (colleague) continue;

            var name = x.Order?.Name is { Length: > 0 } n ? n.Split(' ')[0] : await CustomerData.NameAsync(db, x.a.Email);
            var end = x.a.EndsOn!.Value;
            due.Add(new Due(x.a.Id, x.a.Email, name, plan.Name, end, end.ToString("dd/MM/yyyy"),
                $"{baseUrl.TrimEnd('/')}/Order?plan={plan.Code}"));
        }
        return due;
    }

    /// The reminder for this end date was sent. False = no such account. Calling it twice changes nothing.
    public static async Task<bool> MarkAsync(AppDbContext db, int accountId, DateOnly endsOn)
    {
        var account = await db.Accounts.FindAsync(accountId);
        if (account is null) return false;
        account.RemindedFor = endsOn;
        await db.SaveChangesAsync();
        return true;
    }
}
