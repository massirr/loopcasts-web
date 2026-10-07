namespace LoopCastsWeb.Models;

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public CourseLevel Level { get; set; }
    public int? VideoCount { get; set; }        // ? = may be empty (drafts don't have videos yet)
    public int? DurationMinutes { get; set; }
    public string Status { get; set; } = "";
    public DateOnly? PublishedOn { get; set; }
    public decimal? Price { get; set; }
    public string Instructor { get; set; } = "Marieke De Smet";   // "Recorded by Marieke" on the course card

    // 80 → "1 h 20 min", 60 → "1 h", 55 → "55 min", null → null (unknown, not shown)
    public string? DurationText => DurationMinutes switch
    {
        null => null,
        < 60 => $"{DurationMinutes} min",
        _ when DurationMinutes % 60 == 0 => $"{DurationMinutes / 60} h",
        _ => $"{DurationMinutes / 60} h {DurationMinutes % 60} min",
    };
}

public enum CourseLevel
{
    Beginner,
    Intermediate,
    Advanced
}