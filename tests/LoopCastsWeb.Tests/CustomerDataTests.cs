using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LoopCastsWeb.Tests;

public class CustomerDataTests
{
    // Two paid orders: Ann (yearly) and Bob (team). Bob's colleague Cleo has a seat on Bob's order.
    private static async Task<AppDbContext> Seed()
    {
        var sp = TestDb.Services();
        var db = sp.GetRequiredService<AppDbContext>();
        var payments = sp.GetRequiredService<PaymentService>();
        foreach (var (email, plan) in new[] { ("Ann@Example.com", "yearly"), ("bob@example.com", "team") })
        {
            var o = new Order { Name = email, Email = email, Country = "BE", PlanCode = plan, Price = Plans.Find(plan)!.Price, AccessKey = "k" };
            db.Orders.Add(o);
            await db.SaveChangesAsync();
            await payments.MarkPaidAsync(o.Id);
        }
        var bobsOrder = db.Orders.Single(o => o.Email == "bob@example.com").Id;
        db.Accounts.Add(new Account { OrderId = bobsOrder, Email = "cleo@example.com", StartsOn = DateOnly.FromDateTime(DateTime.UtcNow) });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Invoices_OnlyOwn_CaseInsensitive()
    {
        var db = await Seed();

        var ann = await CustomerData.InvoicesAsync(db, "ann@example.com");

        Assert.Single(ann);
        Assert.Equal("Yearly subscription", ann[0].Description);
    }

    [Fact]
    public async Task Invoice_OtherCustomersNumber_IsNull_AdminSeesIt()
    {
        var db = await Seed();
        var bobsNumber = db.Invoices.Single(i => i.BillingName == "bob@example.com").Number;

        Assert.Null(await CustomerData.InvoiceAsync(db, bobsNumber, "ann@example.com", isAdmin: false));
        Assert.Null(await CustomerData.InvoiceAsync(db, bobsNumber, null, isAdmin: false));
        Assert.NotNull(await CustomerData.InvoiceAsync(db, bobsNumber, "BOB@example.com", isAdmin: false));
        Assert.NotNull(await CustomerData.InvoiceAsync(db, bobsNumber, "staff@loopcasts.be", isAdmin: true));
    }

    [Fact]
    public async Task TeamSeat_SeesOwnSubscription_NotTheBuyersInvoice()
    {
        var db = await Seed();

        var subs = await CustomerData.SubscriptionsAsync(db, "cleo@example.com");

        Assert.Equal("Team licence", Assert.Single(subs).PlanName);
        Assert.Empty(await CustomerData.InvoicesAsync(db, "cleo@example.com"));
    }

    [Theory]
    [InlineData("ann@example.com", "Ann@Example.com")]    // name on the order (Seed uses the e-mail as name)
    [InlineData("cleo.peeters@example.com", "Cleo")]      // never ordered → e-mail before the @, first part
    public async Task Name_FromOrder_ElseFromEmail(string email, string name)
    {
        var db = await Seed();

        Assert.Equal(name, await CustomerData.NameAsync(db, email));
    }

    [Fact]
    public async Task MoveToEmail_OrdersSeatsAndInvoicesFollow()
    {
        var db = await Seed();

        await CustomerData.MoveToEmailAsync(db, "ANN@example.com", "ann@new.be");
        db.ChangeTracker.Clear();   // ExecuteUpdate writes straight to the database

        Assert.Single(await CustomerData.InvoicesAsync(db, "ann@new.be"));
        Assert.Single(await CustomerData.SubscriptionsAsync(db, "ann@new.be"));
        Assert.Empty(await CustomerData.SubscriptionsAsync(db, "ann@example.com"));
    }
}
