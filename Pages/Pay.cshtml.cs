using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

// MOCK checkout. In the real version this page is Mollie's payment screen,
// and Mollie calls our webhook instead of this OnPost.
public class PayModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;

    public PayModel(AppDbContext db, PaymentService payments)
    {
        _db = db;
        _payments = payments;
    }

    public Order? Order { get; set; }
    public Plan? Plan { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, string? key)
    {
        Order = await _db.Orders.FindAsync(id);
        if (Order is null || !Order.Opens(key)) return NotFound();
        if (Order.IsPaid) return RedirectToPage("/OrderThanks", new { id, key });   // nothing left to pay

        Plan = Plans.Find(Order.PlanCode);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string? key)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null || !order.Opens(key)) return NotFound();
        await _payments.MarkPaidAsync(id);
        return RedirectToPage("/OrderThanks", new { id, key });
    }
}
