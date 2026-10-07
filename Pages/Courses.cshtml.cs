using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Pages;

public class CoursesModel : PageModel
{
    private readonly AppDbContext _db;

    // Dependency injection: ASP.NET sees that this page needs an AppDbContext
    // and passes the one we registered in Program.cs.
    public CoursesModel(AppDbContext db) => _db = db;

    public List<Course> Courses { get; set; } = [];

    public async Task OnGetAsync()
    {
        // SQL: SELECT * FROM Courses WHERE Status = 'live'
        var live = await _db.Courses
            .Where(c => c.Status == "live")
            .ToListAsync();

        // Sort in C# (after loading): Level is stored as text in the database,
        // so sorting there would be alphabetical (Advanced, Beginner, ...).
        Courses = live
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Title)
            .ToList();
    }

    // Helper used by the view: 80 -> "1h 20m", 55 -> "55 min"
    public static string FormatDuration(int? minutes) => minutes switch
    {
        null => "–",
        < 60 => $"{minutes} min",
        _ => $"{minutes / 60}h {minutes % 60:00}m",
    };
}
