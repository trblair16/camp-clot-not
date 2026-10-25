using CampClotNot.Data;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public enum GuestIdentityMatch
{
    NoMatch,
    SignInRequired,
    LinkedTeamMember
}

public record GuestIdentityMatchResult(GuestIdentityMatch Kind, Guid? TeamEventStaffId = null)
{
    public static readonly GuestIdentityMatchResult None = new(GuestIdentityMatch.NoMatch);
    public static readonly GuestIdentityMatchResult SignIn = new(GuestIdentityMatch.SignInRequired);
    public static GuestIdentityMatchResult Team(Guid eventStaffId) =>
        new(GuestIdentityMatch.LinkedTeamMember, eventStaffId);
}

/// <summary>
/// Matches a typed guest name against this event's team roster first, then — only if nothing there
/// matches — against every sign-in-capable account site-wide. See
/// docs/superpowers/specs/2026-10-24-guest-identity-matching-design.md for the full rule.
/// </summary>
public class GuestIdentityMatchService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<GuestIdentityMatchResult> MatchAsync(string firstName, string lastName, Guid eventId)
    {
        var normFirst = GuestAccessService.Normalize(firstName);
        var normLast = GuestAccessService.Normalize(lastName);

        using var db = factory.CreateDbContext();

        var team = await db.EventStaff
            .Where(s => s.EventId == eventId)
            .Include(s => s.User)
            .ToListAsync();
        var teamMatches = team
            .Where(s => GuestAccessService.Normalize(s.User.FirstName) == normFirst
                     && GuestAccessService.Normalize(s.User.LastName) == normLast
                     && s.User.IsActive)
            .ToList();

        if (teamMatches.Count > 0)
        {
            if (teamMatches.Any(s => s.User.CanSignIn)) return GuestIdentityMatchResult.SignIn;
            // All matches are CanSignIn=false. Exactly one -> link it. More than one -> ambiguous,
            // don't guess which one they mean.
            return teamMatches.Count == 1
                ? GuestIdentityMatchResult.Team(teamMatches[0].EventStaffId)
                : GuestIdentityMatchResult.None;
        }

        var accounts = await db.Users
            .Where(u => u.CanSignIn && u.IsActive)
            .Select(u => new { u.FirstName, u.LastName })
            .ToListAsync();
        var hasAccount = accounts.Any(u =>
            GuestAccessService.Normalize(u.FirstName) == normFirst &&
            GuestAccessService.Normalize(u.LastName) == normLast);

        return hasAccount ? GuestIdentityMatchResult.SignIn : GuestIdentityMatchResult.None;
    }
}
