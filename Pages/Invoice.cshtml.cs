using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

// /Invoice?number=2026-0001 — printable invoice (browser "Print → Save as PDF").
// Customers see only their own invoices, staff (Admin) see all. Replaces the hand-made Word invoice.
[Authorize]
public class InvoiceModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly IConfiguration _config;

    public InvoiceModel(AppDbContext db, UserManager<IdentityUser> users, IConfiguration config)
    {
        _db = db;
        _users = users;
        _config = config;
    }

    public Invoice Invoice { get; set; } = null!;
    public IConfigurationSection Seller => _config.GetSection("Seller");   // name, address, VAT nr (appsettings.json)

    public async Task<IActionResult> OnGetAsync(string number)
    {
        var user = await _users.GetUserAsync(User);
        var invoice = await CustomerData.InvoiceAsync(_db, number, user?.Email, User.IsInRole(AdminSeeder.AdminRole));
        if (invoice is null) return NotFound();   // not yours = "doesn't exist" (don't reveal which numbers exist)

        Invoice = invoice;
        return Page();
    }
}
