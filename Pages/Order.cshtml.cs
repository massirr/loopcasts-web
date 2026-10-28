using System.Security.Cryptography;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

public class OrderModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ViesClient _vies;

    public OrderModel(AppDbContext db, ViesClient vies)
    {
        _db = db;
        _vies = vies;
    }

    [BindProperty]                        // form fields → this object, on POST
    public Order Order { get; set; } = new();

    public Plan? SelectedPlan { get; set; }

    // Dropdown items for the country field (a fixed list instead of free text)
    public IEnumerable<SelectListItem> CountryOptions =>
        Countries.All.Select(c => new SelectListItem(c.Name, c.Code));

    public IActionResult OnGet(string? plan)     // ?plan=monthly arrives here
    {
        SelectedPlan = Plans.Find(plan);
        if (SelectedPlan is null) return RedirectToPage("/Pricing");

        Order.PlanCode = SelectedPlan.Code;
        Order.IsCompany = SelectedPlan.CompanyOnly;      // team licence: company fields shown from the start
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        SelectedPlan = Plans.Find(Order.PlanCode);       // never trust the browser
        if (SelectedPlan is null) return RedirectToPage("/Pricing");

        if (Countries.Find(Order.Country) is null)               // never trust the browser: only codes from our list
            ModelState.AddModelError("Order.Country", "Choose your country from the list.");

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
