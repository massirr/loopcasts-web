using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

// Checkout. With a Stripe key: redirect to Stripe Checkout, Stripe sends the customer back here (and calls the webhook).
// Without a key: MOCK checkout, the button marks the order paid directly.
public class PayModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;
    private readonly StripeService _stripe;

    public PayModel(AppDbContext db, PaymentService payments, StripeService stripe)
    {
        _db = db;
        _payments = payments;
        _stripe = stripe;
    }

    public Order? Order { get; set; }
    public Plan? Plan { get; set; }
    public decimal Total { get; set; }
    public bool UsesStripe => _stripe.Enabled;
    public string? Error { get; set; }
    public bool NotCompleted { get; set; }   // came back from Stripe without paying ("back" on Stripe's page)

    public async Task<IActionResult> OnGetAsync(int id, string? key)
    {
        Order = await _db.Orders.FindAsync(id);
        if (Order is null || !Order.Opens(key)) return NotFound();

        // Back from Stripe? Ask Stripe for the status (works without a webhook, e.g. on localhost)
        if (!Order.IsPaid && Order.PaymentId is not null && _stripe.Enabled)
        {
            try { await _stripe.SyncAsync(Order.PaymentId); }
            catch (HttpRequestException) { Error = "We couldn't check your payment with Stripe right now. Refresh this page in a minute."; }
            NotCompleted = !Order.IsPaid && Error is null;
        }

        if (Order.IsPaid) return RedirectToPage("/OrderThanks", new { id, key });   // nothing left to pay

        Plan = Plans.Find(Order.PlanCode);
        Total = StripeService.Total(Order);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string? key)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null || !order.Opens(key)) return NotFound();
        if (order.IsPaid) return RedirectToPage("/OrderThanks", new { id, key });

        if (!_stripe.Enabled)
        {
            await _payments.MarkPaidAsync(id);   // mock
            return RedirectToPage("/OrderThanks", new { id, key });
        }

        try
        {
            var back = Url.Page("/Pay", null, new { id, key }, Request.Scheme)!;
            return Redirect(await _stripe.CreateCheckoutAsync(order, back));
        }
        catch (HttpRequestException)
        {
            Order = order;
            Plan = Plans.Find(order.PlanCode);
            Total = StripeService.Total(order);
            Error = "The payment provider is not reachable right now. Please try again in a minute.";
            return Page();
        }
    }
}
