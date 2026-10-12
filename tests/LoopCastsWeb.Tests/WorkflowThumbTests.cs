using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using static LoopCastsWeb.Services.WorkflowThumb;

namespace LoopCastsWeb.Tests;

public class WorkflowThumbTests
{
    [Theory]
    [InlineData(null, 2)]
    [InlineData(0, 2)]
    [InlineData(4, 2)]
    [InlineData(9, 3)]
    [InlineData(12, 3)]
    [InlineData(13, 4)]
    [InlineData(40, 4)]
    public void StepCount_OnePerFourVideos_Between2And4(int? videos, int steps) => Assert.Equal(steps, StepCount(videos));

    [Theory]
    [InlineData(3, "n8n basics", true)]                // every third course
    [InlineData(1, "n8n error handling", true)]
    [InlineData(1, "What if the API is down?", true)]
    [InlineData(2, "Routing leads", true)]
    [InlineData(4, "Human in the loop", true)]
    [InlineData(1, "Shell scripting that saves your week", false)]
    [InlineData(2, "Shifting to n8n", false)]           // "if" inside a word does not count
    [InlineData(4, "Errors explained", false)]          // only the whole word "error"
    public void HasBranch_IdOrTitleWord(int id, string title, bool branch) => Assert.Equal(branch, HasBranch(id, title));

    [Theory]
    [InlineData(2, "n8n basics", Shape.Straight)]
    [InlineData(1, "n8n basics", Shape.Loop)]              // Id % 3 == 1 loops
    [InlineData(5, "Cron and schedulers", Shape.Loop)]     // schedules repeat
    [InlineData(1, "AI agents with n8n", Shape.Tool)]      // AI calls a tool; the word wins over the Id
    [InlineData(11, "RAG on your own documents", Shape.Tool)]
    [InlineData(3, "AI agents", Shape.Branch)]             // the branch rule (§5) always comes first
    [InlineData(2, "Retail tips", Shape.Straight)]         // "ai" inside a word does not count
    public void ShapeOf_TitleWordsThenId(int id, string title, Shape shape) => Assert.Equal(shape, ShapeOf(id, title));

    [Theory]
    [InlineData(2, 12, 3, 22)]
    [InlineData(5, null, 2, 22)]
    [InlineData(8, 13, 4, 18)]   // four steps are drawn smaller to fit
    public void Straight_StepsInALine_WithOneEdgeMoreThanSteps(int id, int? videos, int steps, double size)
    {
        var t = Build(id, "Plain title", videos);

        Assert.False(t.Branch);
        Assert.Equal(steps, t.Steps);
        Assert.Equal(steps + 1, t.Edges.Count);
        Assert.All(t.Nodes.Where(n => n.Kind == Kind.Step), n => Assert.Equal(size, n.W));
        Assert.Equal(Kind.Trigger, t.Nodes[0].Kind);
        Assert.Equal(Kind.End, t.Nodes[^1].Kind);
    }

    [Fact]
    public void Branch_OneStepThenTwoParallelSteps_CurvedEdges()
    {
        var t = Build(7, "n8n error handling", 8);

        Assert.True(t.Branch);
        Assert.Equal(3, t.Steps);
        Assert.Equal(5, t.Edges.Count);
        Assert.Equal(4, t.Edges.Count(e => e.Contains('C')));
    }

    [Fact]
    public void Branch_EvenId_SplitsRightAfterTheTrigger()
    {
        var t = Build(6, "Plain title", 8);

        Assert.True(t.Branch);
        Assert.Equal(3, t.Steps);
        Assert.Equal(5, t.Edges.Count);
        Assert.StartsWith("M33 35C", t.Edges[0]);   // the first edge already curves
    }

    [Fact]
    public void Loop_ArcFromLastStepBackToFirst()
    {
        var t = Build(4, "Plain title", 12);

        Assert.Equal(Shape.Loop, t.Shape);
        Assert.Equal(3 + 2, t.Edges.Count);        // line edges + the arc
        Assert.Contains('C', t.Edges[^1]);
    }

    [Fact]
    public void Tool_OneSmallNodeUnderAStep()
    {
        var t = Build(10, "AI agents with n8n", 13);

        var tool = Assert.Single(t.Nodes, n => n.Kind == Kind.Tool);
        Assert.Equal(12, tool.W);
        Assert.Equal(5 + 1, t.Edges.Count);        // 4 steps → 5 line edges, plus the line to the tool
    }

    [Fact]
    public void SameCourse_SamePicture()
    {
        var a = Build(new Course { Id = 10, Title = "AI agents with n8n", VideoCount = 13 });
        var b = Build(new Course { Id = 10, Title = "AI agents with n8n", VideoCount = 13 });

        Assert.Equal(a.Nodes, b.Nodes);
        Assert.Equal(a.Edges, b.Edges);
    }

    [Fact]
    public void EveryShape_StaysInsideTheViewBox()
    {
        for (var id = 1; id <= 6; id++)
            foreach (var videos in new int?[] { null, 4, 9, 13, 50 })
            foreach (var title in new[] { "x", "AI agents" })
                foreach (var n in Build(id, title, videos).Nodes)
                {
                    var (left, top) = n.Kind == Kind.Trigger ? (n.X - n.W / 2, n.Y - n.H / 2) : (n.X, n.Y);
                    Assert.InRange(left, 0, 160 - n.W);
                    Assert.InRange(top, 0, 70 - n.H);
                }
    }

    [Fact]
    public void Numbers_UseADot_EvenOnABelgianServer()
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("nl-BE");
            Assert.DoesNotContain(Build(1, "x", 12).Edges, e => e.Contains(','));
        }
        finally { Thread.CurrentThread.CurrentCulture = culture; }
    }

    [Theory]
    [InlineData(80, "1 h 20 min")]
    [InlineData(60, "1 h")]
    [InlineData(55, "55 min")]
    [InlineData(null, null)]
    public void DurationText(int? minutes, string? text) => Assert.Equal(text, new Course { DurationMinutes = minutes }.DurationText);
}
