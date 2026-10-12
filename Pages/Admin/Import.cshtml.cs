using LoopCastsWeb.Data;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LoopCastsWeb.Pages.Admin;

// /Admin/Import — one-time import of the old Excel exports (#7) and the old website's members table (#20).
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
    [BindProperty] public string? MembersCsv { get; set; }   // the members WITHOUT the password column (never the .sql)

    public OldDataImport? Preview { get; set; }
    public OldMemberImport? MembersPreview { get; set; }
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
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

    public async Task<IActionResult> OnPostPreviewMembersAsync(IFormFile? users)
    {
        if (users is null || users.Length > MaxFileBytes)
        {
            Error = "Choose the users table export (.sql, max. 1 MB).";
            return Page();
        }
        // The .sql (with the MD5 passwords) is read once, here, and dropped; only the cleaned rows go to the page
        MembersPreview = OldMemberImport.ParseSql(await ReadAsync(users), Today);
        MembersCsv = MembersPreview.ToCsv();
        return Page();
    }

    public async Task<IActionResult> OnPostImportMembersAsync()
    {
        if (string.IsNullOrWhiteSpace(MembersCsv)) return RedirectToPage();

        var (imported, existing) = await OldMemberImport.ParseCsv(MembersCsv, Today).ApplyAsync(_db, _members);
        Message = $"Imported {imported} member(s) with a login without password. {existing} already had an account and were skipped. Nobody was e-mailed.";
        return RedirectToPage();
    }

    private static async Task<string> ReadAsync(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());   // UTF-8 (Excel "CSV UTF-8" export)
        return await reader.ReadToEndAsync();
    }
}
