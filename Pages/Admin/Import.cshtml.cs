using LoopCastsWeb.Data;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages.Admin;

// /Admin/Import — one-time import of the old Excel exports (#7).
// Step 1 "Preview": upload both CSVs → dry-run report, nothing saved.  Step 2 "Import": save, then Post-Redirect-Get.
// The CSV text travels in hidden fields between the steps: files are never stored on the server.
public class ImportModel : PageModel
{
    private const long MaxFileBytes = 1_000_000;

    private readonly AppDbContext _db;
    private readonly MemberService _members;

    public ImportModel(AppDbContext db, MemberService members)
    {
        _db = db;
        _members = members;
    }

    [BindProperty] public string? OrdersCsv { get; set; }
    [BindProperty] public string? RegisterCsv { get; set; }

    public OldDataImport? Preview { get; set; }
    public string? Error { get; set; }
    [TempData] public string? Message { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostPreviewAsync(IFormFile? orders, IFormFile? register)
    {
        if (orders is null || register is null || orders.Length > MaxFileBytes || register.Length > MaxFileBytes)
        {
            Error = "Choose both CSV files (max. 1 MB each).";
            return Page();
        }
        OrdersCsv = await ReadAsync(orders);
        RegisterCsv = await ReadAsync(register);
        Preview = OldDataImport.Parse(OrdersCsv, RegisterCsv);
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (string.IsNullOrWhiteSpace(OrdersCsv) || string.IsNullOrWhiteSpace(RegisterCsv)) return RedirectToPage();

        // Parse again on the server: never trust what came back from the browser as "already checked"
        var (imported, existing) = await OldDataImport.Parse(OrdersCsv, RegisterCsv).ApplyAsync(_db, _members);
        Message = $"Imported {imported} order(s). {existing} were already imported and skipped.";
        return RedirectToPage();
    }

    private static async Task<string> ReadAsync(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());   // UTF-8 (Excel "CSV UTF-8" export)
        return await reader.ReadToEndAsync();
    }
}
