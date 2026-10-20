using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoopCastsWeb.Tests;

public class CourseCsvImporterTests
{
    /// Remembers every log line, to check what the importer reports.
    private class ListLogger : ILogger
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? e, Func<TState, Exception?, string> format)
            => Lines.Add(format(state, e));
    }

    private static string Csv(params string[] rows)
    {
        var path = Path.GetTempFileName();
        File.WriteAllLines(path, ["title;level;videos;total_duration;status;published;single_price", .. rows]);
        return path;
    }

    [Fact]
    public async Task Seed_FullCatalogue_60Courses_53Live()
    {
        var db = TestDb.Create();

        await CourseCsvImporter.SeedAsync(db, Path.Combine(PublicApiTests.RepoRoot(), "Data", "Seed", "course-catalogue.csv"), new ListLogger());

        var status = await db.Courses.GroupBy(c => c.Status).ToDictionaryAsync(g => g.Key, g => g.Count());
        Assert.Equal(new Dictionary<string, int> { ["live"] = 53, ["retired"] = 4, ["draft"] = 3 }, status);
    }

    [Fact]
    public async Task Seed_MessyValuesFromTheFullCatalogue_AreCleanedAndLogged()
    {
        var db = TestDb.Create();
        var log = new ListLogger();

        await CourseCsvImporter.SeedAsync(db, Csv(
            "Airtable automations;easy;6;68;live;2026-06-23;39",
            "Docker for automation people;advanced;11;2h 4;live;2026-02-14;",
            "Slack bots with n8n;advanced;7;102 min;live;06.02.2019;",
            "Scraping politely;easy;13;2h55;live;10/13/2023;39",
            "Testing workflows;;14;217;live;23-1-24;39",
            "Home automation with n8n;intermediate;7;98;live;24 okt;39"), log);

        var c = await db.Courses.ToDictionaryAsync(x => x.Title);
        Assert.Equal((68, new DateOnly(2026, 6, 23)), (c["Airtable automations"].DurationMinutes, c["Airtable automations"].PublishedOn!.Value));
        Assert.Equal((124, (decimal?)null), (c["Docker for automation people"].DurationMinutes, c["Docker for automation people"].Price));
        Assert.Equal(new DateOnly(2019, 2, 6), c["Slack bots with n8n"].PublishedOn);       // dd.MM.yyyy
        Assert.Equal(new DateOnly(2023, 10, 13), c["Scraping politely"].PublishedOn);       // month first: 13 can't be a month
        Assert.Equal((217, new DateOnly(2024, 1, 23), CourseLevel.Beginner),
            (c["Testing workflows"].DurationMinutes, c["Testing workflows"].PublishedOn!.Value, c["Testing workflows"].Level));
        Assert.Null(c["Home automation with n8n"].PublishedOn);                             // no year: unknown, not guessed
        Assert.Contains(log.Lines, l => l.Contains("'68'") && l.Contains("minutes"));
        Assert.Contains(log.Lines, l => l.Contains("'10/13/2023'") && l.Contains("2023-10-13"));
        Assert.Contains(log.Lines, l => l.Contains("'24 okt'"));
    }

    [Fact]
    public async Task Seed_AgainOnAnExistingDatabase_UpdatesByTitle_KeepsIds_LogsOnlyChanges()
    {
        var db = TestDb.Create();
        var old = new Course { Title = "N8N  Basics", Level = CourseLevel.Beginner, Status = "draft", VideoCount = 3 };
        var notInCsv = new Course { Title = "Course added by hand", Status = "live" };
        db.Courses.AddRange(old, notInCsv);
        await db.SaveChangesAsync();
        var csv = Csv("n8n basics;beginner;12;1h20;live;14/03/2023;39", "Webhooks from zero;beginner;9;55 min;live;02/05/2023;39");

        await CourseCsvImporter.SeedAsync(db, csv, new ListLogger());
        var log = new ListLogger();
        await CourseCsvImporter.SeedAsync(db, csv, log);   // second start: nothing new

        var courses = await db.Courses.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(3, courses.Count);                                       // no duplicate, hand-made course kept
        Assert.Equal((old.Id, "n8n basics", "live", 12), (courses[0].Id, courses[0].Title, courses[0].Status, courses[0].VideoCount!.Value));
        Assert.Empty(log.Lines);
    }
}
