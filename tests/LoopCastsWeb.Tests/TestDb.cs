using LoopCastsWeb.Data;
using LoopCastsWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoopCastsWeb.Tests;

/// A real SQLite database that lives in memory: same SQL and UNIQUE indexes as loopcasts.db, gone after the test.
public static class TestDb
{
    public static AppDbContext Create() => Services().GetRequiredService<AppDbContext>();

    /// The app's services (Identity, PaymentService, ...) on an in-memory database, like Program.cs registers them.
    public static ServiceProvider Services()
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

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return provider;
    }
}
