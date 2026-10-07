using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Course> Courses { get; set; } = [];

    // The six newest live courses; the rest is on /Courses
    public async Task OnGetAsync()
    {
        var live = await _db.Courses.Where(c => c.Status == "live").ToListAsync();
        Courses = live.OrderByDescending(c => c.PublishedOn).Take(6).ToList();
    }
}
