using System.Net;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoopCastsWeb.Tests;

/// Pretends to be an HTTP API (Stripe, n8n, VIES): answers every request with the given status + JSON, remembers what was asked.
public class FakeApi(HttpStatusCode status, string json) : HttpMessageHandler
{
    public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];
    public List<string> Headers { get; } = [];   // X-LoopCasts-Secret values

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
        if (request.Headers.TryGetValues("X-LoopCasts-Secret", out var secret)) Headers.AddRange(secret);
        return new HttpResponseMessage(status) { Content = new StringContent(json) };
    }
}

public class StripeServiceTests
{
    private const string Session = "cs_test_abc123";

    // Yearly, Belgium: 149 + 21% = 180.29 → 18029 cents
    private static async Task<(AppDbContext, StripeService, Order)> Setup(FakeApi fake, string? paymentId = Session)
    {
        var sp = TestDb.Services(fake);
        var db = sp.GetRequiredService<AppDbContext>();
        var order = new Order { Name = "Ann", Email = "ann@example.com", Country = "BE", PlanCode = "yearly", Price = 149m, AccessKey = "k", PaymentId = paymentId };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (db, sp.GetRequiredService<StripeService>(), order);
    }

    private static string Checkout(string paymentStatus, int orderId, long amount = 18029, string currency = "eur") =>
        $$$"""{"id":"{{{Session}}}","payment_status":"{{{paymentStatus}}}","amount_total":{{{amount}}},"currency":"{{{currency}}}","metadata":{"orderId":"{{{orderId}}}"}}""";

    [Fact]
    public async Task Sync_Paid_MarksOrderPaidWithInvoice()
    {
        var fake = new FakeApi(HttpStatusCode.OK, Checkout("paid", 1));
        var (db, stripe, order) = await Setup(fake);

        await stripe.SyncAsync(Session);

        Assert.True(order.IsPaid);
        Assert.Equal(180.29m, (await db.Invoices.SingleAsync()).Total);
        Assert.Equal($"/v1/checkout/sessions/{Session}", fake.Requests.Single().Path);
    }

    [Theory]
    [InlineData("unpaid", 18029, "eur", Session)]            // cancelled / not paid (yet)
    [InlineData("paid", 100, "eur", Session)]                // amount does not match the order
    [InlineData("paid", 18029, "usd", Session)]              // wrong currency
    [InlineData("paid", 18029, "eur", "cs_test_other")]      // session belongs to another checkout of this order
    public async Task Sync_DoesNotMarkPaid(string status, long amount, string currency, string storedPaymentId)
    {
        var (db, stripe, order) = await Setup(new FakeApi(HttpStatusCode.OK, Checkout(status, 1, amount, currency)), storedPaymentId);

        await stripe.SyncAsync(Session);

        Assert.False(order.IsPaid);
        Assert.Empty(db.Invoices);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tr_abc123")]                 // old Mollie id
    [InlineData("../customers")]
    [InlineData("cs_test_abc?expand=x")]
    public async Task Sync_InvalidId_DoesNotCallStripe(string? id)
    {
        var fake = new FakeApi(HttpStatusCode.OK, Checkout("paid", 1));
        var (_, stripe, order) = await Setup(fake);

        await stripe.SyncAsync(id);

        Assert.Empty(fake.Requests);
        Assert.False(order.IsPaid);
    }

    [Fact]
    public async Task Sync_UnknownSession_IsIgnored()
    {
        var (_, stripe, order) = await Setup(new FakeApi(HttpStatusCode.NotFound, "{}"));

        await stripe.SyncAsync("cs_test_unknown");

        Assert.False(order.IsPaid);
    }

    [Fact]
    public async Task Create_SendsTotalInCents_StoresSessionId_ReturnsCheckoutUrl()
    {
        var fake = new FakeApi(HttpStatusCode.OK, """{"id":"cs_test_new1","url":"https://checkout.stripe.com/c/pay/test"}""");
        var (_, stripe, order) = await Setup(fake, paymentId: null);

        var url = await stripe.CreateCheckoutAsync(order, "https://shop/Pay?id=1&key=k");

        Assert.Equal("https://checkout.stripe.com/c/pay/test", url);
        Assert.Equal("cs_test_new1", order.PaymentId);
        var request = fake.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/checkout/sessions", request.Path);
        Assert.Contains("unit_amount%5D=18029", request.Body);   // line_items[0][price_data][unit_amount]=18029
        Assert.Contains("currency%5D=eur", request.Body);
    }

    [Fact]
    public async Task Create_ReusesStillOpenSession()   // double click / second tab: paying the first tab must still count
    {
        var fake = new FakeApi(HttpStatusCode.OK, $$"""{"id":"{{Session}}","status":"open","url":"https://checkout.stripe.com/c/pay/first"}""");
        var (_, stripe, order) = await Setup(fake);

        var url = await stripe.CreateCheckoutAsync(order, "https://shop/Pay?id=1&key=k");

        Assert.Equal("https://checkout.stripe.com/c/pay/first", url);
        Assert.Equal(Session, order.PaymentId);
        Assert.Equal(HttpMethod.Get, fake.Requests.Single().Method);   // no new session created
    }

    [Fact]
    public async Task Sync_WithoutKey_DoesNothing()
    {
        var stripe = TestDb.Services().GetRequiredService<StripeService>();   // real HttpClient, no key: must not call out

        await stripe.SyncAsync(Session);
    }

    [Fact]
    public void Enabled_OnlyWithKey()
    {
        Assert.False(TestDb.Services().GetRequiredService<StripeService>().Enabled);
        Assert.True(TestDb.Services(new FakeApi(HttpStatusCode.OK, "{}")).GetRequiredService<StripeService>().Enabled);
    }

    [Theory]
    [InlineData("""{"type":"checkout.session.completed","data":{"object":{"id":"cs_test_1","payment_status":"paid"}}}""", "cs_test_1")]
    [InlineData("""{"type":"ping"}""", null)]
    [InlineData("not json", null)]
    [InlineData("", null)]
    public void SessionIdFromEvent_OnlyTakesTheId(string json, string? expected) =>
        Assert.Equal(expected, StripeService.SessionIdFromEvent(json));
}
