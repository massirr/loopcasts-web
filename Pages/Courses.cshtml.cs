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
    public decimal SinglePrice => Plans.Find("single")?.Price ?? 0;

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
        // "Recorded by …" only when there is more than one instructor (loopcasts-ui §6); read by _CourseCard
        ViewData["ManyInstructors"] = live.Select(c => c.Instructor).Distinct().Count() > 1;
    }
}
