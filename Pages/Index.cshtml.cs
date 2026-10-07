using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages;

public class IndexModel : PageModel
{
    public string Slogan { get; set; } = "";

    public void OnGet()
    {
        Slogan = "Hello, Who TF is the World";
    }
}
