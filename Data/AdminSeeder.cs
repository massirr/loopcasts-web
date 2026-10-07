using Microsoft.AspNetCore.Identity;

namespace LoopCastsWeb.Data;

/// <summary>
/// Creates the "Admin" role and one personal admin account per person listed under "SeedAdmins"
/// in configuration. The passwords come from user-secrets (dev) or environment variables (server),
/// NEVER from code or appsettings.json — the lesson from login.php's hardcoded password.
/// </summary>
public static class AdminSeeder
{
    public const string AdminRole = "Admin";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var users = services.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roles.RoleExistsAsync(AdminRole))
            await roles.CreateAsync(new IdentityRole(AdminRole));

        var people = config.GetSection("SeedAdmins").GetChildren().ToList();
        if (people.Count == 0)
        {
            logger.LogWarning("No 'SeedAdmins' found in configuration (user-secrets). No admin accounts created. " +
                              "Run 'dotnet user-secrets list' inside the project folder to check.");
            return;
        }
        logger.LogInformation("Found {Count} SeedAdmins entries in configuration", people.Count);

        foreach (var person in people)
        {
            var email = person["Email"];
            var password = person["Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("SeedAdmins:{Key} is missing Email or Password, skipped", person.Key);
                continue;
            }

            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                var result = await users.CreateAsync(user, password);   // stored as a salted hash, not MD5
                if (!result.Succeeded)
                {
                    logger.LogWarning("Could not create admin {Email}: {Errors}", email,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                    continue;
                }
                logger.LogInformation("Created admin account {Email}", email);
            }
            else
            {
                logger.LogInformation("Admin account {Email} already exists", email);
            }

            if (!await users.IsInRoleAsync(user, AdminRole))
                await users.AddToRoleAsync(user, AdminRole);
        }
    }
}
