using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

// Checkout. With a Mollie API key: redirect to Mollie, Mollie sends the customer back here (and calls the webhook).
// Without a key: MOCK checkout, the button marks the order paid directly.
public class PayModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;
    private readonly MollieService _mollie;

    public PayModel(AppDbContext db, PaymentService payments, MollieService mollie)
    {
        _db = db;
        _payments = payments;
        _mollie = mollie;
    }

    public Order? Order { get; set; }
    public Plan? Plan { get; set; }
    public decimal Total { get; set; }
    public bool UsesMollie => _mollie.Enabled;
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, string? key)
    {
        Order = await _db.Orders.FindAsync(id);
        if (Order is null || !Order.Opens(key)) return NotFound();

        // Back from Mollie? Ask Mollie for the status (works without a public webhook URL, e.g. on localhost)
        if (!Order.IsPaid && Order.MolliePaymentId is not null && _mollie.Enabled)
            await _mollie.SyncAsync(Order.MolliePaymentId);

        if (Order.IsPaid) return RedirectToPage("/OrderThanks", new { id, key });   // nothing left to pay

        Plan = Plans.Find(Order.PlanCode);
        Total = MollieService.Total(Order);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string? key)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null || !order.Opens(key)) return NotFound();
        if (order.IsPaid) return RedirectToPage("/OrderThanks", new { id, key });

        if (!_mollie.Enabled)
        {
            await _payments.MarkPaidAsync(id);   // mock
            return RedirectToPage("/OrderThanks", new { id, key });
        }

        try
        {
            var back = Url.Page("/Pay", null, new { id, key }, Request.Scheme)!;
            return Redirect(await _mollie.CreatePaymentAsync(order, back));
        }
        catch (HttpRequestException)
        {
            Order = order;
            Plan = Plans.Find(order.PlanCode);
            Total = MollieService.Total(order);
            Error = "The payment provider is not reachable right now. Please try again in a minute.";
            return Page();
        }
    }
}
