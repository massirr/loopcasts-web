using System.Net;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LoopCastsWeb.Tests;

public class N8nNotifierTests
{
    private static readonly Dictionary<string, string?> Config = new()
    {
        ["N8n:WebhookUrl"] = "https://n8n.test/webhook/loopcasts",
        ["N8n:Secret"] = "s3cret",
        ["App:BaseUrl"] = "https://shop.test/",
    };

    private static async Task<(IServiceProvider Sp, int OrderId)> PaidOrder(FakeMollie n8n, Dictionary<string, string?>? config = null)
    {
        var sp = TestDb.Services(n8n: n8n, config: config ?? Config);
        var db = sp.GetRequiredService<AppDbContext>();
        var order = new Order { Name = "Ann", Email = "ann@example.com", Country = "BE", PlanCode = "yearly", Price = 149m, AccessKey = "k" };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await sp.GetRequiredService<PaymentService>().MarkPaidAsync(order.Id);
        return (sp, order.Id);
    }

    [Fact]
    public async Task Paid_SendsInvoiceEventOnce_WithLinksAndSecret()
    {
        var n8n = new FakeMollie(HttpStatusCode.OK, "{}");
        var (sp, id) = await PaidOrder(n8n);
        await sp.GetRequiredService<PaymentService>().MarkPaidAsync(id);   // webhook retry: no 2nd e-mail

        var body = Assert.Single(n8n.Requests).Body!;
        Assert.Contains("\"event\":\"invoice.created\"", body);
        Assert.Contains("\"to\":\"ann@example.com\"", body);
        Assert.Contains("https://shop.test/Invoice?number=", body);
        Assert.Contains("https://shop.test/Identity/Account/ResetPassword?code=", body);   // new customer
        Assert.Equal("s3cret", n8n.Headers.Single());
    }

    [Fact]
    public async Task ReturningCustomerWithPassword_NoSetupLink()
    {
        var n8n = new FakeMollie(HttpStatusCode.OK, "{}");
        var (sp, _) = await PaidOrder(n8n);
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        await users.AddPasswordAsync((await users.FindByEmailAsync("ann@example.com"))!, "Test-pass1!");
        var db = sp.GetRequiredService<AppDbContext>();
        var second = new Order { Name = "Ann", Email = "ann@example.com", Country = "BE", PlanCode = "single", Price = 39m, AccessKey = "k" };
        db.Orders.Add(second);
        await db.SaveChangesAsync();

        await sp.GetRequiredService<PaymentService>().MarkPaidAsync(second.Id);

        Assert.Equal(2, n8n.Requests.Count);
        Assert.Contains("\"setPasswordUrl\":null", n8n.Requests[1].Body);
    }

    [Fact]
    public async Task N8nDown_PaymentStillSucceeds()
    {
        var (sp, id) = await PaidOrder(new FakeMollie(HttpStatusCode.InternalServerError, "boom"));

        Assert.True((await sp.GetRequiredService<AppDbContext>().Orders.FindAsync(id))!.IsPaid);
        Assert.Single(sp.GetRequiredService<AppDbContext>().Invoices);
    }

    [Fact]
    public async Task NotConfigured_SendsNothing()
    {
        var n8n = new FakeMollie(HttpStatusCode.OK, "{}");
        await PaidOrder(n8n, config: []);

        Assert.Empty(n8n.Requests);
    }

    [Fact]
    public async Task ForgotPassword_GoesThroughN8nAsEmailEvent()
    {
        var n8n = new FakeMollie(HttpStatusCode.OK, "{}");
        var sp = TestDb.Services(n8n: n8n, config: Config);

        await sp.GetRequiredService<N8nNotifier>().SendEmailAsync("ann@example.com", "Reset Password", "<a href='x'>reset</a>");

        Assert.Contains("\"event\":\"email\"", Assert.Single(n8n.Requests).Body);
    }

    [Theory]
    [InlineData("Reset your password")]   // Identity UI 10 (the real one, seen in Mailpit)
    [InlineData("Reset Password")]        // older Identity versions
    public async Task IdentityEmail_InOurWords_KeepsTheLink(string identitySubject)
    {
        var n8n = new FakeMollie(HttpStatusCode.OK, "{}");
        var sp = TestDb.Services(n8n: n8n, config: Config);

        await sp.GetRequiredService<N8nNotifier>().SendEmailAsync("ann@example.com", identitySubject,
            "Please reset your password by <a href='https://shop.test/reset?code=abc'>clicking here</a>.");

        var body = Assert.Single(n8n.Requests).Body!;
        Assert.Contains("\"subject\":\"Choose a new LoopCasts password\"", body);
        Assert.Contains("https://shop.test/reset?code=abc", body);
        Assert.DoesNotContain("clicking here", body);
    }

    [Fact]
    public async Task UnknownEmail_GoesOutUnchanged()
    {
        var n8n = new FakeMollie(HttpStatusCode.OK, "{}");
        var sp = TestDb.Services(n8n: n8n, config: Config);

        await sp.GetRequiredService<N8nNotifier>().SendEmailAsync("ann@example.com", "Hello", "plain text");

        Assert.Contains("\"subject\":\"Hello\"", Assert.Single(n8n.Requests).Body);
    }
}
