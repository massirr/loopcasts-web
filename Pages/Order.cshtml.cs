using System.Security.Cryptography;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Pages;

public class OrderModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ViesClient _vies;
    private readonly UserManager<IdentityUser> _users;

    public OrderModel(AppDbContext db, ViesClient vies, UserManager<IdentityUser> users)
    {
        _db = db;
        _vies = vies;
        _users = users;
    }

    [BindProperty]                        // form fields → this object, on POST
    public Order Order { get; set; } = new();

    public Plan? SelectedPlan { get; set; }
    public bool ChoosesCourse => SelectedPlan?.Code == "single";
    public List<SelectListItem> CourseOptions { get; set; } = [];
    public string? AccountEmail { get; set; }    // logged in: the order goes to this login, no e-mail field (#56)

    // Dropdown items for the country field (a fixed list instead of free text)
    public IEnumerable<SelectListItem> CountryOptions =>
        Countries.All.Select(c => new SelectListItem(c.Name, c.Code));

    public async Task<IActionResult> OnGetAsync(string? plan, int? course)   // ?plan=single&course=12 arrives here
    {
        SelectedPlan = Plans.Find(plan);
        if (SelectedPlan is null) return RedirectToPage("/Pricing");

        Order.PlanCode = SelectedPlan.Code;
        Order.IsCompany = SelectedPlan.CompanyOnly;      // team licence: company fields shown from the start
        if (ChoosesCourse) Order.CourseId = course;      // from the course card; checked again on POST
        await LoadChoicesAsync();
        if (AccountEmail is not null)                    // returning customer: name from their last PAID order (an unpaid one could be typed by anyone)
            Order.Name = await _db.Orders.Where(o => o.Email == AccountEmail && o.IsPaid).OrderByDescending(o => o.Id)
                .Select(o => o.Name).FirstOrDefaultAsync() ?? "";
        return Page();
    }

    // Course list + the login's e-mail: needed on GET and again when the form comes back with errors
    private async Task LoadChoicesAsync()
    {
        AccountEmail = User.Identity?.IsAuthenticated == true ? (await _users.GetUserAsync(User))?.Email : null;
        if (!ChoosesCourse) return;
        CourseOptions = await _db.Courses.Where(c => c.Status == "live").OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), c.Id == Order.CourseId)).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        SelectedPlan = Plans.Find(Order.PlanCode);       // never trust the browser
        if (SelectedPlan is null) return RedirectToPage("/Pricing");

        if (Countries.Find(Order.Country) is null)               // never trust the browser: only codes from our list
            ModelState.AddModelError("Order.Country", "Choose your country from the list.");

        // Single course: must be a course you can watch today; other plans never carry a course
        if (!ChoosesCourse)
            Order.CourseId = null;
        else if (Order.CourseId is null || !await _db.Courses.AnyAsync(c => c.Id == Order.CourseId && c.Status == "live"))
            ModelState.AddModelError("Order.CourseId", "Choose the course you want.");

        // Logged in: the order belongs to this login, whatever the browser sent
        await LoadChoicesAsync();
        if (AccountEmail is not null)
        {
            Order.Email = AccountEmail;
            ModelState.Remove("Order.Email");
        }

        // " Ann " and "Ann" are the same customer: trim before saving ([Required] already refuses only-spaces)
        Order.Name = Order.Name?.Trim() ?? "";        // empty field → null from the model binder
        Order.Email = Order.Email?.Trim() ?? "";
        if (ModelState.GetFieldValidationState("Order.Email") == ModelValidationState.Valid && !Input.IsEmail(Order.Email))
            ModelState.AddModelError("Order.Email", "Enter an e-mail address like name@company.com.");

        if (SelectedPlan.CompanyOnly)
            Order.IsCompany = true;                      // team licence is always a company, whatever the browser sent

        if (Order.IsCompany)
        {
            Order.Company = Order.Company?.Trim();
            Order.Address = Order.Address?.Trim();
            if (string.IsNullOrEmpty(Order.Company))
                ModelState.AddModelError("Order.Company", "Enter your company name.");

            if (string.IsNullOrEmpty(Order.Address))
                ModelState.AddModelError("Order.Address", "Enter the company address for the invoice.");

            CheckVatNumberFormat();
        }
        else
        {
            // Individual: don't keep anything typed in the hidden company fields
            Order.Company = null;
            Order.VatNumber = null;
            Order.Address = null;
        }

        if (!ModelState.IsValid) return Page();          // cheap checks first: no VIES call for a form with errors

        // Reverse charge (0%) only exists for EU companies outside Belgium: only then ask VIES (#33)
        if (Order.IsCompany && Countries.Find(Order.Country) is { InEu: true, Code: not "BE" })
        {
            var answer = await _vies.CheckAsync(Order.VatNumber!);
            if (answer.Result == VatCheck.Invalid)
                ModelState.AddModelError("Order.VatNumber", "The EU VAT register (VIES) doesn't know this VAT number. Check it for a typo.");
            Order.VatCheckResult = answer.Result;
            Order.VatCheckedAt = DateTime.UtcNow;
            Order.VatCheckName = answer.Name;
        }

        if (!ModelState.IsValid) return Page();          // VIES: invalid number → no order

        // TODO 1: snapshot the price from SelectedPlan into Order.Price
        Order.Price = SelectedPlan.Price;
        // TODO 2: set Order.CreatedAt to the current time (hint: DateTime.UtcNow)
        Order.CreatedAt = DateTime.UtcNow;
        Order.AccessKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        // TODO 3: add Order to _db.Orders and save (look at how the importer saves)
        _db.Orders.Add(Order);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Pay", new { id = Order.Id, key = Order.AccessKey });   // next: pay (mock checkout)
    }

    /// Cleans the VAT number (BE 0123.456.789 → BE0123456789) and checks it's written correctly for the country.
    private void CheckVatNumberFormat()
    {
        var country = Countries.Find(Order.Country)?.Code;
        if (string.IsNullOrWhiteSpace(Order.VatNumber))
        {
            ModelState.AddModelError("Order.VatNumber", "Enter your company's VAT number, like BE0123456789.");
            return;
        }
        if (country is null || ModelState.GetFieldValidationState("Order.VatNumber") == ModelValidationState.Invalid)
            return;                                      // country error already shown / too long

        Order.VatNumber = VatNumber.Normalise(Order.VatNumber, country);
        if (VatNumber.Error(Order.VatNumber, country, allowTestNumbers: _vies.IsTestService) is { } error)
            ModelState.AddModelError("Order.VatNumber", error);
    }
}
