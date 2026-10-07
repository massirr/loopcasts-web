using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Services;

/// <summary>
/// "Which rows belong to this customer?" in ONE place, so /My and /Invoice can never show someone else's data.
/// E-mails are compared case-insensitively (Identity does that too: "Lotte@x.be" = "lotte@x.be").
/// </summary>
public static class CustomerData
{
    public record Subscription(Account Account, string PlanName);

    /// Accounts by the account e-mail (a team member sees their own seat, not the buyer's).
    public static async Task<List<Subscription>> SubscriptionsAsync(AppDbContext db, string email)
    {
        var e = email.ToLower();
        var rows = await db.Accounts
            .Where(a => a.Email.ToLower() == e)
            .Join(db.Orders, a => a.OrderId, o => o.Id, (a, o) => new { a, o.PlanCode })
            .OrderByDescending(x => x.a.StartsOn)
            .ToListAsync();
        return rows.Select(x => new Subscription(x.a, Plans.Find(x.PlanCode)?.Name ?? x.PlanCode)).ToList();
    }

    /// Invoices by the e-mail of the order that was paid (the buyer).
    public static Task<List<Invoice>> InvoicesAsync(AppDbContext db, string email)
    {
        var e = email.ToLower();
        return db.Invoices
            .Where(i => db.Orders.Any(o => o.Id == i.OrderId && o.Email.ToLower() == e))
            .OrderByDescending(i => i.Number)
            .ToListAsync();
    }

    /// One invoice, only if it belongs to this e-mail (or the caller is staff).
    public static async Task<Invoice?> InvoiceAsync(AppDbContext db, string number, string? email, bool isAdmin)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Number == number);
        if (invoice is null || isAdmin) return invoice;

        var e = email?.ToLower();
        var owns = e is not null && await db.Orders.AnyAsync(o => o.Id == invoice.OrderId && o.Email.ToLower() == e);
        return owns ? invoice : null;
    }
}
