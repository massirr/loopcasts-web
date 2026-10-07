using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Pages;

// Shown after a successful order (Post-Redirect-Get: refreshing this page never re-submits the form).
public class OrderThanksModel : PageModel
{
    private readonly AppDbContext _db;
    public OrderThanksModel(AppDbContext db) => _db = db;
    public Order? Order { get; set; }
    public Plan? Plan { get; set; }
    public Invoice? Invoice { get; set; }
    public Account? Account { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Order = await _db.Orders.FindAsync(id);   // SELECT ... WHERE Id = @id
        if (Order is null) return NotFound();

        Plan = Plans.Find(Order.PlanCode);
        Invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.OrderId == id);
        Account = await _db.Accounts.FirstOrDefaultAsync(a => a.OrderId == id);
        return Page();
    }
}
