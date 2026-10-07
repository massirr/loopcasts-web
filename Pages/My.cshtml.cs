using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

// /My — the customer's own subscriptions and invoices. [Authorize] = not logged in → login page first.
[Authorize]
public class MyModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _users;

    public MyModel(AppDbContext db, UserManager<IdentityUser> users)
    {
        _db = db;
        _users = users;
    }

    public string Email { get; set; } = "";
    public List<CustomerData.Subscription> Subscriptions { get; set; } = [];
    public List<Invoice> Invoices { get; set; } = [];

    public async Task OnGetAsync()
    {
        // The e-mail comes from the login cookie, never from the URL: you can only ever see your own data
        var user = await _users.GetUserAsync(User);
        Email = user?.Email ?? "";
        if (Email == "") return;

        Subscriptions = await CustomerData.SubscriptionsAsync(_db, Email);
        Invoices = await CustomerData.InvoicesAsync(_db, Email);
    }
}
