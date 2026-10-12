using System.Net;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static LoopCastsWeb.Services.OldDataImport;

namespace LoopCastsWeb.Tests;

public class OldMemberImportTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private const string Hash = "5f4dcc3b5aa765d61d8327deb882cf99";   // fake MD5, must never come out again

    // Made-up rows in the phpMyAdmin format of the old site (no real members)
    private const string Sql = """
        -- phpMyAdmin SQL Dump
        CREATE TABLE `users` (
          `id` int(11) NOT NULL AUTO_INCREMENT,
          `email` varchar(255) NOT NULL,
          `password` varchar(32) NOT NULL COMMENT 'md5',
          `product` varchar(20) DEFAULT NULL,
          `start_date` date DEFAULT NULL,
          `end_date` date DEFAULT NULL,
          `active` tinyint(1) NOT NULL DEFAULT '1'
        ) ENGINE=MyISAM DEFAULT CHARSET=latin1;

        INSERT INTO `users` (`id`, `email`, `password`, `product`, `start_date`, `end_date`, `active`) VALUES
        (1, 'ann@example.com', '5f4dcc3b5aa765d61d8327deb882cf99', 'yearly', '2026-01-10', '2027-01-10', 1),
        (2, 'Bob@Example.com ', 'it''s,a)weird\'hash', 'monthly', '2024-03-14', NULL, 1),
        (3, 'cleo@example.com', '5f4dcc3b5aa765d61d8327deb882cf99', 'team', '2025-02-01', '2026-02-01', 1),
        (4, 'cleo@example.com', '5f4dcc3b5aa765d61d8327deb882cf99', 'team', '2024-02-01', '2025-02-01', 0),
        (5, 'dirk@example.com', '5f4dcc3b5aa765d61d8327deb882cf99', 'single', '2023-05-05', NULL, 1),
        (6, 'eva@example.com', '5f4dcc3b5aa765d61d8327deb882cf99', 'yearly', '2023-01-01', '2024-01-01', 0),
        (7, 'not-an-email', '5f4dcc3b5aa765d61d8327deb882cf99', 'yearly', '2026-01-01', '2027-01-01', 1),
        (8, 'fien@example.com', '5f4dcc3b5aa765d61d8327deb882cf99', 'gold', '2026-01-01', '2027-01-01', 1);
        """;

    private static OldMemberImport Parse() => OldMemberImport.ParseSql(Sql, Today);

    [Fact]
    public void PasswordColumn_NeverRead_NotInRowsReportOrCsv()
    {
        var p = Parse();

        var everything = string.Join("|", p.Rows.Select(r => r.ToString())) + p.ToCsv()
            + string.Join("|", p.Report.Select(l => l.Message)) + string.Join("|", p.Members.Select(m => m.ToString()));
        Assert.DoesNotContain(Hash, everything);
        Assert.DoesNotContain("weird", everything);                   // even a hash with quotes, commas and ')' in it
        Assert.DoesNotContain("password", p.ToCsv().Split('\n')[0]);
        Assert.Equal(8, p.Rows.Count);
    }

    [Fact]
    public void Members_CleanedMappedAndDeduplicated()
    {
        var p = Parse();

        Assert.Equal(["ann@example.com", "bob@example.com", "cleo@example.com", "dirk@example.com", "eva@example.com"],
            p.Members.Select(m => m.Email));
        Assert.Equal(3, p.Members.Single(m => m.Email == "cleo@example.com").Row);   // duplicate: the active row wins
        Assert.Contains(p.Report, l => l.Level == Level.Skipped && l.Message.Contains("not-an-email"));
        Assert.Contains(p.Report, l => l.Level == Level.Skipped && l.Message.Contains("'gold' unknown"));
        Assert.Contains(p.Report, l => l.Level == Level.Skipped && l.Where == "users row 4");
    }

    [Fact]
    public void EndDates_SingleLifetime_MonthlyToNextRenewal()
    {
        var p = Parse();

        Assert.Null(p.Members.Single(m => m.Email == "dirk@example.com").EndsOn);
        Assert.Equal(new DateOnly(2026, 10, 14), p.Members.Single(m => m.Email == "bob@example.com").EndsOn);
        Assert.True(p.Members.Single(m => m.Email == "bob@example.com").IsActive);
    }

    [Theory]
    [InlineData("2024-03-14", "2026-10-12", "2026-10-14")]
    [InlineData("2024-03-14", "2026-10-14", "2026-10-14")]   // renewal day itself
    [InlineData("2024-03-14", "2026-10-20", "2026-11-14")]
    [InlineData("2024-01-31", "2026-02-10", "2026-02-28")]   // short month
    public void NextRenewal(string start, string today, string next) =>
        Assert.Equal(DateOnly.Parse(next), OldMemberImport.NextRenewal(DateOnly.Parse(start), DateOnly.Parse(today)));

    [Fact]
    public void ActiveButEnded_ListForLotte_WithYearlyValue()
    {
        var p = Parse();

        var overdue = Assert.Single(p.ActiveButEnded);
        Assert.Equal(("cleo@example.com", 119.80m), (overdue.Email, overdue.YearlyValue));   // one team seat = 599 / 5
        Assert.False(p.Members.Single(m => m.Email == "cleo@example.com").IsActive);   // imported as ended
        Assert.Equal(2, p.Expired);                                                     // cleo + eva
        Assert.Equal(180m, OldMemberImport.YearlyValue(Plans.Find("monthly")!));
        Assert.Equal(149m, OldMemberImport.YearlyValue(Plans.Find("yearly")!));
        Assert.Equal(0m, OldMemberImport.YearlyValue(Plans.Find("single")!));
    }

    [Fact]
    public void CsvRoundTrip_SameMembersAsTheSql()
    {
        var p = Parse();

        var again = OldMemberImport.ParseCsv(p.ToCsv(), Today);

        Assert.Equal(p.Members, again.Members);
        Assert.Equal(p.Report, again.Report);
    }

    [Fact]
    public async Task Apply_AccountsAndLoginsWithoutPassword_SkipsExisting_NoEmails()
    {
        var n8n = new FakeMollie(HttpStatusCode.OK, "{}");
        var sp = TestDb.Services(n8n: n8n, config: new() { ["N8n:WebhookUrl"] = "https://n8n.test/x" });
        var db = sp.GetRequiredService<AppDbContext>();
        db.Accounts.Add(new Account { OrderId = 99, Email = "ann@example.com", StartsOn = Today });   // e.g. from an imported order
        await db.SaveChangesAsync();
        var members = sp.GetRequiredService<MemberService>();

        var first = await Parse().ApplyAsync(db, members);
        var second = await Parse().ApplyAsync(db, members);

        Assert.Equal((4, 1), first);
        Assert.Equal((0, 5), second);
        var bob = await db.Accounts.SingleAsync(a => a.Email == "bob@example.com");
        Assert.Equal(("monthly", (int?)null), (bob.PlanCode, bob.OrderId));
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByEmailAsync("bob@example.com"))!;
        Assert.False(await users.HasPasswordAsync(user));
        Assert.True(await users.IsInRoleAsync(user, MemberService.MemberRole));
        Assert.Empty(n8n.Requests);                                                    // nobody is e-mailed
        Assert.Equal("Monthly subscription", (await CustomerData.SubscriptionsAsync(db, "bob@example.com")).Single().PlanName);
    }
}
