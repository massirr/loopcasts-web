using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using System.Text.RegularExpressions;
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
        api.MapGet("/faq", (IWebHostEnvironment env, IConfiguration config) => Faq(env.ContentRootPath, config));
    }

    public record CourseInfo(string Title, string Level, int? Videos, string? Duration, string Url);
    // No price incl. VAT: it depends on country and company (FAQ), a Belgian-only total misled the model (#29)
    public record PlanInfo(string Code, string Name, decimal PriceExclVat, string Period,
        string Detail, int Seats, bool CompanyOnly, string OrderUrl);
    public record PlansInfo(string PricingUrl, List<PlanInfo> Plans);

    private static string Base(IConfiguration config) => (config["App:BaseUrl"] is { Length: > 0 } url ? url : "http://localhost:5180").TrimEnd('/');

    /// Live courses only (no drafts or retired ones), like /Courses.
    public static async Task<IResult> Courses(AppDbContext db, IConfiguration config)
    {
        var live = await db.Courses.Where(c => c.Status == "live").ToListAsync();
        var url = $"{Base(config)}/Courses";
        return Results.Ok(live.OrderBy(c => c.Level).ThenBy(c => c.Title)
            .Select(c => new CourseInfo(c.Title, c.Level.ToString(), c.VideoCount, c.DurationText, url)).ToList());
    }

    /// The plans from Models/Plan.cs: the same prices the order form uses, plus the link to /Pricing.
    public static IResult PlansList(IConfiguration config) => Results.Ok(new PlansInfo($"{Base(config)}/Pricing", Plans.All.Select(p => new PlanInfo(
        p.Code, p.Name, p.Price,
        p.AccessMonths switch { 1 => "per month", 12 => "per year", null => "once, lifetime access", var m => $"per {m} months" },
        p.Detail, p.Seats, p.CompanyOnly, $"{Base(config)}/Order?plan={p.Code}")).ToList()));

    /// n8n/knowledge/faq.md as markdown (one file for the FAQ, edited in the repo). Site paths like /Pricing become
    /// full URLs, so every link the model sees is one a visitor can click (#29).
    public static IResult Faq(string contentRoot, IConfiguration config)
    {
        var path = Path.Combine(contentRoot, "n8n", "knowledge", "faq.md");
        if (!File.Exists(path)) return Results.NotFound();
        var text = Regex.Replace(File.ReadAllText(path), @"(?<=^|\s)/(?=[A-Z])", $"{Base(config)}/", RegexOptions.Multiline);
        return Results.Text(text, "text/markdown; charset=utf-8");
    }
}
