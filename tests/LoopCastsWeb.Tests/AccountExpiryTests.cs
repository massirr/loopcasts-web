using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Tests;

public class AccountExpiryTests
{
    [Fact]
    public async Task Run_DeactivatesOnlyActiveAccountsPastEndDate()
    {
        using var db = TestDb.Create();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Accounts.AddRange(
            new Account { OrderId = 1, Email = "expired@x.be", EndsOn = today.AddDays(-1), IsActive = true },
            new Account { OrderId = 2, Email = "endstoday@x.be", EndsOn = today, IsActive = true },
            new Account { OrderId = 3, Email = "future@x.be", EndsOn = today.AddMonths(1), IsActive = true },
            new Account { OrderId = 4, Email = "lifetime@x.be", EndsOn = null, IsActive = true },
            new Account { OrderId = 5, Email = "already@x.be", EndsOn = today.AddDays(-30), IsActive = false });
        await db.SaveChangesAsync();

        var count = await AccountExpiry.RunAsync(db);

        Assert.Equal(1, count);
        var active = await db.Accounts.Where(a => a.IsActive).Select(a => a.Email).OrderBy(e => e).ToListAsync();
        Assert.Equal(["endstoday@x.be", "future@x.be", "lifetime@x.be"], active);
    }

    [Fact]
    public async Task Run_SecondTime_DeactivatesNothing()
    {
        using var db = TestDb.Create();
        db.Accounts.Add(new Account { OrderId = 1, Email = "a@x.be", EndsOn = new DateOnly(2020, 1, 1), IsActive = true });
        await db.SaveChangesAsync();

        Assert.Equal(1, await AccountExpiry.RunAsync(db));
        Assert.Equal(0, await AccountExpiry.RunAsync(db));
    }
}
