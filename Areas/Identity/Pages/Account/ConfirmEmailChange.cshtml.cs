using System.Text;
using LoopCastsWeb.Data;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace LoopCastsWeb.Areas.Identity.Pages.Account;

// The link in the "confirm your new e-mail" mail. Same steps as the Identity package's page, plus ONE extra:
// orders and seats move to the new e-mail (they are found by e-mail), so /My keeps showing them.
public class ConfirmEmailChangeModel : PageModel
{
    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly AppDbContext _db;

    public ConfirmEmailChangeModel(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn, AppDbContext db)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
    }

    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? userId, string? email, string? code)
    {
        if (userId is null || email is null || code is null) return RedirectToPage("/Index", new { area = "" });
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var oldEmail = user.Email ?? "";
        var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        if (!(await _users.ChangeEmailAsync(user, email, token)).Succeeded)
        {
            StatusMessage = "Error changing email.";
            return Page();
        }
        // The login name is the e-mail (MemberService), so it changes too
        if (!(await _users.SetUserNameAsync(user, email)).Succeeded)
        {
            await _users.SetEmailAsync(user, oldEmail);   // undo: login name and e-mail stay one and the same
            StatusMessage = "Error changing user name.";
            return Page();
        }

        await CustomerData.MoveToEmailAsync(_db, oldEmail, email);
        await _signIn.RefreshSignInAsync(user);
        StatusMessage = "Thank you for confirming your email change.";
        return Page();
    }
}
