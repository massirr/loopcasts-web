using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LoopCastsWeb.Tests;

public class PaymentServiceTests
{
    private static Order NewOrder(string plan = "yearly", string country = "BE") => new()
    {
        Name = "Test Customer", Email = "test@example.com", Country = country,
        PlanCode = plan, Price = Plans.Find(plan)!.Price, CreatedAt = DateTime.UtcNow,
    };

    private static (LoopCastsWeb.Data.AppDbContext, PaymentService) Setup()
    {
        var sp = TestDb.Services();
        return (sp.GetRequiredService<LoopCastsWeb.Data.AppDbContext>(), sp.GetRequiredService<PaymentService>());
    }

    private static async Task<int> AddOrder(LoopCastsWeb.Data.AppDbContext db, Order order)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    [Fact]
    public async Task MarkPaid_NumbersInvoicesInSequencePerYear()
    {
        var (db, service) = Setup();
        var year = DateTime.UtcNow.Year;

        for (var i = 0; i < 3; i++)
            await service.MarkPaidAsync(await AddOrder(db, NewOrder()));

        var numbers = await db.Invoices.OrderBy(i => i.Id).Select(i => i.Number).ToListAsync();
        Assert.Equal([$"{year}-0001", $"{year}-0002", $"{year}-0003"], numbers);
    }

    [Fact]
    public async Task MarkPaid_ContinuesAfterLastNumberOfThisYear_IgnoresOtherYears()
    {
        var (db, service) = Setup();
        var year = DateTime.UtcNow.Year;
        db.Invoices.Add(new Invoice { OrderId = 900, Number = $"{year - 1}-0099", BillingName = "x", BillingCountry = "BE" });
        db.Invoices.Add(new Invoice { OrderId = 901, Number = $"{year}-0041", BillingName = "x", BillingCountry = "BE" });
        await db.SaveChangesAsync();

        await service.MarkPaidAsync(await AddOrder(db, NewOrder()));

        Assert.True(await db.Invoices.AnyAsync(i => i.Number == $"{year}-0042"));
    }

    [Fact]
    public async Task MarkPaid_Twice_CreatesOneInvoiceAndOneAccount()
    {
        var (db, service) = Setup();
        var id = await AddOrder(db, NewOrder());

        Assert.True(await service.MarkPaidAsync(id));
        Assert.True(await service.MarkPaidAsync(id));   // webhook sent twice / double click

        Assert.Equal(1, await db.Invoices.CountAsync());
        Assert.Equal(1, await db.Accounts.CountAsync());
    }

    [Fact]
    public async Task MarkPaid_UnknownOrder_ReturnsFalse()
    {
        var (db, service) = Setup();

        Assert.False(await service.MarkPaidAsync(12345));
    }

    [Fact]
    public async Task MarkPaid_SnapshotsBillingAndVat_AndSetsAccessEnd()
    {
        var (db, service) = Setup();
        var order = NewOrder("team", "NL");
        order.IsCompany = true; order.Company = "Acme BV"; order.VatNumber = "NL123456789B01";
        var id = await AddOrder(db, order);

        await service.MarkPaidAsync(id);

        var inv = await db.Invoices.SingleAsync();
        Assert.Equal("Acme BV", inv.BillingCompany);
        Assert.Equal(599m, inv.Total);
        Assert.True(inv.ReverseCharge);
        var acc = await db.Accounts.SingleAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(today.AddMonths(12), acc.EndsOn);
        Assert.True((await db.Orders.FindAsync(id))!.IsPaid);
    }

    [Fact]
    public async Task MarkPaid_SingleCourse_LifetimeAccess()
    {
        var (db, service) = Setup();

        await service.MarkPaidAsync(await AddOrder(db, NewOrder("single")));

        Assert.Null((await db.Accounts.SingleAsync()).EndsOn);
    }

    [Fact]
    public async Task Database_RefusesDuplicateInvoiceNumber()
    {
        using var db = TestDb.Create();
        db.Invoices.Add(new Invoice { OrderId = 1, Number = "2026-0029", BillingName = "a", BillingCountry = "BE" });
        db.Invoices.Add(new Invoice { OrderId = 2, Number = "2026-0029", BillingName = "b", BillingCountry = "BE" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task MarkPaid_CreatesMemberLoginWithoutPassword()
    {
        var sp = TestDb.Services();
        var db = sp.GetRequiredService<LoopCastsWeb.Data.AppDbContext>();
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();

        await sp.GetRequiredService<PaymentService>().MarkPaidAsync(await AddOrder(db, NewOrder()));

        var user = await users.FindByEmailAsync("test@example.com");
        Assert.NotNull(user);
        Assert.False(await users.HasPasswordAsync(user));
        Assert.True(await users.IsInRoleAsync(user, MemberService.MemberRole));
    }

    [Fact]
    public async Task MarkPaid_ReturningCustomerOrPaidTwice_OneLogin()
    {
        var sp = TestDb.Services();
        var db = sp.GetRequiredService<LoopCastsWeb.Data.AppDbContext>();
        var service = sp.GetRequiredService<PaymentService>();
        var first = await AddOrder(db, NewOrder());

        await service.MarkPaidAsync(first);
        await service.MarkPaidAsync(first);
        await service.MarkPaidAsync(await AddOrder(db, NewOrder("single")));

        Assert.Equal(1, await db.Users.CountAsync());
    }
}
