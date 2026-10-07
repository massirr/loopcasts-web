using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Services;

/// <summary>
/// Team licence: the buyer adds and removes colleagues (Plan.Seats in total, the buyer's own seat included).
/// Replaces "e-mail LoopCasts for every new team member". A seat = an Account on the team order, with the same
/// end date as the licence, so AccountExpiry switches the whole team off together.
/// Every method checks that the caller (buyerEmail, from the login) bought this order.
/// </summary>
public class TeamSeats
{
    public record Licence(Order Order, int Seats, List<Account> Accounts)
    {
        public int Used => Accounts.Count(a => a.IsActive);
    }

    /// NewLogin: set only when the colleague's login was created just now (then the buyer may pass on the setup link).
    public record AddResult(string? Error, IdentityUser? NewLogin = null);

    private readonly AppDbContext _db;
    private readonly MemberService _members;

    public TeamSeats(AppDbContext db, MemberService members)
    {
        _db = db;
        _members = members;
    }

    /// Paid multi-seat orders bought by this e-mail, with their seats.
    public async Task<List<Licence>> LicencesAsync(string buyerEmail)
    {
        var e = buyerEmail.ToLower();
        var multiSeat = Plans.All.Where(p => p.Seats > 1).Select(p => p.Code).ToList();
        var orders = await _db.Orders
            .Where(o => o.IsPaid && o.Email.ToLower() == e && multiSeat.Contains(o.PlanCode))
            .ToListAsync();

        var result = new List<Licence>();
        foreach (var o in orders)
        {
            var accounts = await _db.Accounts.Where(a => a.OrderId == o.Id).OrderBy(a => a.Id).ToListAsync();
            result.Add(new Licence(o, Plans.Find(o.PlanCode)!.Seats, accounts));
        }
        return result;
    }

    public async Task<AddResult> AddAsync(int orderId, string buyerEmail, string colleagueEmail)
    {
        var licence = (await LicencesAsync(buyerEmail)).FirstOrDefault(l => l.Order.Id == orderId);
        if (licence is null) return new("Licence not found.");

        colleagueEmail = colleagueEmail.Trim();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var buyerSeat = licence.Accounts.First();                       // created by PaymentService when paid
        if (buyerSeat.EndsOn < today) return new("This licence has ended.");

        var existing = licence.Accounts.FirstOrDefault(a => string.Equals(a.Email, colleagueEmail, StringComparison.OrdinalIgnoreCase));
        if (existing is { IsActive: true }) return new($"{colleagueEmail} already has a seat.");
        if (licence.Used >= licence.Seats) return new($"All {licence.Seats} seats are in use. Remove someone first.");

        if (existing is not null)
            existing.IsActive = true;                                    // removed earlier, added again
        else
            _db.Accounts.Add(new Account
            {
                OrderId = orderId,
                Email = colleagueEmail,
                StartsOn = today,
                EndsOn = buyerSeat.EndsOn,
                IsActive = true,
            });
        await _db.SaveChangesAsync();

        var (user, created) = await _members.EnsureMemberCreatedAsync(colleagueEmail);
        return new(null, created ? user : null);
    }

    public async Task<string?> RemoveAsync(int orderId, string buyerEmail, int accountId)
    {
        var licence = (await LicencesAsync(buyerEmail)).FirstOrDefault(l => l.Order.Id == orderId);
        var seat = licence?.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (licence is null || seat is null) return "Seat not found.";
        if (string.Equals(seat.Email, licence.Order.Email, StringComparison.OrdinalIgnoreCase))
            return "You can't remove your own seat.";

        seat.IsActive = false;
        await _db.SaveChangesAsync();
        return null;
    }
}
