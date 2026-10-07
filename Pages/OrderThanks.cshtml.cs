using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Pages;

// Shown after a successful order (Post-Redirect-Get: refreshing this page never re-submits the form).
public class OrderThanksModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly MemberService _members;

    public OrderThanksModel(AppDbContext db, UserManager<IdentityUser> users, MemberService members)
    {
        _db = db;
        _users = users;
        _members = members;
    }

    public Order? Order { get; set; }
    public Plan? Plan { get; set; }
    public Invoice? Invoice { get; set; }
    public Account? Account { get; set; }
    public string? PasswordSetupUrl { get; set; }   // first order: "choose your password"
    public bool HasLogin { get; set; }              // returning customer: "log in"

    public async Task<IActionResult> OnGetAsync(int id, string? key)
    {
        Order = await _db.Orders.FindAsync(id);   // SELECT ... WHERE Id = @id
        if (Order is null || !Order.Opens(key)) return NotFound();

        Plan = Plans.Find(Order.PlanCode);
        Invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.OrderId == id);
        Account = await _db.Accounts.FirstOrDefaultAsync(a => a.OrderId == id);

        var user = Order.IsPaid ? await _users.FindByEmailAsync(Order.Email) : null;
        if (user is null) return Page();

        // Only for the e-mail's FIRST paid order: otherwise anyone could order with someone else's
        // e-mail address and set the password of that person's account. Later the link is e-mailed (#5).
        var firstPaidOrder = await _db.Orders.Where(o => o.Email == Order.Email && o.IsPaid).MinAsync(o => o.Id);
        if (!await _users.HasPasswordAsync(user) && firstPaidOrder == id)
        {
            var code = await _members.PasswordSetupCodeAsync(user);
            PasswordSetupUrl = Url.Page("/Account/ResetPassword", null, new { area = "Identity", code }, Request.Scheme);
        }
        else
        {
            HasLogin = true;
        }
        return Page();
    }
}
