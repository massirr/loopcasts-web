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
    /// The plan comes from the order, or from the account itself for members imported from the old site (no order).
    public static async Task<List<Subscription>> SubscriptionsAsync(AppDbContext db, string email)
    {
        var e = email.ToLower();
        var rows = await db.Accounts
            .Where(a => a.Email.ToLower() == e)
            .Select(a => new { a, PlanCode = a.PlanCode ?? db.Orders.Where(o => o.Id == a.OrderId).Select(o => o.PlanCode).FirstOrDefault() ?? "" })
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

    /// The name for the account button: the name on the newest order, else the e-mail before the @ ("ann.peeters" → "Ann").
    /// Identity only stores the e-mail; staff and colleagues on a team seat never ordered themselves.
    public static async Task<string> NameAsync(AppDbContext db, string email)
    {
        var e = email.ToLower();
        var name = await db.Orders.Where(o => o.Email.ToLower() == e)
            .OrderByDescending(o => o.Id).Select(o => o.Name).FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
        var part = email.Split('@')[0].Split('.', '_', '-', '+')[0];
        return part.Length == 0 ? email : char.ToUpperInvariant(part[0]) + part[1..];
    }

    /// After a confirmed login e-mail change: orders and seats follow the customer, so /My keeps showing them.
    /// Invoices keep their Billing* snapshot (the e-mail is not on the invoice).
    public static async Task MoveToEmailAsync(AppDbContext db, string oldEmail, string newEmail)
    {
        var e = oldEmail.ToLower();
        await db.Orders.Where(o => o.Email.ToLower() == e).ExecuteUpdateAsync(s => s.SetProperty(o => o.Email, newEmail));
        await db.Accounts.Where(a => a.Email.ToLower() == e).ExecuteUpdateAsync(s => s.SetProperty(a => a.Email, newEmail));
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
