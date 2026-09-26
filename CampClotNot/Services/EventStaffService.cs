using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

/// <summary>A person's details, entered once and reused on every event's team and directory.</summary>
public record PersonDetails(string FirstName, string LastName, string? Email, string? Phone, string AvatarEmoji = "👤");

/// <summary>
/// An event's team (<see cref="EventStaff"/>) and the people on it. "All staff" on rosters, bulk
/// sign-up, and the unassigned list means the active people with a row here for that event. A
/// person is a <see cref="User"/> row; <see cref="User.CanSignIn"/> is false for people who are
/// only listed (a nurse line, a guest speaker). Admins never see the word "person" — the Team page
/// talks about team members and "Can sign in".
/// </summary>
public class EventStaffService(IDbContextFactory<AppDbContext> factory, StaffDirectoryService directory)
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

    /// <summary>Adds the person (role defaults to their account role). A no-op if they're already on the team.</summary>
    public async Task AddAsync(Guid eventId, Guid userId, Guid? userRoleId = null, bool showInDirectory = false, string? title = null)
    {
        using var db = factory.CreateDbContext();
        if (await db.EventStaff.AnyAsync(s => s.EventId == eventId && s.UserId == userId)) return;

        var roleId = userRoleId
            ?? await db.Users.Where(u => u.UserId == userId).Select(u => u.UserRoleId).FirstAsync();
        db.EventStaff.Add(new EventStaff
        {
            EventStaffId    = Guid.NewGuid(),
            EventId         = eventId,
            UserId          = userId,
            UserRoleId      = roleId,
            Title           = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            ShowInDirectory = showInDirectory,
            SortOrder       = await NextSortOrderAsync(db, eventId),
            AddedAt         = CampTime.Now
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
        if (showInDirectory) directory.Invalidate(eventId);
    }

    private static async Task<int> NextSortOrderAsync(AppDbContext db, Guid eventId) =>
        (await db.EventStaff.Where(s => s.EventId == eventId).MaxAsync(s => (int?)s.SortOrder) ?? -1) + 1;

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
    /// Adds the active people from another event's team with the role, title, directory setting,
    /// and card order they had there — but no group, since groups belong to one event. People
    /// already on this team are left alone. Returns how many were added.
    /// </summary>
    public async Task<int> CopyFromEventAsync(Guid sourceEventId, Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var source = await db.EventStaff
            .Where(s => s.EventId == sourceEventId && s.User.IsActive
                && !db.EventStaff.Any(t => t.EventId == eventId && t.UserId == s.UserId))
            .Select(s => new { s.UserId, s.UserRoleId, s.Title, s.ShowInDirectory, s.SortOrder })
            .ToListAsync();
        var offset = await NextSortOrderAsync(db, eventId);
        foreach (var s in source)
            db.EventStaff.Add(new EventStaff
            {
                EventStaffId    = Guid.NewGuid(),
                EventId         = eventId,
                UserId          = s.UserId,
                UserRoleId      = s.UserRoleId,
                Title           = s.Title,
                ShowInDirectory = s.ShowInDirectory,
                SortOrder       = offset + s.SortOrder,
                AddedAt         = CampTime.Now
            });
        await db.SaveChangesAsync();
        directory.Invalidate(eventId);
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

    /// <summary>Anyone (active or not) with this email, so adding someone doesn't create a duplicate.</summary>
    public async Task<User?> FindUserByEmailAsync(string email, Guid? exceptUserId = null)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length == 0) return null;
        using var db = factory.CreateDbContext();
        return await db.Users.AsNoTracking().Include(u => u.UserRole)
            .FirstOrDefaultAsync(u => u.Email == normalized && u.UserId != exceptUserId);
    }

    // ── People (entered once, reused on every event) ─────────────────────────

    /// <summary>
    /// Creates someone who is only on teams/directories and can't sign in. (People who can sign in
    /// are created through AuthService and then invited.)
    /// </summary>
    public async Task<User> CreateListedPersonAsync(PersonDetails d, Guid userRoleId)
    {
        using var db = factory.CreateDbContext();
        var user = new User
        {
            UserId       = Guid.NewGuid(),
            UserRoleId   = userRoleId,
            FirstName    = d.FirstName.Trim(),
            LastName     = d.LastName.Trim(),
            Email        = (d.Email ?? "").Trim().ToLowerInvariant(),
            Phone        = string.IsNullOrWhiteSpace(d.Phone) ? null : d.Phone.Trim(),
            AvatarEmoji  = string.IsNullOrWhiteSpace(d.AvatarEmoji) ? "👤" : d.AvatarEmoji.Trim(),
            PasswordHash = "",
            CanSignIn    = false,
            IsActive     = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Updates name, email, phone, and emoji. Shows everywhere they're listed.</summary>
    public async Task UpdatePersonAsync(Guid userId, PersonDetails d)
    {
        using var db = factory.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.UserId == userId);
        user.FirstName   = d.FirstName.Trim();
        user.LastName    = d.LastName.Trim();
        user.Email       = (d.Email ?? "").Trim().ToLowerInvariant();
        user.Phone       = string.IsNullOrWhiteSpace(d.Phone) ? null : d.Phone.Trim();
        user.AvatarEmoji = string.IsNullOrWhiteSpace(d.AvatarEmoji) ? "👤" : d.AvatarEmoji.Trim();
        await db.SaveChangesAsync();
        await InvalidateDirectoriesAsync(db, userId);
    }

    /// <summary>Sets (or, with null data, removes) the photo, and its crop position.</summary>
    public async Task SetPhotoAsync(Guid userId, byte[]? data, string? contentType, string? objectPosition, bool keepExisting)
    {
        using var db = factory.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.UserId == userId);
        if (!keepExisting)
        {
            user.PhotoData        = data;
            user.PhotoContentType = data is null ? null : contentType;
        }
        user.PhotoObjectPosition = user.PhotoData is null ? null : objectPosition;
        await db.SaveChangesAsync();
        await InvalidateDirectoriesAsync(db, userId);
    }

    /// <summary>
    /// Turns sign-in on or off. Turning it off keeps their password but login, forgot-password,
    /// and reset links refuse them. Turning it on for someone who never had a password needs an
    /// invite link (the caller sends it).
    /// </summary>
    public async Task SetCanSignInAsync(Guid userId, bool canSignIn)
    {
        using var db = factory.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.UserId == userId);
        user.CanSignIn = canSignIn;
        await db.SaveChangesAsync();
    }

    // Name/phone/photo changes show on every event's directory the person is in.
    private async Task InvalidateDirectoriesAsync(AppDbContext db, Guid userId)
    {
        foreach (var eventId in await db.EventStaff.Where(s => s.UserId == userId).Select(s => s.EventId).ToListAsync())
            directory.Invalidate(eventId);
    }

    /// <summary>The title on their directory card at this event, and whether the card is shown.</summary>
    public async Task UpdateDirectoryAsync(Guid eventStaffId, string? title, bool showInDirectory)
    {
        using var db = factory.CreateDbContext();
        var row = await db.EventStaff.FindAsync(eventStaffId)
            ?? throw new InvalidOperationException("That team member no longer exists.");
        if (showInDirectory && !row.ShowInDirectory)
            row.SortOrder = await NextSortOrderAsync(db, row.EventId);   // newly shown cards go last
        row.Title           = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        row.ShowInDirectory = showInDirectory;
        await db.SaveChangesAsync();
        directory.Invalidate(row.EventId);
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
        directory.Invalidate(row.EventId);
    }
}
