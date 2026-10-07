using LoopCastsWeb.Data;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static LoopCastsWeb.Services.OldDataImport;

namespace LoopCastsWeb.Tests;

public class OldDataImportTests
{
    // Small fake export with the same kinds of mess as the real sheets (no real customer data in the repo)
    private const string Orders = """
        date;name;email;company;vat_number;country;product;price_excl;invoice_nr;account_created;remark
        05/01/2026;Ann Peeters;ann@example.com;;;BE;Yearly subscription;149;2026-0001;red;
        5-1-26;Ruud de Vries;ruud@devries.example ;De Vries BV;NL853746291B01;NL;Yearly subscription;149;2026-0002;red;
        6 jan;Tom Maes;T.MAES@EXAMPLE.COM;;;België;Monthly subscription;€ 15;2026-0003;;
        2026-01-07;Ann Peeters;ann@example.com;;;BE;Yearly subscription;149;;;same person?
        07/01/2026;Lukas Schmidt;lukas@schmidt.example;Schmidt GmbH;;DE;Team licence;599;waiting;;VAT nr asked
        01/15/2026;Wout Hermans;wout@example,com;;;belgie;Single course;39,00;2026-0004;;
        16/01/2026;Daan Bakker;daan@bakker.example;Bakker BV;NL861234567B01;Nederland;Yearly subscription;149;2026-0005;;
        16/01/2026;Tim Vermeulen;tim@vermeulen.example;Vermeulen BV;BE 0890 345 672;BE;Yearly subscription;149;2026-0006;;PO 45-2291
        16/01/2026;Bad Date;bad@example.com;;;BE;Yearly subscription;149;2026-0009;;
        """;

    private const string Register = """
        invoice_nr;date;customer;amount_excl;vat;total;paid;remark
        2026-0001;09/01/2026;Ann Peeters;149,00;31,29;180,29;green;
        2026-0002;09/01/2026;De Vries BV;149,00;0,00;149,00;JA;
        2026-0003;2026-01-09;Tom Maes;15,00;3,15;18,51;yes;
        2026-0004;16/01/2026;Wout Hermans;39,00;8,19;47,19;;
        2026-0005;16/01/2026;Bakker BV;149,00;31,29;180,29;green;WRONG: should be reverse charge
        2026-0006;16/01/2026;Vermeulen BV;149,00;0,00;149,00;orange;paid 140
        2026-0005;23/01/2026;Bakker BV;-149,00;-31,29;-180,29;;credit note for 2026-0005
        2026-0008;23/01/2026;Bakker BV;149,00;0,00;149,00;;VAT reverse charged
        """;

    private static bool Has(OldDataImport i, Level level, string text) =>
        i.Report.Any(l => l.Level == level && l.Message.Contains(text));

    [Theory]
    [InlineData("05/01/2026", 2026, 1, 5, false)]
    [InlineData("9/1/2026", 2026, 1, 9, true)]
    [InlineData("5-1-26", 2026, 1, 5, true)]
    [InlineData("2026-01-06", 2026, 1, 6, true)]
    [InlineData("01/15/2026", 2026, 1, 15, true)]   // impossible as day/month → US
    [InlineData("6 jan", 2026, 1, 6, true)]         // no year → previous row's year
    public void ParseDate_AllOldFormats(string raw, int y, int m, int d, bool reported)
    {
        var year = 2026;
        Assert.Equal(new DateOnly(y, m, d), ParseDate(raw, ref year, out var fix));
        Assert.Equal(reported, fix is not null);
    }

    [Fact]
    public void ParseDate_Unreadable_IsNull()
    {
        var year = 2026;
        Assert.Null(ParseDate("next week", ref year, out _));
    }

    [Theory]
    [InlineData("149", 149)]
    [InlineData("€ 149", 149)]
    [InlineData("149,00", 149)]
    [InlineData("149.00", 149)]
    [InlineData("-180,29", -180.29)]
    public void Money_AllOldFormats(string raw, decimal expected) => Assert.Equal(expected, Money(raw));

    [Theory]
    [InlineData(" ruben@devries.example ", "ruben@devries.example")]
    [InlineData("T.MAES@EXAMPLE.COM", "t.maes@example.com")]
    [InlineData("wout@example,com", "wout@example.com")]
    [InlineData("no-at-sign.example.com", null)]
    public void CleanEmail(string raw, string? expected) => Assert.Equal(expected, OldDataImport.CleanEmail(raw));

    [Theory]
    [InlineData("BE", "BE")]
    [InlineData("Belgium", "BE")]
    [InlineData("België", "BE")]
    [InlineData("belgie", "BE")]
    [InlineData("Nederland", "NL")]
    [InlineData("France", "FR")]
    [InlineData("Narnia", null)]
    public void FindCountry(string raw, string? code) => Assert.Equal(code, OldDataImport.FindCountry(raw)?.Code);

    [Fact]
    public void Parse_CleansAndReports()
    {
        var i = Parse(Orders, Register);

        // 6 invoiced + Schmidt unpaid; Ann's duplicate and the row with an unknown invoice skipped
        Assert.Equal(7, i.Rows.Count);
        Assert.Single(i.Rows, r => r.Invoice is null && r.Order.Email == "lukas@schmidt.example");
        Assert.True(Has(i, Level.Skipped, "duplicate of orders line 2"));
        Assert.True(Has(i, Level.Skipped, "invoice 2026-0009 is not in the invoice register"));

        var vermeulen = i.Rows.Single(r => r.Order.Company == "Vermeulen BV");
        Assert.Equal("BE0890345672", vermeulen.Order.VatNumber);
        Assert.Equal(0m, vermeulen.Invoice!.VatAmount);                       // kept as registered...
        Assert.True(Has(i, Level.Warning, "VAT 0.00 in register, rules say 31.29"));   // ...but reported
        Assert.True(Has(i, Level.Warning, "paid column 'orange'"));
        Assert.True(Has(i, Level.Warning, "total 18.51 ≠ amount + VAT 18.15"));
        Assert.True(Has(i, Level.Fixed, "is month/day (US)"));
        Assert.True(Has(i, Level.Note, "paid column empty"));
        Assert.True(Has(i, Level.Note, "\"PO 45-2291\""));
    }

    [Fact]
    public void Parse_CreditedInvoice_ReplacedByLaterOne_CreditNoteReported()
    {
        var i = Parse(Orders, Register);

        var bakker = i.Rows.Single(r => r.Order.Company == "Bakker BV").Invoice!;
        Assert.Equal("2026-0008", bakker.Number);
        Assert.True(bakker.ReverseCharge);
        Assert.True(Has(i, Level.Warning, "credit note 2026-0005"));
        Assert.True(Has(i, Level.Warning, "used twice"));
        Assert.True(Has(i, Level.Note, "2026-0007 does not exist (gap)"));
        Assert.DoesNotContain(i.Rows, r => r.Invoice?.Number == "2026-0005");
    }

    [Fact]
    public async Task Apply_CreatesOrdersInvoicesAccountsLogins_AndIsIdempotent()
    {
        var sp = TestDb.Services();
        var db = sp.GetRequiredService<AppDbContext>();
        var members = sp.GetRequiredService<MemberService>();

        var first = await Parse(Orders, Register).ApplyAsync(db, members);
        var second = await Parse(Orders, Register).ApplyAsync(db, members);

        Assert.Equal((7, 0), first);
        Assert.Equal((0, 7), second);
        Assert.Equal(6, await db.Invoices.CountAsync());
        Assert.Equal(6, await db.Accounts.CountAsync());
        Assert.False((await db.Accounts.SingleAsync(a => a.Email == "t.maes@example.com")).IsActive);   // monthly from January
        Assert.Null((await db.Accounts.SingleAsync(a => a.Email == "wout@example.com")).EndsOn);         // single course = lifetime
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        Assert.False(await users.HasPasswordAsync((await users.FindByEmailAsync("ann@example.com"))!));
        Assert.Null(await users.FindByEmailAsync("lukas@schmidt.example"));                              // unpaid: no login
    }

    [Fact]
    public async Task Apply_NewInvoicesContinueAfterImportedNumbers()
    {
        var sp = TestDb.Services();
        var db = sp.GetRequiredService<AppDbContext>();
        await Parse(Orders, Register).ApplyAsync(db, sp.GetRequiredService<MemberService>());

        var schmidt = await db.Orders.SingleAsync(o => !o.IsPaid);
        await sp.GetRequiredService<PaymentService>().MarkPaidAsync(schmidt.Id);

        var year = DateTime.UtcNow.Year;   // imported numbers are 2026-…; a new year starts at 0001
        var expected = year == 2026 ? "2026-0009" : $"{year}-0001";
        Assert.Equal(expected, (await db.Invoices.SingleAsync(i => i.OrderId == schmidt.Id)).Number);
    }
}
