using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

// /My — the customer's own subscriptions and invoices. [Authorize] = not logged in → login page first.
[Authorize]
public class MyModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly TeamSeats _seats;
    private readonly MemberService _members;

    public MyModel(AppDbContext db, UserManager<IdentityUser> users, TeamSeats seats, MemberService members)
    {
        _db = db;
        _users = users;
        _seats = seats;
        _members = members;
    }

    public string Email { get; set; } = "";
    public List<CustomerData.Subscription> Subscriptions { get; set; } = [];
    public List<Invoice> Invoices { get; set; } = [];
    public List<TeamSeats.Licence> Licences { get; set; } = [];

    [TempData] public string? Message { get; set; }
    [TempData] public string? Error { get; set; }
    [TempData] public string? SetupUrl { get; set; }      // shown once, right after adding a NEW colleague

    public async Task OnGetAsync()
    {
        // The e-mail comes from the login cookie, never from the URL: you can only ever see your own data
        var user = await _users.GetUserAsync(User);
        Email = user?.Email ?? "";
        if (Email == "") return;

        Subscriptions = await CustomerData.SubscriptionsAsync(_db, Email);
        Invoices = await CustomerData.InvoicesAsync(_db, Email);
        Licences = await _seats.LicencesAsync(Email);
    }

    // The buyer's e-mail always comes from the login; TeamSeats checks it bought this order
    private async Task<string> MyEmailAsync() => (await _users.GetUserAsync(User))?.Email ?? "";

    public async Task<IActionResult> OnPostAddSeatAsync(int orderId, string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email.Trim()))
        {
            Error = "Enter a valid e-mail address.";
            return RedirectToPage();
        }

        var result = await _seats.AddAsync(orderId, await MyEmailAsync(), email);
        if (result.Error is not null)
        {
            Error = result.Error;
        }
        else
        {
            Message = $"{email.Trim()} now has access.";
            if (result.NewLogin is { } user)
            {
                var code = await _members.PasswordSetupCodeAsync(user);
                SetupUrl = Url.Page("/Account/ResetPassword", null, new { area = "Identity", code }, Request.Scheme);
            }
        }
        return RedirectToPage();   // Post-Redirect-Get
    }

    public async Task<IActionResult> OnPostRemoveSeatAsync(int orderId, int accountId)
    {
        Error = await _seats.RemoveAsync(orderId, await MyEmailAsync(), accountId);
        if (Error is null) Message = "Seat removed.";
        return RedirectToPage();
    }
}
