using System.Text.RegularExpressions;

namespace LoopCastsWeb.Tests;

/// The docs an agent reads must match the repo and stay small (#35, docs/analysis/context-budget.md).
/// A renamed file or a growing CLAUDE.md fails CI instead of silently costing tokens or sending agents to the wrong place.
public class DocsTests
{
    private static readonly string Root = PublicApiTests.RepoRoot();

    [Theory]
    [InlineData("CLAUDE.md")]
    [InlineData("docs/handover.md")]
    public void LoadedByDefault_StaysUnder60Lines(string file)
    {
        Assert.InRange(File.ReadAllLines(Path.Combine(Root, file)).Length, 1, 60);
    }

    // `Services/ViesClient.cs`, `docs/log/` ... must exist. Skipped: templates (<issue>), URLs, ../ (outside the repo),
    // and screenshots (a handover may ask for one that isn't taken yet).
    [Theory]
    [InlineData("CLAUDE.md")]
    [InlineData("docs/architecture.md")]
    [InlineData("docs/handover.md")]
    public void PathsInDocs_Exist(string file)
    {
        var missing = Regex.Matches(File.ReadAllText(Path.Combine(Root, file)), "`([^`]+)`")
            .Select(m => m.Groups[1].Value)
            .Where(p => Regex.IsMatch(p, @"^[\w.][\w./-]*(/|\.(cs|cshtml|md|json|yml|mjs|csv))$") && !p.StartsWith("..") && p.Contains('/'))
            .Where(p => !File.Exists(Path.Combine(Root, p)) && !Directory.Exists(Path.Combine(Root, p)))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Specs_OnlyTheActiveOneOutsideDone()
    {
        var active = Directory.GetFiles(Path.Combine(Root, "docs", "specs"), "*.md")
            .Select(Path.GetFileName)
            .Where(f => f != "_template.md" && !f!.StartsWith("._"))
            .ToList();

        Assert.True(active.Count <= 1, $"Move merged specs to docs/specs/done/: {string.Join(", ", active)}");
    }
}
