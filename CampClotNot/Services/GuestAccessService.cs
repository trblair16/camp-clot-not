using System.Security.Claims;
using System.Text.RegularExpressions;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace CampClotNot.Services;

public static class GuestClaimTypes
{
    public const string EventId = "ccn:guestEventId";
    public const string GuestAttendeeId = "ccn:guestAttendeeId";
}

public class GuestAccessService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<Event?> ValidateCodeAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var normalized = code.Trim().ToUpperInvariant();

        using var db = factory.CreateDbContext();
        var ev = await db.Events.FirstOrDefaultAsync(e => e.GuestCode == normalized);
        if (ev is null) return null;

        var accessExpires = ev.ExpDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        if (DateTime.UtcNow > accessExpires) return null;

        return ev;
    }

    public async Task SignInGuestAsync(HttpContext httpContext, Event ev, GuestAttendee guest)
    {
        var claims = new List<Claim>
        {
            new(GuestClaimTypes.EventId, ev.EventId.ToString()),
            new(GuestClaimTypes.GuestAttendeeId, guest.GuestAttendeeId.ToString()),
            new(ClaimTypes.Name, guest.FirstName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var expiresUtc = new DateTimeOffset(ev.ExpDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = expiresUtc });
    }

    public Guid? GetGuestEventId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(GuestClaimTypes.EventId)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public Guid? GetGuestAttendeeId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(GuestClaimTypes.GuestAttendeeId)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static string Normalize(string s) =>
        Regex.Replace(s.Trim(), @"\s+", " ").ToLowerInvariant();

    public async Task<GuestAttendee> GetOrCreateGuestAsync(string firstName, string lastName)
    {
        var normFirst = Normalize(firstName);
        var normLast = Normalize(lastName);
        if (string.IsNullOrEmpty(normFirst) || string.IsNullOrEmpty(normLast))
            throw new ArgumentException("First and last name cannot be empty.");

        using var db = factory.CreateDbContext();
        var existing = await db.GuestAttendees.FirstOrDefaultAsync(g =>
            g.NormalizedFirstName == normFirst && g.NormalizedLastName == normLast);
        if (existing is not null) return existing;

        var guest = new GuestAttendee
        {
            GuestAttendeeId = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            NormalizedFirstName = normFirst,
            NormalizedLastName = normLast,
            CreatedAt = DateTime.UtcNow
        };
        db.GuestAttendees.Add(guest);
        try
        {
            await db.SaveChangesAsync();
            return guest;
        }
        catch (DbUpdateException)
        {
            // A concurrent request won the race to insert the same normalized name,
            // tripping the DB-level unique index on (NormalizedFirstName, NormalizedLastName).
            // The original context may be in a bad state after the failed save, so
            // re-query with a fresh context and return the winner's row instead of throwing.
            using var db2 = factory.CreateDbContext();
            var winner = await db2.GuestAttendees.FirstOrDefaultAsync(g =>
                g.NormalizedFirstName == normFirst && g.NormalizedLastName == normLast);
            if (winner is not null) return winner;

            // Should not happen given the unique index, but don't return null
            // from a non-nullable method if it somehow does.
            throw;
        }
    }

    public async Task RecordVisitAsync(Guid guestAttendeeId, Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var now = DateTime.UtcNow;
        var visit = await db.GuestEventVisits.FirstOrDefaultAsync(v =>
            v.GuestAttendeeId == guestAttendeeId && v.EventId == eventId);

        if (visit is null)
        {
            db.GuestEventVisits.Add(new GuestEventVisit
            {
                GuestEventVisitId = Guid.NewGuid(),
                GuestAttendeeId = guestAttendeeId,
                EventId = eventId,
                FirstJoinedAt = now,
                LastSeenAt = now
            });

            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException)
            {
                // A concurrent request won the race to insert the same (GuestAttendeeId, EventId)
                // pair, tripping the DB-level unique index. The original context may be in a bad
                // state after the failed save, so re-query with a fresh context and update the
                // winner's row instead of throwing.
                using var db2 = factory.CreateDbContext();
                var winner = await db2.GuestEventVisits.FirstOrDefaultAsync(v =>
                    v.GuestAttendeeId == guestAttendeeId && v.EventId == eventId);
                if (winner is not null)
                {
                    winner.LastSeenAt = now;
                    await db2.SaveChangesAsync();
                    return;
                }

                // Should not happen given the unique index, but don't silently swallow it if it does.
                throw;
            }
        }

        visit.LastSeenAt = now;
        await db.SaveChangesAsync();
    }

    public async Task<List<GuestAttendee>> GetAllGuestsWithVisitsAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.GuestAttendees
            .Include(g => g.Visits)
            .ThenInclude(v => v.Event)
            .OrderBy(g => g.LastName)
            .ThenBy(g => g.FirstName)
            .ToListAsync();
    }

    public async Task<bool> UpdateGuestNameAsync(Guid guestAttendeeId, string firstName, string lastName)
    {
        var normFirst = Normalize(firstName);
        var normLast = Normalize(lastName);
        if (string.IsNullOrEmpty(normFirst) || string.IsNullOrEmpty(normLast))
            throw new ArgumentException("First and last name cannot be empty.");

        using var db = factory.CreateDbContext();
        var guest = await db.GuestAttendees.FindAsync(guestAttendeeId);
        if (guest is null) return true;

        var collision = await db.GuestAttendees.AnyAsync(g =>
            g.GuestAttendeeId != guestAttendeeId &&
            g.NormalizedFirstName == normFirst &&
            g.NormalizedLastName == normLast);
        if (collision) return false;

        guest.FirstName = firstName.Trim();
        guest.LastName = lastName.Trim();
        guest.NormalizedFirstName = normFirst;
        guest.NormalizedLastName = normLast;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task DeleteGuestAsync(Guid guestAttendeeId)
    {
        using var db = factory.CreateDbContext();
        var guest = await db.GuestAttendees.FindAsync(guestAttendeeId);
        if (guest is null) return;
        db.GuestAttendees.Remove(guest);
        await db.SaveChangesAsync();
    }

    public byte[] GenerateJoinQrPng(string joinUrl)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(20);
    }
}
