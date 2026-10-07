using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoopCastsWeb.Tests;

public class PaymentServiceTests
{
    private static Order NewOrder(string plan = "yearly", string country = "BE") => new()
    {
        Name = "Test Customer", Email = "test@example.com", Country = country,
        PlanCode = plan, Price = Plans.Find(plan)!.Price, CreatedAt = DateTime.UtcNow,
    };

    private static async Task<int> AddOrder(LoopCastsWeb.Data.AppDbContext db, Order order)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    [Fact]
    public async Task MarkPaid_NumbersInvoicesInSequencePerYear()
    {
        using var db = TestDb.Create();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
        var year = DateTime.UtcNow.Year;

        for (var i = 0; i < 3; i++)
            await service.MarkPaidAsync(await AddOrder(db, NewOrder()));

        var numbers = await db.Invoices.OrderBy(i => i.Id).Select(i => i.Number).ToListAsync();
        Assert.Equal([$"{year}-0001", $"{year}-0002", $"{year}-0003"], numbers);
    }

    [Fact]
    public async Task MarkPaid_ContinuesAfterLastNumberOfThisYear_IgnoresOtherYears()
    {
        using var db = TestDb.Create();
        var year = DateTime.UtcNow.Year;
        db.Invoices.Add(new Invoice { OrderId = 900, Number = $"{year - 1}-0099", BillingName = "x", BillingCountry = "BE" });
        db.Invoices.Add(new Invoice { OrderId = 901, Number = $"{year}-0041", BillingName = "x", BillingCountry = "BE" });
        await db.SaveChangesAsync();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);

        await service.MarkPaidAsync(await AddOrder(db, NewOrder()));

        Assert.True(await db.Invoices.AnyAsync(i => i.Number == $"{year}-0042"));
    }

    [Fact]
    public async Task MarkPaid_Twice_CreatesOneInvoiceAndOneAccount()
    {
        using var db = TestDb.Create();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
        var id = await AddOrder(db, NewOrder());

        Assert.True(await service.MarkPaidAsync(id));
        Assert.True(await service.MarkPaidAsync(id));   // webhook sent twice / double click

        Assert.Equal(1, await db.Invoices.CountAsync());
        Assert.Equal(1, await db.Accounts.CountAsync());
    }

    [Fact]
    public async Task MarkPaid_UnknownOrder_ReturnsFalse()
    {
        using var db = TestDb.Create();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);

        Assert.False(await service.MarkPaidAsync(12345));
    }

    [Fact]
    public async Task MarkPaid_SnapshotsBillingAndVat_AndSetsAccessEnd()
    {
        using var db = TestDb.Create();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);
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
        using var db = TestDb.Create();
        var service = new PaymentService(db, NullLogger<PaymentService>.Instance);

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
}
