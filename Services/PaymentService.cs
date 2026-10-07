using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Services;

/// <summary>
/// Everything that must happen when an order is paid, in ONE place:
///   1. mark the order paid
///   2. create the invoice (unique number, VAT, billing details copied = snapshot)
///   3. create the account (with an end date)
/// All three are saved with ONE SaveChangesAsync → one database transaction:
/// either everything is saved, or nothing (no "paid but no invoice" half-states).
/// Called by: the mock checkout (/Pay), the admin fallback button, and later the Mollie webhook.
/// </summary>
public class PaymentService
{
    private readonly AppDbContext _db;
    private readonly MemberService _members;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(AppDbContext db, MemberService members, ILogger<PaymentService> logger)
    {
        _db = db;
        _members = members;
        _logger = logger;
    }

    /// Returns false when the order doesn't exist.
    public async Task<bool> MarkPaidAsync(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order is null) return false;

        if (!order.IsPaid)                      // paid twice (double click, webhook sent twice)? no 2nd invoice
            await RecordPaymentAsync(order);

        // 4. Login for the customer — AFTER the payment is safely saved: if this fails, the payment is not lost,
        //    and the next call (webhook retry) tries again because EnsureMemberAsync is idempotent.
        try
        {
            await _members.EnsureMemberAsync(order.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order {OrderId} is paid, but creating the customer login failed", order.Id);
        }
        return true;
    }

    private async Task RecordPaymentAsync(Order order)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // 1. Paid
        order.IsPaid = true;
        order.PaidAt = now;

        // 2. Invoice — every billing field is COPIED from the order (snapshot, see the ERD page)
        var plan = Plans.Find(order.PlanCode);
        var vat = VatCalculator.Calculate(order.Price, order.Country, order.IsCompany, order.VatNumber);
        var invoice = new Invoice
        {
            OrderId = order.Id,
            Number = await NextInvoiceNumberAsync(today.Year),
            IssuedOn = today,
            Description = plan?.Name ?? order.PlanCode,
            BillingName = order.Name,
            BillingCompany = order.Company,
            BillingVatNumber = order.VatNumber,
            BillingAddress = order.Address,
            BillingCountry = order.Country,
            AmountExclVat = order.Price,
            VatRate = vat.Rate,
            VatAmount = vat.VatAmount,
            Total = vat.Total,
            ReverseCharge = vat.ReverseCharge,
        };
        _db.Invoices.Add(invoice);

        // 3. Account with an end date (team licence: the buyer's seat; the 4 other seats come later)
        _db.Accounts.Add(new Account
        {
            OrderId = order.Id,
            Email = order.Email,
            StartsOn = today,
            EndsOn = plan?.AccessMonths is int months ? today.AddMonths(months) : null,
            IsActive = true,
        });

        await _db.SaveChangesAsync();           // one transaction for all three
        _logger.LogInformation("Order {OrderId} paid → invoice {Number}", order.Id, invoice.Number);
    }

    /// "2026-0001", "2026-0002", ... The database has a UNIQUE index on Number,
    /// so two invoices can never get the same number (no more 2026-0029 twice).
    private async Task<string> NextInvoiceNumberAsync(int year)
    {
        var prefix = $"{year}-";
        var last = await _db.Invoices
            .Where(i => i.Number.StartsWith(prefix))
            .OrderByDescending(i => i.Number)
            .Select(i => i.Number)
            .FirstOrDefaultAsync();

        var next = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return $"{prefix}{next:0000}";
    }
}
