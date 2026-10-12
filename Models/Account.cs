namespace LoopCastsWeb.Models;

/// <summary>
/// Access to the courses, created automatically when an order is paid.
/// Replaces Lotte's manual "insert row in phpMyAdmin + MD5 password" (handbook ch. 3).
/// </summary>
public class Account
{
    public int Id { get; set; }
    public int? OrderId { get; set; }                // FK → Orders.Id; null = member imported from the old website (#20)
    public string? PlanCode { get; set; }            // only for imported members (no order to read the plan from)
    public string Email { get; set; } = "";
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }            // null = lifetime (single course)
    public bool IsActive { get; set; } = true;       // the nightly job sets this to false after EndsOn
}
