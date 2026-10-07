using LoopCastsWeb.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

public class PricingModel : PageModel
{
    public IReadOnlyList<Plan> Plans { get; private set; } = [];

    public void OnGet()
    {
        Plans = Models.Plans.All;
    }
}
