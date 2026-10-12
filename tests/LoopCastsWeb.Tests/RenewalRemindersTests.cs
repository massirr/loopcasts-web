using LoopCastsWeb.Api;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoopCastsWeb.Tests;

public class RenewalRemindersTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private const string Base = "https://shop.test";

    // Ann (yearly, ends in 5 days), Bob (team buyer, ends in 10 days) + his colleague Cleo, Dirk (ends in 30 days),
    // Eva (lifetime), Fien (old member without order, monthly, ends in 3 days), Gert (inactive, ends in 2 days)
    private static async Task<AppDbContext> Seed()
    {
        var db = TestDb.Create();
        Order O(string name, string email, string plan) => new() { Name = name, Email = email, Country = "BE", PlanCode = plan, AccessKey = "k", IsPaid = true };
        var orders = new[] { O("Ann Peeters", "ann@example.com", "yearly"), O("Bob Maes", "bob@example.com", "team"),
                             O("Dirk", "dirk@example.com", "yearly"), O("Eva", "eva@example.com", "single"), O("Gert", "gert@example.com", "yearly") };
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
        Account A(Order? o, string email, int? days, bool active = true, string? plan = null) =>
            new() { OrderId = o?.Id, PlanCode = plan, Email = email, StartsOn = Today.AddYears(-1), EndsOn = days is int d ? Today.AddDays(d) : null, IsActive = active };
        db.Accounts.AddRange(A(orders[0], "ann@example.com", 5), A(orders[1], "bob@example.com", 10), A(orders[1], "cleo@example.com", 10),
            A(orders[2], "dirk@example.com", 30), A(orders[3], "eva@example.com", null), A(null, "fien.claes@example.com", 3, plan: "monthly"),
            A(orders[4], "gert@example.com", 2, active: false));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Due_ActiveEndingWithinDays_BuyersOnly()
    {
        var db = await Seed();

        var due = await RenewalReminders.DueAsync(db, Today, 14, Base);

        Assert.Equal(["fien.claes@example.com", "ann@example.com", "bob@example.com"], due.Select(d => d.Email));   // soonest first
        var ann = due.Single(d => d.Email == "ann@example.com");
        Assert.Equal(("Ann", "Yearly subscription", "https://shop.test/Order?plan=yearly"), (ann.Name, ann.Plan, ann.RenewUrl));
        Assert.Equal("Fien", due[0].Name);   // old member without order: name from the e-mail
    }

    [Fact]
    public async Task Reminded_NotAgainForTheSameEndDate_ButAgainAfterRenewal()
    {
        var db = await Seed();
        var ann = (await RenewalReminders.DueAsync(db, Today, 14, Base)).Single(d => d.Email == "ann@example.com");

        Assert.True(await RenewalReminders.MarkAsync(db, ann.AccountId, ann.EndsOn));
        Assert.True(await RenewalReminders.MarkAsync(db, ann.AccountId, ann.EndsOn));   // twice = no harm
        Assert.DoesNotContain(await RenewalReminders.DueAsync(db, Today, 14, Base), d => d.Email == "ann@example.com");

        (await db.Accounts.FindAsync(ann.AccountId))!.EndsOn = Today.AddDays(7);        // renewed / changed end date
        await db.SaveChangesAsync();
        Assert.Contains(await RenewalReminders.DueAsync(db, Today, 14, Base), d => d.Email == "ann@example.com");
        Assert.False(await RenewalReminders.MarkAsync(db, 9999, Today));
    }

    // ---------- the HTTP endpoints: secret header ----------

    private static IConfiguration Config(string? secret) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["N8n:Secret"] = secret, ["App:BaseUrl"] = Base }).Build();

    private static HttpRequest Request(string? header)
    {
        var ctx = new DefaultHttpContext();
        if (header is not null) ctx.Request.Headers[AutomationApi.SecretHeader] = header;
        return ctx.Request;
    }

    [Theory]
    [InlineData("s3cret", null)]        // no header
    [InlineData("s3cret", "wrong")]
    [InlineData("s3cret", "s3cre")]     // almost
    [InlineData(null, "")]              // no secret configured = closed, even with an empty header
    [InlineData("", "")]
    public async Task Endpoints_WithoutTheRightSecret_401(string? secret, string? header)
    {
        var db = await Seed();

        Assert.IsType<UnauthorizedHttpResult>(await AutomationApi.Expiring(Request(header), Config(secret), db, 14));
        Assert.IsType<UnauthorizedHttpResult>(await AutomationApi.Reminded(Request(header), Config(secret), db, new(1, Today)));
    }

    [Fact]
    public async Task Endpoints_WithTheSecret_ListThenMark()
    {
        var db = await Seed();

        var list = Assert.IsType<Ok<List<RenewalReminders.Due>>>(await AutomationApi.Expiring(Request("s3cret"), Config("s3cret"), db, 4)).Value!;
        var fien = Assert.Single(list);   // 4 days: only Fien
        Assert.IsType<NoContent>(await AutomationApi.Reminded(Request("s3cret"), Config("s3cret"), db, new(fien.AccountId, fien.EndsOn)));
        Assert.IsType<NotFound>(await AutomationApi.Reminded(Request("s3cret"), Config("s3cret"), db, new(9999, fien.EndsOn)));
        Assert.Empty(Assert.IsType<Ok<List<RenewalReminders.Due>>>(await AutomationApi.Expiring(Request("s3cret"), Config("s3cret"), db, 4)).Value!);
    }
}
