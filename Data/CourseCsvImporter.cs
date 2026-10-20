using System.Globalization;
using System.Text.RegularExpressions;
using LoopCastsWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoopCastsWeb.Data;

/// <summary>
/// Imports LoopCasts' course-catalogue.csv (export of their "course catalogue.xlsx") and cleans the messy values:
///   level:     "beginner" / "Beginner" / "easy" / "medium" / "expert"  ->  CourseLevel enum
///   duration:  "1h20" / "1h 15" / "55 min" / "52min" / "1:45:00" / "68" ->  minutes (int)
///   published: "14/03/2023" / "2025-10-14" / "06.02.2019" / "10/13/2023" / "23-1-24"  ->  DateOnly
///   empty, "?" or a date without a year ("24 okt")                     ->  null
/// Runs at every start: a course with the same title (ignoring case and spaces) is updated, not added twice, so its
/// Id stays the same; courses that are not in the file are kept. Every value it has to fix is logged, but only for
/// courses that are new or changed, so a normal start logs nothing.
/// </summary>
public static class CourseCsvImporter
{
    public static async Task SeedAsync(AppDbContext db, string csvPath, ILogger logger)
    {
        if (!File.Exists(csvPath))
        {
            logger.LogWarning("Seed file not found: {Path}", csvPath);
            return;
        }

        var lines = await File.ReadAllLinesAsync(csvPath);
        var courses = await db.Courses.ToListAsync();
        int added = 0, updated = 0;

        foreach (var line in lines.Skip(1)) // Skip(1) = skip the header row
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Split on ';' and make sure there are always 7 columns
            var col = line.Split(';').Select(c => c.Trim()).ToList();
            while (col.Count < 7) col.Add("");

            var course = Parse(col, NullLogger.Instance);
            var existing = courses.FirstOrDefault(c => Key(c.Title) == Key(course.Title));
            if (existing is null)
            {
                db.Courses.Add(course);
                courses.Add(course);
                added++;
            }
            else
            {
                course.Id = existing.Id;
                course.Instructor = existing.Instructor;
                db.Entry(existing).CurrentValues.SetValues(course);   // EF marks only the values that differ
                if (db.Entry(existing).State != EntityState.Modified)
                    continue;   // unchanged: nothing to log
                updated++;
            }
            Parse(col, logger);   // again, now logging what had to be fixed in this row
        }

        await db.SaveChangesAsync();
        if (added + updated > 0)
            logger.LogInformation("Courses from {File}: {Added} added, {Updated} updated", Path.GetFileName(csvPath), added, updated);
    }

    // "N8N  Basics" and "n8n basics" are the same course
    private static string Key(string title) => Regex.Replace(title.Trim(), @"\s+", " ").ToLowerInvariant();

    private static Course Parse(List<string> col, ILogger logger)
    {
        var title = col[0];
        return new Course
        {
            Title = title,
            Level = ParseLevel(col[1], title, logger),
            VideoCount = int.TryParse(col[2], out var videos) ? videos : null,
            DurationMinutes = ParseDuration(col[3], title, logger),
            Status = col[4].ToLowerInvariant(),
            PublishedOn = ParseDate(col[5], title, logger),
            Price = decimal.TryParse(col[6], NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                ? price
                : null,
        };
    }

    private static CourseLevel ParseLevel(string raw, string title, ILogger logger)
    {
        switch (raw.ToLowerInvariant())
        {
            case "beginner": return CourseLevel.Beginner;
            case "intermediate": return CourseLevel.Intermediate;
            case "advanced": return CourseLevel.Advanced;

            // Values that are not in the official list: map them and log it
            case "easy":
                logger.LogWarning("'{Title}': level '{Raw}' mapped to Beginner", title, raw);
                return CourseLevel.Beginner;
            case "medium":
                logger.LogWarning("'{Title}': level '{Raw}' mapped to Intermediate", title, raw);
                return CourseLevel.Intermediate;
            case "expert":
                logger.LogWarning("'{Title}': level '{Raw}' mapped to Advanced", title, raw);
                return CourseLevel.Advanced;

            default:
                logger.LogWarning("'{Title}': unknown level '{Raw}', set to Beginner", title, raw);
                return CourseLevel.Beginner;
        }
    }

    private static int? ParseDuration(string raw, string title, ILogger logger)
    {
        if (raw is "" or "?")
        {
            logger.LogWarning("'{Title}': no duration ('{Raw}')", title, raw);
            return null;
        }

        // "1h20", "1h 15", "1h05"
        var m = Regex.Match(raw, @"^(\d+)\s*h\s*(\d+)?$", RegexOptions.IgnoreCase);
        if (m.Success)
            return int.Parse(m.Groups[1].Value) * 60 + (m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0);

        // "55 min", "52min"
        m = Regex.Match(raw, @"^(\d+)\s*min$", RegexOptions.IgnoreCase);
        if (m.Success)
            return int.Parse(m.Groups[1].Value);

        // "1:45:00"  (hours:minutes:seconds)
        m = Regex.Match(raw, @"^(\d+):(\d{2}):(\d{2})$");
        if (m.Success)
            return int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);

        // "68" (a bare number in the catalogue is always minutes: "202" can't be hours)
        if (int.TryParse(raw, out var minutes))
        {
            logger.LogWarning("'{Title}': duration '{Raw}' read as {Minutes} minutes", title, raw, minutes);
            return minutes;
        }

        logger.LogWarning("'{Title}': could not read duration '{Raw}'", title, raw);
        return null;
    }

    private static DateOnly? ParseDate(string raw, string title, ILogger logger)
    {
        if (raw == "")
            return null;

        string[] formats = ["dd/MM/yyyy", "yyyy-MM-dd"];
        if (DateOnly.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        // Other ways people typed dates. "MM/dd/yyyy" only when day-first is impossible ("10/13/2023").
        string[] other = ["dd.MM.yyyy", "d-M-yy", "MM/dd/yyyy"];
        if (DateOnly.TryParseExact(raw, other, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            logger.LogWarning("'{Title}': date '{Raw}' read as {Date}", title, raw, date.ToString("yyyy-MM-dd"));
            return date;
        }

        logger.LogWarning("'{Title}': could not read date '{Raw}'", title, raw);
        return null;
    }
}
