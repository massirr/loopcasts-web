using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoopCastsWeb.Tests;

public class TeamSeatsTests
{
    private const string Buyer = "boss@acme.be";

    private static async Task<(AppDbContext Db, TeamSeats Seats, int OrderId)> PaidOrder(string plan = "team")
    {
        var sp = TestDb.Services();
        var db = sp.GetRequiredService<AppDbContext>();
        var order = new Order { Name = "Boss", Email = Buyer, Country = "BE", IsCompany = true, Company = "Acme", VatNumber = "BE1", PlanCode = plan, Price = Plans.Find(plan)!.Price, AccessKey = "k" };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await sp.GetRequiredService<PaymentService>().MarkPaidAsync(order.Id);
        return (db, sp.GetRequiredService<TeamSeats>(), order.Id);
    }

    [Fact]
    public async Task Add_GivesColleagueSeatWithLicenceEndDate_AndNewLogin()
    {
        var (db, seats, id) = await PaidOrder();

        var result = await seats.AddAsync(id, Buyer, " ann@acme.be ");

        Assert.Null(result.Error);
        Assert.NotNull(result.NewLogin);
        var ann = await db.Accounts.SingleAsync(a => a.Email == "ann@acme.be");
        var boss = await db.Accounts.SingleAsync(a => a.Email == Buyer);
        Assert.Equal(boss.EndsOn, ann.EndsOn);
        Assert.Equal(id, ann.OrderId);
    }

    [Fact]
    public async Task Add_MaxFiveSeatsIncludingBuyer()
    {
        var (_, seats, id) = await PaidOrder();
        for (var i = 1; i <= 4; i++)
            Assert.Null((await seats.AddAsync(id, Buyer, $"c{i}@acme.be")).Error);

        Assert.NotNull((await seats.AddAsync(id, Buyer, "c5@acme.be")).Error);
    }

    [Fact]
    public async Task Add_SameEmailTwice_Refused_CaseInsensitive()
    {
        var (_, seats, id) = await PaidOrder();
        await seats.AddAsync(id, Buyer, "ann@acme.be");

        Assert.NotNull((await seats.AddAsync(id, Buyer, "ANN@acme.be")).Error);
        Assert.NotNull((await seats.AddAsync(id, Buyer, Buyer)).Error);   // buyer already has a seat
    }

    [Fact]
    public async Task Add_ExistingLogin_NoSetupLinkForTheBuyer()
    {
        var (_, seats, id) = await PaidOrder();
        await seats.AddAsync(id, Buyer, "ann@acme.be");
        await seats.RemoveAsync(id, Buyer, (await seats.LicencesAsync(Buyer))[0].Accounts.Last().Id);

        var again = await seats.AddAsync(id, Buyer, "ann@acme.be");   // login exists already

        Assert.Null(again.Error);
        Assert.Null(again.NewLogin);
    }

    [Fact]
    public async Task Remove_FreesSeat_ButNeverOwnSeat()
    {
        var (db, seats, id) = await PaidOrder();
        await seats.AddAsync(id, Buyer, "ann@acme.be");
        var ann = await db.Accounts.SingleAsync(a => a.Email == "ann@acme.be");
        var boss = await db.Accounts.SingleAsync(a => a.Email == Buyer);

        Assert.NotNull(await seats.RemoveAsync(id, Buyer, boss.Id));
        Assert.Null(await seats.RemoveAsync(id, Buyer, ann.Id));
        Assert.False(ann.IsActive);
        Assert.Equal(1, (await seats.LicencesAsync(Buyer))[0].Used);
    }

    [Fact]
    public async Task OnlyTheBuyer_CanManage()
    {
        var (db, seats, id) = await PaidOrder();
        await seats.AddAsync(id, Buyer, "ann@acme.be");
        var ann = await db.Accounts.SingleAsync(a => a.Email == "ann@acme.be");

        Assert.NotNull((await seats.AddAsync(id, "ann@acme.be", "mallory@evil.com")).Error);   // a colleague
        Assert.NotNull(await seats.RemoveAsync(id, "mallory@evil.com", ann.Id));
        Assert.True(ann.IsActive);
    }

    [Fact]
    public async Task NoLicence_ForSingleSeatPlans_OrExpiredLicence()
    {
        var (_, yearly, yid) = await PaidOrder("yearly");
        Assert.Empty(await yearly.LicencesAsync(Buyer));
        Assert.NotNull((await yearly.AddAsync(yid, Buyer, "ann@acme.be")).Error);

        var (db, seats, id) = await PaidOrder();
        (await db.Accounts.SingleAsync()).EndsOn = new DateOnly(2020, 1, 1);
        await db.SaveChangesAsync();
        Assert.NotNull((await seats.AddAsync(id, Buyer, "ann@acme.be")).Error);
    }
}
