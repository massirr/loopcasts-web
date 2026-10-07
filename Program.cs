using LoopCastsWeb.Data;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
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
builder.Services.AddScoped<PaymentService>();   // one instance per request, like the DbContext
builder.Services.AddHostedService<ExpireAccountsService>();   // nightly: switch off expired accounts

var app = builder.Build();

// On startup: apply pending migrations, then import the old course catalogue (only if the table is empty).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
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
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
