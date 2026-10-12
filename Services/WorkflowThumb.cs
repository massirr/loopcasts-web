using System.Globalization;
using System.Text.RegularExpressions;
using LoopCastsWeb.Models;

namespace LoopCastsWeb.Services;

/// <summary>
/// Every course is drawn as a small workflow (loopcasts-ui skill §5). This is the geometry only, as a pure function:
/// the same course always gives the same picture. Pages/Shared/_WorkflowThumb.cshtml turns it into SVG.
/// viewBox 0 0 160 70:  trigger circle (level colour) → 2–4 steps, a split into two parallel steps, a loop back
/// or a tool node → end square.
/// </summary>
public static class WorkflowThumb
{
    public enum Kind { Trigger, Step, End, Tool }

    /// Straight line, split into two parallel steps, a loop back (scheduled/repeating work), or a step that calls a tool (AI).
    public enum Shape { Straight, Branch, Loop, Tool }

    /// Trigger: X/Y = centre, W = diameter. Step/End/Tool: X/Y = top-left corner.
    public record Node(Kind Kind, double X, double Y, double W, double H);

    /// Edges are SVG path data ("M33 35H48", curves "C" on a branch or loop).
    public record Thumb(Shape Shape, IReadOnlyList<Node> Nodes, IReadOnlyList<string> Edges)
    {
        public bool Branch => Shape == Shape.Branch;
        public int Steps => Nodes.Count(n => n.Kind == Kind.Step);
    }

    private const double Mid = 35, StartX = 33, EndX = 140;
    private static readonly Regex BranchWords = new(@"\b(error|if|routing|human)\b", RegexOptions.IgnoreCase);
    private static readonly Regex LoopWords = new(@"\b(cron|schedul\w*|loop|retry|batch|every)\b", RegexOptions.IgnoreCase);
    private static readonly Regex ToolWords = new(@"\b(ai|agents?|rag|mcp|llm|prompt)\b", RegexOptions.IgnoreCase);

    /// One step per 4 videos, at least 2, at most 4. Unknown video count → 2.
    public static int StepCount(int? videos) => Math.Clamp((int)Math.Ceiling((videos ?? 0) / 4.0), 2, 4);

    /// A third of the courses (Id % 3 == 0), and courses about errors, conditions, routing or humans, branch.
    public static bool HasBranch(int id, string title) => id % 3 == 0 || BranchWords.IsMatch(title);

    /// Branch first (§5), then title words (AI → tool, schedules → loop), then the Id spreads the rest: Id % 3 == 1 loops.
    public static Shape ShapeOf(int id, string title) =>
        HasBranch(id, title) ? Shape.Branch
        : ToolWords.IsMatch(title) ? Shape.Tool
        : LoopWords.IsMatch(title) || id % 3 == 1 ? Shape.Loop
        : Shape.Straight;

    public static Thumb Build(Course course) => Build(course.Id, course.Title, course.VideoCount);

    public static Thumb Build(int id, string title, int? videos)
    {
        var shape = ShapeOf(id, title);
        var n = StepCount(videos);
        var size = n == 4 ? 18.0 : 22.0;
        var nodes = new List<Node> { new(Kind.Trigger, 20, Mid, 26, 26) };
        var edges = new List<string>();

        if (shape != Shape.Branch)
        {
            // n steps in a straight line, equal gaps
            var gap = (EndX - StartX - n * size) / (n + 1);
            var x = StartX;
            for (var i = 0; i < n; i++)
            {
                var next = x + gap;
                edges.Add($"M{F(x)} {F(Mid)}H{F(next)}");
                nodes.Add(new(Kind.Step, next, Mid - size / 2, size, size));
                x = next + size;
            }
            edges.Add($"M{F(x)} {F(Mid)}H{F(EndX)}");

            var steps = nodes.Where(s => s.Kind == Kind.Step).ToList();
            var top = Mid - size / 2;
            if (shape == Shape.Loop)
            {
                // an arc above the line, from the last step back to the first: the work repeats
                var (a, b) = (steps[^1].X + size / 2, steps[0].X + size / 2);
                edges.Add($"M{F(a)} {F(top)}C{F(a)} 4 {F(b)} 4 {F(b)} {F(top)}");
            }
            else if (shape == Shape.Tool)
            {
                // a small tool node under one step (which one depends on the Id), joined by a short line
                var s = steps[id % steps.Count];
                var cx = s.X + size / 2;
                edges.Add($"M{F(cx)} {F(top + size)}V54");
                nodes.Add(new(Kind.Tool, cx - 6, 54, 12, 12));
            }
        }
        else if (id % 2 == 0)
        {
            // split straight after the trigger into two parallel steps, merge into one step before the end
            const double split = 52, last = 104;
            edges.Add($"M{F(StartX)} {F(Mid)}C{F(StartX + 10)} {F(Mid)} {F(split - 10)} 16 {F(split)} 16");
            edges.Add($"M{F(StartX)} {F(Mid)}C{F(StartX + 10)} {F(Mid)} {F(split - 10)} 54 {F(split)} 54");
            nodes.Add(new(Kind.Step, split, 6, size, 20));
            nodes.Add(new(Kind.Step, split, 44, size, 20));
            var b = split + size;
            edges.Add($"M{F(b)} 16C{F(b + 10)} 16 {F(last - 10)} {F(Mid)} {F(last)} {F(Mid)}");
            edges.Add($"M{F(b)} 54C{F(b + 10)} 54 {F(last - 10)} {F(Mid)} {F(last)} {F(Mid)}");
            nodes.Add(new(Kind.Step, last, Mid - size / 2, size, size));
            edges.Add($"M{F(last + size)} {F(Mid)}H{F(EndX)}");
        }
        else
        {
            // one step, then split into two parallel steps that merge again before the end
            const double first = 48, split = 92;
            edges.Add($"M{F(StartX)} {F(Mid)}H{F(first)}");
            nodes.Add(new(Kind.Step, first, Mid - size / 2, size, size));
            var a = first + size;
            edges.Add($"M{F(a)} {F(Mid)}C{F(a + 12)} {F(Mid)} {F(split - 12)} 16 {F(split)} 16");
            edges.Add($"M{F(a)} {F(Mid)}C{F(a + 12)} {F(Mid)} {F(split - 12)} 54 {F(split)} 54");
            nodes.Add(new(Kind.Step, split, 6, size, 20));
            nodes.Add(new(Kind.Step, split, 44, size, 20));
            var b = split + size;
            edges.Add($"M{F(b)} 16C{F(b + 10)} 16 {F(EndX - 10)} {F(Mid)} {F(EndX)} {F(Mid)}");
            edges.Add($"M{F(b)} 54C{F(b + 10)} 54 {F(EndX - 10)} {F(Mid)} {F(EndX)} {F(Mid)}");
        }

        nodes.Add(new(Kind.End, EndX, Mid - 9, 18, 18));
        return new Thumb(shape, nodes, edges);
    }

    /// Numbers in SVG always with a dot ("12.5"), whatever the server's culture is.
    public static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
