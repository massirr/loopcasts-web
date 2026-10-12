using System.Text.RegularExpressions;
using LoopCastsWeb.Api;
using LoopCastsWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace LoopCastsWeb.Tests;

public class PublicApiTests
{
    private static readonly IConfiguration Config =
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:BaseUrl"] = "https://shop.test/" }).Build();

    [Fact]
    public async Task Courses_OnlyLive_WithLink()
    {
        var db = TestDb.Create();
        db.Courses.AddRange(
            new Course { Title = "n8n basics", Level = CourseLevel.Beginner, Status = "live", VideoCount = 12, DurationMinutes = 80 },
            new Course { Title = "Draft course", Level = CourseLevel.Advanced, Status = "draft" },
            new Course { Title = "Old course", Level = CourseLevel.Beginner, Status = "retired" });
        await db.SaveChangesAsync();

        var list = Assert.IsType<Ok<List<PublicApi.CourseInfo>>>(await PublicApi.Courses(db, Config)).Value!;

        var c = Assert.Single(list);
        Assert.Equal(new PublicApi.CourseInfo("n8n basics", "Beginner", 12, "1 h 20 min", "https://shop.test/Courses"), c);
    }

    [Fact]
    public void Plans_SamePricesAsTheOrderForm()
    {
        var list = Assert.IsType<Ok<List<PublicApi.PlanInfo>>>(PublicApi.PlansList(Config)).Value!;

        Assert.Equal(Plans.All.Select(p => (p.Code, p.Price)), list.Select(p => (p.Code, p.PriceExclVat)));
        var yearly = list.Single(p => p.Code == "yearly");
        Assert.Equal((180.29m, "per year", "https://shop.test/Order?plan=yearly"), (yearly.PriceInclBelgianVat, yearly.Period, yearly.OrderUrl));
        Assert.Equal((5, true), (list.Single(p => p.Code == "team").Seats, list.Single(p => p.Code == "team").CompanyOnly));
    }

    // The real FAQ in the repo: it goes to an AI model, so it may hold public information only
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "n8n", "knowledge", "faq.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("n8n/knowledge/faq.md not found");
    }

    [Fact]
    public void Faq_ServedAsMarkdown_PublicInformationOnly()
    {
        var result = Assert.IsType<ContentHttpResult>(PublicApi.Faq(RepoRoot()));
        var text = result.ResponseContent!;

        Assert.StartsWith("text/markdown", result.ContentType);
        Assert.Contains("support@loopcasts.example", text);
        Assert.Equal(["support@loopcasts.example"], Regex.Matches(text, @"[\w.+-]+@[\w-]+(\.[\w-]+)+").Select(m => m.Value).Distinct());
        foreach (var name in new[] { "Marieke", "Lotte", "Pieter", "Yasmine", "Tom", "Jens", "phpMyAdmin", "MD5", "KBC" })
            Assert.DoesNotMatch(new Regex($@"\b{name}\b", RegexOptions.IgnoreCase), text);   // whole words: "customer" is fine
    }

    [Fact]
    public void Faq_MissingFile_404() => Assert.IsType<NotFound>(PublicApi.Faq(Path.GetTempPath()));

    [Fact]
    public void AssistantWorkflow_UsesTheDocumentedPrompt_AndOnlyPublicTools()
    {
        var root = RepoRoot();
        var workflow = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "n8n", "workflows", "loopcasts-assistant.json")));
        var nodes = workflow.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var agent = nodes.Single(n => n.GetProperty("type").GetString()!.EndsWith(".agent"));
        var prompt = agent.GetProperty("parameters").GetProperty("options").GetProperty("systemMessage").GetString();

        Assert.Equal(File.ReadAllText(Path.Combine(root, "n8n", "knowledge", "system-prompt.md")).Trim(), prompt);
        var toolUrls = nodes.Where(n => n.GetProperty("type").GetString() == "n8n-nodes-base.httpRequestTool")
            .Select(n => n.GetProperty("parameters").GetProperty("url").GetString()!).ToList();
        Assert.Equal(3, toolUrls.Count);
        Assert.All(toolUrls, u => Assert.Contains("/api/public/", u));   // never /api/automation or customer data
    }
}
