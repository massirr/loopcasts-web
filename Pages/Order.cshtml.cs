using System.Security.Cryptography;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

public class OrderModel : PageModel
{
    private readonly AppDbContext _db;
    public OrderModel(AppDbContext db) => _db = db;

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
            ModelState.AddModelError("Order.Country", "Choose a country from the list.");

        if (SelectedPlan.CompanyOnly)
            Order.IsCompany = true;                      // team licence is always a company, whatever the browser sent

        if (Order.IsCompany)
        {
            if (string.IsNullOrWhiteSpace(Order.Company))
                ModelState.AddModelError("Order.Company", "Company name is required for companies.");

            if (string.IsNullOrWhiteSpace(Order.VatNumber))
                ModelState.AddModelError("Order.VatNumber", "VAT number is required for companies.");

            if (string.IsNullOrWhiteSpace(Order.Address))
                ModelState.AddModelError("Order.Address", "Address is required for companies.");
        }
        else
        {
            // Individual: don't keep anything typed in the hidden company fields
            Order.Company = null;
            Order.VatNumber = null;
            Order.Address = null;
        }

        if (!ModelState.IsValid) return Page();          // [Required] failed → show errors

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
}