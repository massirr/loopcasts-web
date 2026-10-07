using System.Globalization;
using System.Text.RegularExpressions;
using LoopCastsWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Data;

/// <summary>
/// Imports LoopCasts' old course-catalogue.csv ONCE and cleans the messy values:
///   level:     "beginner" / "Beginner" / "easy" / "medium" / "expert"  ->  CourseLevel enum
///   duration:  "1h20" / "1h 15" / "55 min" / "52min" / "1:45:00"     ->  minutes (int)
///   published: "14/03/2023" / "2025-10-14"                           ->  DateOnly
///   empty or "?"                                                     ->  null
/// Every value it has to fix is logged, so you can show the teacher what was wrong.
/// </summary>
public static class CourseCsvImporter
{
    public static async Task SeedAsync(AppDbContext db, string csvPath, ILogger logger)
    {
        if (await db.Courses.AnyAsync())
            return; // already imported earlier

        if (!File.Exists(csvPath))
        {
            logger.LogWarning("Seed file not found: {Path}", csvPath);
            return;
        }

        var lines = await File.ReadAllLinesAsync(csvPath);

        foreach (var line in lines.Skip(1)) // Skip(1) = skip the header row
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Split on ';' and make sure there are always 7 columns
            var col = line.Split(';').Select(c => c.Trim()).ToList();
            while (col.Count < 7) col.Add("");

            var title = col[0];

            var course = new Course
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

            db.Courses.Add(course);
        }

        var count = await db.SaveChangesAsync();
        logger.LogInformation("Imported {Count} courses from {File}", count, Path.GetFileName(csvPath));
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

        logger.LogWarning("'{Title}': could not read date '{Raw}'", title, raw);
        return null;
    }
}
