using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

/// <summary>
/// Who is staff at which event. "All staff" on rosters, bulk sign-up, and the unassigned list
/// means the active users with a row here for that event.
/// </summary>
public class EventStaffService(IDbContextFactory<AppDbContext> factory)
{
    /// <summary>Active users on the event's staff, sorted by last then first name.</summary>
    public async Task<List<EventStaff>> GetForEventAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var rows = await db.EventStaff.AsNoTracking()
            .Where(s => s.EventId == eventId && s.User.IsActive)
            .Include(s => s.User)
            .Include(s => s.UserRole)
            .Include(s => s.Group)
            .ToListAsync();
        return rows
            .OrderBy(s => s.User.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.User.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Active users not yet on the event's staff, for the "+ Add" picker.</summary>
    public async Task<List<User>> GetAddableUsersAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.Users.AsNoTracking()
            .Include(u => u.UserRole)
            .Where(u => u.IsActive && !db.EventStaff.Any(s => s.EventId == eventId && s.UserId == u.UserId))
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();
    }

    public async Task<List<UserRole>> GetRolesAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.UserRoles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
    }

    public async Task<List<Group>> GetGroupsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.Groups.AsNoTracking().Where(g => g.EventId == eventId).OrderBy(g => g.Name).ToListAsync();
    }

    public async Task<EventStaff?> GetAsync(Guid userId, Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.EventStaff.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.EventId == eventId);
    }

    public async Task<bool> IsStaffAtAsync(Guid userId, Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.EventStaff.AnyAsync(s => s.UserId == userId && s.EventId == eventId);
    }

    /// <summary>Adds the user (role defaults to their global role). A no-op if they're already listed.</summary>
    public async Task AddAsync(Guid eventId, Guid userId, Guid? userRoleId = null)
    {
        using var db = factory.CreateDbContext();
        if (await db.EventStaff.AnyAsync(s => s.EventId == eventId && s.UserId == userId)) return;

        var roleId = userRoleId
            ?? await db.Users.Where(u => u.UserId == userId).Select(u => u.UserRoleId).FirstAsync();
        db.EventStaff.Add(new EventStaff
        {
            EventStaffId = Guid.NewGuid(),
            EventId      = eventId,
            UserId       = userId,
            UserRoleId   = roleId,
            AddedAt      = CampTime.Now
        });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent add of the same (EventId, UserId) tripped the unique index — that's the
            // outcome we wanted. Re-check with a fresh context so a real failure still surfaces.
            using var db2 = factory.CreateDbContext();
            if (!await db2.EventStaff.AnyAsync(s => s.EventId == eventId && s.UserId == userId)) throw;
        }
    }

    /// <summary>Adds every active user not yet on the event's staff. Returns how many were added.</summary>
    public async Task<int> AddAllActiveUsersAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var missing = await db.Users
            .Where(u => u.IsActive && !db.EventStaff.Any(s => s.EventId == eventId && s.UserId == u.UserId))
            .Select(u => new { u.UserId, u.UserRoleId })
            .ToListAsync();
        foreach (var u in missing)
            db.EventStaff.Add(new EventStaff
            {
                EventStaffId = Guid.NewGuid(),
                EventId      = eventId,
                UserId       = u.UserId,
                UserRoleId   = u.UserRoleId,
                AddedAt      = CampTime.Now
            });
        await db.SaveChangesAsync();
        return missing.Count;
    }

    /// <summary>
    /// Adds the active people from another event's team (with the role they had there, no group —
    /// groups belong to one event). People already on this team are left alone. Returns how many were added.
    /// </summary>
    public async Task<int> CopyFromEventAsync(Guid sourceEventId, Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var source = await db.EventStaff
            .Where(s => s.EventId == sourceEventId && s.User.IsActive
                && !db.EventStaff.Any(t => t.EventId == eventId && t.UserId == s.UserId))
            .Select(s => new { s.UserId, s.UserRoleId })
            .ToListAsync();
        foreach (var s in source)
            db.EventStaff.Add(new EventStaff
            {
                EventStaffId = Guid.NewGuid(),
                EventId      = eventId,
                UserId       = s.UserId,
                UserRoleId   = s.UserRoleId,
                AddedAt      = CampTime.Now
            });
        await db.SaveChangesAsync();
        return source.Count;
    }

    /// <summary>Other events with how many active people are on each team, for "Copy team from…".</summary>
    public async Task<List<(Event Event, int TeamSize)>> GetOtherEventsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var rows = await db.Events.AsNoTracking()
            .Where(e => e.EventId != eventId)
            .OrderByDescending(e => e.EffDate)
            .Select(e => new { Event = e, Size = db.EventStaff.Count(s => s.EventId == e.EventId && s.User.IsActive) })
            .ToListAsync();
        return rows.Select(r => (r.Event, r.Size)).ToList();
    }

    /// <summary>Any account (active or not) with this email, so "invite" doesn't create a duplicate.</summary>
    public async Task<User?> FindUserByEmailAsync(string email)
    {
        using var db = factory.CreateDbContext();
        var normalized = email.Trim().ToLowerInvariant();
        return await db.Users.AsNoTracking().Include(u => u.UserRole).FirstOrDefaultAsync(u => u.Email == normalized);
    }

    /// <summary>Team members who have a Hub staff-directory card at this event.</summary>
    public async Task<HashSet<Guid>> GetDirectoryUserIdsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var ids = await db.StaffMembers
            .Where(m => m.CampEventId == eventId && m.LinkedUserId != null)
            .Select(m => m.LinkedUserId!.Value)
            .ToListAsync();
        return ids.ToHashSet();
    }

    /// <summary>Changes the role label and group. The group must belong to the same event.</summary>
    public async Task UpdateAsync(Guid eventStaffId, Guid userRoleId, Guid? groupId)
    {
        using var db = factory.CreateDbContext();
        var row = await db.EventStaff.FindAsync(eventStaffId)
            ?? throw new InvalidOperationException("That staff entry no longer exists.");
        if (groupId is { } gid && !await db.Groups.AnyAsync(g => g.GroupId == gid && g.EventId == row.EventId))
            throw new InvalidOperationException("That group belongs to a different event.");
        row.UserRoleId = userRoleId;
        row.GroupId    = groupId;
        await db.SaveChangesAsync();
    }

    /// <summary>Removes them from the event's staff. Their check-ins and sign-ups are kept.</summary>
    public async Task RemoveAsync(Guid eventStaffId)
    {
        using var db = factory.CreateDbContext();
        var row = await db.EventStaff.FindAsync(eventStaffId);
        if (row is null) return;
        db.EventStaff.Remove(row);
        await db.SaveChangesAsync();
    }
}
