using LoopCastsWeb.Api;
using LoopCastsWeb.Data;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    // Lock the whole /Admin folder: only logged-in users with the Admin role
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    // Only admins may create new accounts (no public sign-up for staff logins)
    options.Conventions.AuthorizeAreaPage("Identity", "/Account/Register", "AdminOnly");
});
builder.Services.AddDbContext<AppDbContext>
(
    options => options.UseSqlite(builder.Configuration.GetConnectionString("Default"))
);
// Login system: personal accounts + roles, stored in our own database (AspNet* tables)
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(AdminSeeder.AdminRole)));

builder.Services.AddScoped<MemberService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<TeamSeats>();   // one instance per request, like the DbContext
// Mollie REST API. The key comes from user-secrets (Mollie:ApiKey); without a key /Pay stays a mock.
builder.Services.AddHttpClient<MollieService>(client =>
{
    client.BaseAddress = new Uri("https://api.mollie.com/v2/");
    if (builder.Configuration["Mollie:ApiKey"] is { Length: > 0 } key)
        client.DefaultRequestHeaders.Authorization = new("Bearer", key);
});
// EU VIES register for VAT numbers (#33). Vies:BaseUrl = test service in Development, the real one in Production.
// Short timeout: the customer is waiting on the order form; no answer → 21% + "not verified", never 0%.
builder.Services.AddHttpClient<ViesClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Vies:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(5);
});
// n8n webhook for the invoice e-mail (N8n:WebhookUrl)
builder.Services.AddHttpClient<N8nNotifier>(client => client.Timeout = TimeSpan.FromSeconds(10));
// Login e-mails ("Forgot password?") go straight over SMTP (Smtp:*), so they work without n8n (#27)
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IEmailSender, LoginEmailSender>();
builder.Services.AddHostedService<ExpireAccountsService>();   // nightly: switch off expired accounts

var app = builder.Build();

// On startup: apply pending migrations, then import the old course catalogue (only if the table is empty).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    // SQLite creates the file but not its folder. On Azure the db lives in /home/data (survives restarts and deploys).
    var dbFile = new SqliteConnectionStringBuilder(app.Configuration.GetConnectionString("Default")).DataSource;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbFile))!);
    await db.Database.MigrateAsync();

    var csvPath = Path.Combine(app.Environment.ContentRootPath, "Data", "Seed", "course-catalogue.csv");
    await CourseCsvImporter.SeedAsync(db, csvPath, logger);
    await AdminSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, logger);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();   // who are you? (reads the login cookie)
app.UseAuthorization();    // are you allowed? (checks the AdminOnly policy)

app.MapStaticAssets();
// Mollie calls this when a payment changes. The body only holds "id=tr_..."; MollieService asks Mollie for the
// real status, so a fake call can't mark anything paid. Not a Razor Page: Mollie can't send an antiforgery token.
app.MapPost("/webhooks/mollie", async (HttpRequest request, MollieService mollie) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync();
    await mollie.SyncAsync(form["id"].ToString());
    return Results.Ok();
});

// JSON endpoints for n8n (renewal reminders), protected by the N8n:Secret header
app.MapAutomationApi();
// Read-only public data (courses, plans, FAQ) for the chat assistant
app.MapPublicApi();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
