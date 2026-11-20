using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Pages;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace LoopCastsWeb.Tests;

/// #60: without a Stripe key the mock checkout works only in Development; elsewhere the order stays unpaid.
public class PayPageTests
{
    private readonly ServiceProvider _sp = TestDb.Services();   // no Stripe key
    private AppDbContext Db => _sp.GetRequiredService<AppDbContext>();

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "LoopCastsWeb";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private PayModel Page(string environment) =>
        new(Db, _sp.GetRequiredService<PaymentService>(), _sp.GetRequiredService<StripeService>(), new Env(environment))
        {
            PageContext = new(new ActionContext(new DefaultHttpContext(), new RouteData(), new CompiledPageActionDescriptor()))
        };

    private async Task<Order> NewOrder()
    {
        var course = new Course { Title = "n8n basics", Status = "live" };
        var order = new Order { Name = "Ann", Email = "ann@example.com", Country = "BE", PlanCode = "single", Course = course, AccessKey = "k" };
        Db.Orders.Add(order);
        await Db.SaveChangesAsync();
        return order;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Outside_development_mock_payment_is_refused(string environment)
    {
        var order = await NewOrder();
        var page = Page(environment);

        var result = await page.OnPostAsync(order.Id, "k");   // posted directly, e.g. with curl

        Assert.IsType<PageResult>(result);
        Assert.True(page.Unavailable);
        Assert.False((await Db.Orders.AsNoTracking().SingleAsync()).IsPaid);
        Assert.Empty(Db.Invoices);
        Assert.Empty(Db.Accounts);
    }

    [Fact]
    public async Task Outside_development_pay_page_shows_unavailable()
    {
        var order = await NewOrder();
        var page = Page("Production");

        Assert.IsType<PageResult>(await page.OnGetAsync(order.Id, "k"));
        Assert.True(page.Unavailable);
    }

    [Fact]
    public async Task In_development_mock_payment_still_works()
    {
        var order = await NewOrder();
        var page = Page("Development");

        var result = await page.OnPostAsync(order.Id, "k");

        Assert.IsType<RedirectToPageResult>(result);
        Assert.False(page.Unavailable);
        Assert.True((await Db.Orders.AsNoTracking().SingleAsync()).IsPaid);
    }
}
