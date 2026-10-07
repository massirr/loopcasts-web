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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Order = await _db.Orders.FindAsync(id);
        if (Order is null) return NotFound();
        if (Order.IsPaid) return RedirectToPage("/OrderThanks", new { id });   // nothing left to pay

        Plan = Plans.Find(Order.PlanCode);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await _payments.MarkPaidAsync(id)) return NotFound();
        return RedirectToPage("/OrderThanks", new { id });
    }
}
