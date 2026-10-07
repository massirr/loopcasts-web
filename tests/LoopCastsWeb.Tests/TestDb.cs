using LoopCastsWeb.Data;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoopCastsWeb.Tests;

/// A real SQLite database that lives in memory: same SQL and UNIQUE indexes as loopcasts.db, gone after the test.
public static class TestDb
{
    public static AppDbContext Create() => Services().GetRequiredService<AppDbContext>();

    /// The app's services (Identity, PaymentService, ...) on an in-memory database, like Program.cs registers them.
    /// mollie: fake Mollie API (null = no API key configured, like a dev machine without user-secrets).
    public static ServiceProvider Services(HttpMessageHandler? mollie = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();   // the in-memory db exists as long as this connection is open

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();   // needed for password-reset tokens
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<MemberService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<TeamSeats>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var client = services.AddHttpClient<MollieService>(c =>
        {
            c.BaseAddress = new Uri("https://api.mollie.com/v2/");
            if (mollie is not null) c.DefaultRequestHeaders.Authorization = new("Bearer", "test_fake");
        });
        if (mollie is not null) client.ConfigurePrimaryHttpMessageHandler(() => mollie);

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return provider;
    }
}
