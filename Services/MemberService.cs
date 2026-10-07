using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LoopCastsWeb.Services;

/// <summary>
/// Customer logins ("Members"). Replaces "insert a row in phpMyAdmin + MD5 password" (handbook ch. 3):
/// the login is created without a password and the customer chooses one via a password-setup link.
/// </summary>
public class MemberService
{
    public const string MemberRole = "Member";

    private readonly UserManager<IdentityUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public MemberService(UserManager<IdentityUser> users, RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    /// Finds or creates the login for this e-mail. Safe to call many times (returning customer, webhook retry).
    public async Task<IdentityUser> EnsureMemberAsync(string email) => (await EnsureMemberCreatedAsync(email)).User;

    /// Same, and tells whether the login was created just now (only then may someone else see a setup link).
    public async Task<(IdentityUser User, bool Created)> EnsureMemberCreatedAsync(string email)
    {
        if (!await _roles.RoleExistsAsync(MemberRole))
            await _roles.CreateAsync(new IdentityRole(MemberRole));

        var user = await _users.FindByEmailAsync(email);
        var created = user is null;
        if (user is null)
        {
            // EmailConfirmed: they paid with this address; the setup link proves it again once it is e-mailed (#5)
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await _users.CreateAsync(user);   // no password on purpose
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not create member: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await _users.IsInRoleAsync(user, MemberRole))
            await _users.AddToRoleAsync(user, MemberRole);
        return (user, created);
    }

    /// Setup code only for a login without password (a customer who already chose one gets no reset link).
    public async Task<string?> SetupCodeIfNoPasswordAsync(IdentityUser user) =>
        await _users.HasPasswordAsync(user) ? null : await PasswordSetupCodeAsync(user);

    /// The "code" for /Identity/Account/ResetPassword (that page expects the token base64url-encoded).
    public async Task<string> PasswordSetupCodeAsync(IdentityUser user)
    {
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }
}
