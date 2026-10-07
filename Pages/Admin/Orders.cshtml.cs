using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Pages.Admin;

// URL: /Admin/Orders  (the folder name becomes part of the URL)
public class OrdersModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;

    public OrdersModel(AppDbContext db, PaymentService payments)
    {
        _db = db;
        _payments = payments;
    }

    public List<Order> Orders { get; set; } = [];

    // GET /Admin/Orders → show all orders, newest first
    public async Task OnGetAsync()
    {
        Orders = await _db.Orders
            .OrderByDescending(o => o.Id)
            .ToListAsync();
    }

    // POST /Admin/Orders?handler=MarkPaid&id=3 → fallback for customers who pay by bank transfer
    public async Task<IActionResult> OnPostMarkPaidAsync(int id)
    {
        // Your 4 lines now live in Services/PaymentService.cs, shared with /Pay (and later the Mollie webhook)
        if (!await _payments.MarkPaidAsync(id)) return NotFound();
        return RedirectToPage();   // back to the list (Post-Redirect-Get)
    }
}
