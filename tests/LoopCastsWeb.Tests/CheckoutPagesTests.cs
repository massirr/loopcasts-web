using System.Security.Claims;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using LoopCastsWeb.Pages;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoopCastsWeb.Tests;

/// #56: the Order and OrderThanks page models called directly (no browser), like ASP.NET would after model binding.
public class CheckoutPagesTests
{
    private readonly ServiceProvider _sp = TestDb.Services();
    private AppDbContext Db => _sp.GetRequiredService<AppDbContext>();
    private UserManager<IdentityUser> Users => _sp.GetRequiredService<UserManager<IdentityUser>>();

    private static PageContext Context(ClaimsPrincipal? user = null) =>
        new(new ActionContext(new DefaultHttpContext { User = user ?? new ClaimsPrincipal() }, new RouteData(), new Microsoft.AspNetCore.Mvc.RazorPages.CompiledPageActionDescriptor()));

    private async Task<ClaimsPrincipal> LoggedIn(string email)
    {
        var user = new IdentityUser { UserName = email, Email = email };
        await Users.CreateAsync(user);
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "test"));
    }

    private async Task<(Course Live, Course Draft)> Courses()
    {
        var live = new Course { Title = "n8n basics", Status = "live" };
        var draft = new Course { Title = "Coming soon", Status = "draft" };
        Db.Courses.AddRange(live, draft);
        await Db.SaveChangesAsync();
        return (live, draft);
    }

    private OrderModel OrderPage(Order order, ClaimsPrincipal? user = null) =>
        new(Db, new ViesClient(new HttpClient(), NullLogger<ViesClient>.Instance), Users) { Order = order, PageContext = Context(user) };

    private static Order Form(string plan, int? courseId = null, string email = "ann@example.com") =>
        new() { Name = "Ann", Email = email, Country = "BE", PlanCode = plan, CourseId = courseId };

    [Fact]
    public async Task SingleCourse_WithLiveCourse_SavesTheCourse()
    {
        var (live, _) = await Courses();

        var result = await OrderPage(Form("single", live.Id)).OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(live.Id, (await Db.Orders.SingleAsync()).CourseId);
    }

    [Theory]
    [InlineData(null)]       // nothing chosen
    [InlineData(999)]        // no such course
    [InlineData(-1)]         // -1 = the draft course (id filled in below)
    public async Task SingleCourse_WithoutLiveCourse_IsRefused(int? courseId)
    {
        var (_, draft) = await Courses();
        var page = OrderPage(Form("single", courseId == -1 ? draft.Id : courseId));

        await page.OnPostAsync();

        Assert.True(page.ModelState.ContainsKey("Order.CourseId"));
        Assert.Empty(Db.Orders);
    }

    [Fact]
    public async Task OtherPlan_NeverKeepsACourse()
    {
        var (live, _) = await Courses();

        await OrderPage(Form("yearly", live.Id)).OnPostAsync();

        Assert.Null((await Db.Orders.SingleAsync()).CourseId);
    }

    [Fact]
    public async Task LoggedIn_OrderUsesTheLoginEmail_NotThePostedOne()
    {
        var user = await LoggedIn("member@example.com");

        await OrderPage(Form("yearly", email: "someone-else@example.com"), user).OnPostAsync();

        Assert.Equal("member@example.com", (await Db.Orders.SingleAsync()).Email);
    }

    [Fact]
    public async Task Thanks_LoggedInBuyer_GoesToMyCourses_NoLoginPrompt()
    {
        var user = await LoggedIn("member@example.com");
        var order = Form("yearly", email: "member@example.com");
        order.AccessKey = "k";
        Db.Orders.Add(order);
        await Db.SaveChangesAsync();
        await _sp.GetRequiredService<PaymentService>().MarkPaidAsync(order.Id);

        var page = new OrderThanksModel(Db, Users, _sp.GetRequiredService<MemberService>(), _sp.GetRequiredService<IConfiguration>())
            { PageContext = Context(user) };
        await page.OnGetAsync(order.Id, "k");

        Assert.True(page.SignedInAsBuyer);
        Assert.False(page.HasLogin);
        Assert.Null(page.PasswordSetupUrl);
    }

    [Fact]
    public async Task Get_FromCourseCard_PreselectsTheCourse()
    {
        var (live, _) = await Courses();
        var page = OrderPage(new Order());

        await page.OnGetAsync("single", live.Id);

        Assert.True(page.CourseOptions.Single(o => o.Value == live.Id.ToString()).Selected);
        Assert.Null(page.AccountEmail);   // visitor: e-mail field shown
    }

    [Fact]
    public async Task Get_LoggedIn_UsesLoginEmail_AndNameFromPaidOrderOnly()
    {
        var user = await LoggedIn("member@example.com");
        Db.Orders.AddRange(
            new Order { Name = "Real Name", Email = "member@example.com", Country = "BE", PlanCode = "yearly", IsPaid = true },
            new Order { Name = "Typed by a stranger", Email = "member@example.com", Country = "BE", PlanCode = "yearly" });
        await Db.SaveChangesAsync();
        var page = OrderPage(new Order(), user);

        await page.OnGetAsync("yearly", null);

        Assert.Equal("member@example.com", page.AccountEmail);
        Assert.Equal("Real Name", page.Order.Name);
    }

    [Fact]
    public async Task Thanks_LoggedInAsSomeoneElse_IsNotTreatedAsBuyer()
    {
        var other = await LoggedIn("other@example.com");
        await Users.CreateAsync(new IdentityUser { UserName = "ann@example.com", Email = "ann@example.com" }, "Ann-has-a-password-1");
        var order = Form("yearly", email: "ann@example.com");
        order.AccessKey = "k";
        Db.Orders.Add(order);
        await Db.SaveChangesAsync();
        await _sp.GetRequiredService<PaymentService>().MarkPaidAsync(order.Id);

        var page = new OrderThanksModel(Db, Users, _sp.GetRequiredService<MemberService>(), _sp.GetRequiredService<IConfiguration>())
            { PageContext = Context(other) };
        await page.OnGetAsync(order.Id, "k");

        Assert.False(page.SignedInAsBuyer);
        Assert.True(page.HasLogin);   // the normal "log in with ann@…" card, not someone else's My courses
    }

    [Fact]
    public async Task Invoice_NamesTheCourse()
    {
        var (live, _) = await Courses();
        var order = Form("single", live.Id);
        order.Price = 39m;
        Db.Orders.Add(order);
        await Db.SaveChangesAsync();

        await _sp.GetRequiredService<PaymentService>().MarkPaidAsync(order.Id);

        Assert.Equal("Single course: n8n basics", (await Db.Invoices.SingleAsync()).Description);
    }

    [Theory]
    [InlineData("single", "n8n basics", "Single course: n8n basics")]
    [InlineData("yearly", null, "Yearly subscription")]
    [InlineData("gone", null, "gone")]          // old plan code no longer in the list
    public void Describe(string plan, string? course, string expected) =>
        Assert.Equal(expected, Plans.Describe(plan, course));
}
