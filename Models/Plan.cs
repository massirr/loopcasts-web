namespace LoopCastsWeb.Models;

/// <summary>
/// One product LoopCasts sells. Prices are excl. VAT (from the old pricing.html).
/// Kept in code (not in the database) because it rarely changes; the Order form
/// will use Plans.Find(code) to check that the chosen product really exists.
/// AccessMonths: how long the account stays active (null = lifetime, handbook ch. 1).
/// CompanyOnly: the team licence is sold to companies only, so company details are always required.
/// </summary>
public record Plan(string Code, string Name, decimal Price, string Detail, int? AccessMonths, bool CompanyOnly = false);

public static class Plans
{
    public static readonly IReadOnlyList<Plan> All =
    [
        new("monthly", "Monthly subscription", 15m,  "All courses, billed every month", AccessMonths: 1),
        new("yearly",  "Yearly subscription",  149m, "All courses for 12 months", AccessMonths: 12),
        new("team",    "Team licence",         599m, "5 seats, you add and remove your own team members", AccessMonths: 12, CompanyOnly: true),
        new("single",  "Single course",        39m,  "One course of your choice, lifetime access", AccessMonths: null),
    ];

    public static Plan? Find(string? code) => All.FirstOrDefault(p => p.Code == code);
}
