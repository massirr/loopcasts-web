using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Api;

/// <summary>
/// Read-only public data for the chat assistant in n8n (#22): what anyone can already see on /Courses and /Pricing,
/// plus the public FAQ. No customer data, no login, GET only.
/// </summary>
public static class PublicApi
{
    public static void MapPublicApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/public");
        api.MapGet("/courses", Courses);
        api.MapGet("/plans", PlansList);
        api.MapGet("/faq", (IWebHostEnvironment env) => Faq(env.ContentRootPath));
    }

    public record CourseInfo(string Title, string Level, int? Videos, string? Duration, string Url);
    public record PlanInfo(string Code, string Name, decimal PriceExclVat, decimal PriceInclBelgianVat, string Period,
        string Detail, int Seats, bool CompanyOnly, string OrderUrl);

    private static string Base(IConfiguration config) => (config["App:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:5180").TrimEnd('/');

    /// Live courses only (no drafts or retired ones), like /Courses.
    public static async Task<IResult> Courses(AppDbContext db, IConfiguration config)
    {
        var live = await db.Courses.Where(c => c.Status == "live").ToListAsync();
        var url = $"{Base(config)}/Courses";
        return Results.Ok(live.OrderBy(c => c.Level).ThenBy(c => c.Title)
            .Select(c => new CourseInfo(c.Title, c.Level.ToString(), c.VideoCount, c.DurationText, url)).ToList());
    }

    /// The plans from Models/Plan.cs: the same prices the order form uses.
    public static IResult PlansList(IConfiguration config) => Results.Ok(Plans.All.Select(p => new PlanInfo(
        p.Code, p.Name, p.Price, VatCalculator.Calculate(p.Price, "BE", false, null).Total,
        p.AccessMonths switch { 1 => "per month", 12 => "per year", null => "once, lifetime access", var m => $"per {m} months" },
        p.Detail, p.Seats, p.CompanyOnly, $"{Base(config)}/Order?plan={p.Code}")).ToList());

    /// n8n/knowledge/faq.md as markdown (one file for the FAQ, edited in the repo).
    public static IResult Faq(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "n8n", "knowledge", "faq.md");
        return File.Exists(path) ? Results.Text(File.ReadAllText(path), "text/markdown; charset=utf-8") : Results.NotFound();
    }
}
