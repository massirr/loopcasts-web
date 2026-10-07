using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Pages.Admin;

// /Admin/Invoices — replaces "Facturen 2026.xlsx" + summing green rows by hand
public class InvoicesModel : PageModel
{
    private readonly AppDbContext _db;
    public InvoicesModel(AppDbContext db) => _db = db;

    public List<Invoice> Invoices { get; set; } = [];
    public List<Account> Accounts { get; set; } = [];

    public async Task OnGetAsync()
    {
        Invoices = await _db.Invoices.OrderByDescending(i => i.Number).ToListAsync();
        Accounts = await _db.Accounts.OrderBy(a => a.EndsOn).ToListAsync();
    }

    [TempData] public string? Message { get; set; }

    // Button "Run expiry check now" (the same check also runs automatically every 24h)
    public async Task<IActionResult> OnPostRunExpiryAsync()
    {
        var count = await AccountExpiry.RunAsync(_db);
        Message = $"Expiry check done: {count} account(s) deactivated.";
        return RedirectToPage();
    }
}
