using System.Net;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoopCastsWeb.Tests;

/// Pretends to be api.mollie.com: answers every request with the given status + JSON, remembers what was asked.
public class FakeMollie(HttpStatusCode status, string json) : HttpMessageHandler
{
    public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
        return new HttpResponseMessage(status) { Content = new StringContent(json) };
    }
}

public class MollieServiceTests
{
    // Yearly, Belgium: 149 + 21% = 180.29
    private static async Task<(AppDbContext, MollieService, Order)> Setup(FakeMollie fake, string? paymentId = "tr_abc123")
    {
        var sp = TestDb.Services(fake);
        var db = sp.GetRequiredService<AppDbContext>();
        var order = new Order { Name = "Ann", Email = "ann@example.com", Country = "BE", PlanCode = "yearly", Price = 149m, AccessKey = "k", MolliePaymentId = paymentId };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (db, sp.GetRequiredService<MollieService>(), order);
    }

    private static string Payment(string status, int orderId, string amount = "180.29") =>
        $$$"""{"id":"tr_abc123","status":"{{{status}}}","amount":{"currency":"EUR","value":"{{{amount}}}"},"metadata":{"orderId":"{{{orderId}}}"}}""";

    [Fact]
    public async Task Sync_Paid_MarksOrderPaidWithInvoice()
    {
        var fake = new FakeMollie(HttpStatusCode.OK, Payment("paid", 1));
        var (db, mollie, order) = await Setup(fake);

        await mollie.SyncAsync("tr_abc123");

        Assert.True(order.IsPaid);
        Assert.Equal(180.29m, (await db.Invoices.SingleAsync()).Total);
        Assert.Equal("/v2/payments/tr_abc123", fake.Requests.Single().Path);
    }

    [Theory]
    [InlineData("open", "180.29", "tr_abc123")]       // not paid (yet)
    [InlineData("canceled", "180.29", "tr_abc123")]
    [InlineData("paid", "1.00", "tr_abc123")]         // amount does not match the order
    [InlineData("paid", "180.29", "tr_other")]        // payment belongs to another checkout of this order
    public async Task Sync_DoesNotMarkPaid(string status, string amount, string storedPaymentId)
    {
        var (db, mollie, order) = await Setup(new FakeMollie(HttpStatusCode.OK, Payment(status, 1, amount)), storedPaymentId);

        await mollie.SyncAsync("tr_abc123");

        Assert.False(order.IsPaid);
        Assert.Empty(db.Invoices);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../customers")]
    [InlineData("tr_abc?x=1")]
    public async Task Sync_InvalidId_DoesNotCallMollie(string? id)
    {
        var fake = new FakeMollie(HttpStatusCode.OK, Payment("paid", 1));
        var (_, mollie, order) = await Setup(fake);

        await mollie.SyncAsync(id);

        Assert.Empty(fake.Requests);
        Assert.False(order.IsPaid);
    }

    [Fact]
    public async Task Sync_UnknownPayment_IsIgnored()
    {
        var (_, mollie, order) = await Setup(new FakeMollie(HttpStatusCode.NotFound, "{}"));

        await mollie.SyncAsync("tr_unknown");

        Assert.False(order.IsPaid);
    }

    [Fact]
    public async Task Create_SendsTotalInclVat_StoresPaymentId_ReturnsCheckout()
    {
        var fake = new FakeMollie(HttpStatusCode.Created,
            """{"id":"tr_new1","_links":{"checkout":{"href":"https://www.mollie.com/checkout/test"}}}""");
        var (_, mollie, order) = await Setup(fake, paymentId: null);

        var url = await mollie.CreatePaymentAsync(order, "https://shop/Pay?id=1&key=k");

        Assert.Equal("https://www.mollie.com/checkout/test", url);
        Assert.Equal("tr_new1", order.MolliePaymentId);
        var request = fake.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("\"value\":\"180.29\"", request.Body);
        Assert.DoesNotContain("webhookUrl", request.Body);   // no public URL configured
    }

    [Fact]
    public async Task Sync_WithoutApiKey_DoesNothing()
    {
        var mollie = TestDb.Services().GetRequiredService<MollieService>();   // real HttpClient, no key: must not call out

        await mollie.SyncAsync("tr_abc123");
    }

    [Fact]
    public void Enabled_OnlyWithApiKey()
    {
        Assert.False(TestDb.Services().GetRequiredService<MollieService>().Enabled);
        Assert.True(TestDb.Services(new FakeMollie(HttpStatusCode.OK, "{}")).GetRequiredService<MollieService>().Enabled);
    }
}
