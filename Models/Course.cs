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

}

public enum CourseLevel
{
    Beginner,
    Intermediate,
    Advanced
}