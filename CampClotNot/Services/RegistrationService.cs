using System.Security.Claims;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public enum SignUpResult { SignedUp, AlreadyInSlot, Full, SelfSignupClosed, NotAllowed, NotAnOption }

/// <summary>Exactly one of GuestAttendeeId / UserId is set.</summary>
public record AttendeeRef(Guid? GuestAttendeeId, Guid? UserId);

/// <summary>Someone who could be signed up: a guest who joined the event, or a user on its staff.</summary>
public record PersonEntry(
    string FirstName, string LastName, bool IsGuest, string? RoleName,
    Guid? RoleId, Guid? GroupId, Guid? GuestAttendeeId, Guid? UserId)
{
    public string FullName => $"{FirstName} {LastName}";
    public string TypeLabel => IsGuest ? "Guest" : RoleName ?? "Staff";
    public AttendeeRef Ref => new(GuestAttendeeId, UserId);
}

public record RegistrationEntry(
    Guid RegistrationId, PersonEntry Person, DateTime RegisteredAt, AttendanceMethod Method,
    string? RegisteredByName, bool OverCapacity, bool CheckedIn);

public record OptionSummary(ScheduleItem Option, int Registered)
{
    public bool IsFull => Option.MaxCapacity is { } cap && Registered >= cap;
    public bool IsOver => Option.MaxCapacity is { } cap && Registered > cap;
}

public record SlotSummary(ScheduleItem Slot, List<OptionSummary> Options, int Unassigned);

public record BulkResult(int Added, int Skipped);

/// <summary>Filters for bulk sign-up. Guests and staff are OR'd; roles and groups narrow the staff side.</summary>
public record BulkFilter(bool Guests, bool Staff, IReadOnlySet<Guid>? RoleIds = null, IReadOnlySet<Guid>? GroupIds = null);

/// <summary>
/// Sign-ups for breakout options (issue #311). A slot is a ScheduleItem with IsBreakoutSlot; its
/// options point at it via ParentScheduleItemId. One option per person per slot is enforced by
/// unique indexes on (SlotScheduleItemId, GuestAttendeeId) and (SlotScheduleItemId, UserId).
/// </summary>
public class RegistrationService(
    IDbContextFactory<AppDbContext> factory,
    GuestAccessService guestSvc,
    ScheduleService scheduleSvc)
{
    private static IQueryable<ScheduleItemRegistration> ForAttendee(
        IQueryable<ScheduleItemRegistration> q, Guid? guestId, Guid? userId) =>
        guestId.HasValue
            ? q.Where(r => r.GuestAttendeeId == guestId)
            : q.Where(r => r.UserId == userId);

    // ── Hub ──────────────────────────────────────────────────────────────────

    /// <summary>The current person's picks for the event: slot id → option id.</summary>
    public async Task<Dictionary<Guid, Guid>> GetMyRegistrationsAsync(ClaimsPrincipal user, Guid eventId)
    {
        var (guestId, userId) = guestSvc.ResolveAttendee(user);
        if (guestId is null && userId is null) return [];

        using var db = factory.CreateDbContext();
        return await ForAttendee(db.ScheduleItemRegistrations, guestId, userId)
            .Where(r => r.SlotScheduleItem.CampEventId == eventId)
            .ToDictionaryAsync(r => r.SlotScheduleItemId, r => r.ScheduleItemId);
    }

    /// <summary>Option id → number signed up, for every option in the event.</summary>
    public async Task<Dictionary<Guid, int>> GetOptionCountsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleItemRegistrations
            .Where(r => r.SlotScheduleItem.CampEventId == eventId)
            .GroupBy(r => r.ScheduleItemId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    public async Task<bool> IsRegisteredAsync(Guid optionId, Guid? guestId, Guid? userId)
    {
        using var db = factory.CreateDbContext();
        return await ForAttendee(db.ScheduleItemRegistrations, guestId, userId)
            .AnyAsync(r => r.ScheduleItemId == optionId);
    }

    /// <summary>
    /// A guest or staff user picks an option. Picks are final — only an Admin can move or remove
    /// them. Capacity is checked under a row lock on the option so two people can't both take
    /// the last spot.
    /// </summary>
    public async Task<SignUpResult> SelfSignUpAsync(ClaimsPrincipal user, Guid optionId)
    {
        var (guestId, userId) = guestSvc.ResolveAttendee(user);
        if (guestId is null && userId is null) return SignUpResult.NotAllowed;

        using var db = factory.CreateDbContext();
        var option = await db.ScheduleItems.AsNoTracking()
            .Include(o => o.ParentScheduleItem)
            .FirstOrDefaultAsync(o => o.ScheduleItemId == optionId);
        if (option?.ParentScheduleItem is not { } slot) return SignUpResult.NotAnOption;
        if (!slot.AllowSelfSignup) return SignUpResult.SelfSignupClosed;

        if (guestId.HasValue)
        {
            if (guestSvc.GetGuestEventId(user) != option.CampEventId) return SignUpResult.NotAllowed;
        }
        else if (!await db.EventStaff.AnyAsync(s => s.UserId == userId && s.EventId == option.CampEventId))
        {
            return SignUpResult.NotAllowed;
        }

        if (await ForAttendee(db.ScheduleItemRegistrations, guestId, userId)
                .AnyAsync(r => r.SlotScheduleItemId == slot.ScheduleItemId))
            return SignUpResult.AlreadyInSlot;

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Lock the option row so concurrent sign-ups for it queue up behind this count.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"ScheduleItems\" WHERE \"ScheduleItemId\" = {optionId} FOR UPDATE");
            if (option.MaxCapacity is { } cap
                && await db.ScheduleItemRegistrations.CountAsync(r => r.ScheduleItemId == optionId) >= cap)
            {
                await tx.RollbackAsync();
                return SignUpResult.Full;
            }

            db.ScheduleItemRegistrations.Add(NewRegistration(option, slot.ScheduleItemId, guestId, userId, AttendanceMethod.Self, null));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return SignUpResult.SignedUp;
        }
        catch (DbUpdateException)
        {
            // A concurrent request (double tap, two tabs) won the race and tripped the one-per-slot
            // unique index. Re-check with a fresh context; anything else is a real failure.
            await tx.RollbackAsync();
            using var db2 = factory.CreateDbContext();
            if (await ForAttendee(db2.ScheduleItemRegistrations, guestId, userId)
                    .AnyAsync(r => r.SlotScheduleItemId == slot.ScheduleItemId))
                return SignUpResult.AlreadyInSlot;
            throw;
        }
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    /// <summary>Every slot in the event, with its options, their counts, and how many expected people are unassigned.</summary>
    public async Task<List<SlotSummary>> GetSlotsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var slots = await db.ScheduleItems.AsNoTracking()
            .Where(s => s.CampEventId == eventId && s.IsBreakoutSlot)
            .Include(s => s.Options).ThenInclude(o => o.Location)
            .OrderBy(s => s.CampDay).ThenBy(s => s.StartTime)
            .ToListAsync();
        var counts = await GetOptionCountsAsync(eventId);
        var perSlot = await db.ScheduleItemRegistrations
            .Where(r => r.SlotScheduleItem.CampEventId == eventId)
            .GroupBy(r => r.SlotScheduleItemId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var expected = (await GetExpectedPeopleAsync(eventId)).Count;

        return slots.Select(s => new SlotSummary(
            s,
            s.Options
                .OrderBy(o => o.StartTime).ThenBy(o => o.Title)
                .Select(o => new OptionSummary(o, counts.GetValueOrDefault(o.ScheduleItemId)))
                .ToList(),
            // Registrations can include people no longer expected (e.g. removed staff), so floor at 0.
            Math.Max(0, expected - perSlot.GetValueOrDefault(s.ScheduleItemId))))
            .ToList();
    }

    /// <summary>Guests who joined the event plus active users on its staff, sorted by name.</summary>
    public async Task<List<PersonEntry>> GetExpectedPeopleAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var guests = await db.GuestEventVisits.AsNoTracking()
            .Where(v => v.EventId == eventId)
            .Select(v => v.GuestAttendee)
            .ToListAsync();
        var staff = await db.EventStaff.AsNoTracking()
            .Where(s => s.EventId == eventId && s.User.IsActive)
            .Include(s => s.User)
            .Include(s => s.UserRole)
            .ToListAsync();

        return guests
            .Select(g => new PersonEntry(g.FirstName, g.LastName, true, null, null, null, g.GuestAttendeeId, null))
            .Concat(staff.Select(s => new PersonEntry(s.User.FirstName, s.User.LastName, false, s.UserRole.Name,
                s.UserRoleId, s.GroupId, null, s.UserId)))
            .OrderBy(p => p.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IEnumerable<PersonEntry> ApplyFilter(IEnumerable<PersonEntry> people, BulkFilter f) =>
        people.Where(p => p.IsGuest
            ? f.Guests
            : (f.Staff
               || (f.RoleIds is { Count: > 0 } && p.RoleId is { } r && f.RoleIds.Contains(r))
               || (f.GroupIds is { Count: > 0 } && p.GroupId is { } g && f.GroupIds.Contains(g))));

    /// <summary>Expected people who aren't in any option of the slot.</summary>
    public async Task<List<PersonEntry>> GetUnassignedAsync(Guid slotId)
    {
        using var db = factory.CreateDbContext();
        var slot = await db.ScheduleItems.AsNoTracking().FirstOrDefaultAsync(s => s.ScheduleItemId == slotId);
        if (slot is null) return [];

        var taken = await db.ScheduleItemRegistrations.AsNoTracking()
            .Where(r => r.SlotScheduleItemId == slotId)
            .Select(r => new { r.GuestAttendeeId, r.UserId })
            .ToListAsync();
        var takenGuests = taken.Where(t => t.GuestAttendeeId.HasValue).Select(t => t.GuestAttendeeId!.Value).ToHashSet();
        var takenUsers  = taken.Where(t => t.UserId.HasValue).Select(t => t.UserId!.Value).ToHashSet();

        return (await GetExpectedPeopleAsync(slot.CampEventId))
            .Where(p => p.IsGuest ? !takenGuests.Contains(p.GuestAttendeeId!.Value) : !takenUsers.Contains(p.UserId!.Value))
            .ToList();
    }

    /// <summary>
    /// Everyone signed up for the option. Over-capacity is flagged by sign-up order: the first
    /// MaxCapacity people are within capacity and anyone after them is over.
    /// </summary>
    public async Task<List<RegistrationEntry>> GetOptionRegistrationsAsync(Guid optionId)
    {
        using var db = factory.CreateDbContext();
        var option = await db.ScheduleItems.AsNoTracking().FirstOrDefaultAsync(o => o.ScheduleItemId == optionId);
        if (option is null) return [];

        var regs = await db.ScheduleItemRegistrations.AsNoTracking()
            .Where(r => r.ScheduleItemId == optionId)
            .Include(r => r.GuestAttendee)
            .Include(r => r.User).ThenInclude(u => u!.UserRole)
            .Include(r => r.RegisteredByUser)
            .OrderBy(r => r.RegisteredAt)
            .ToListAsync();
        var eventRoles = await db.EventStaff.AsNoTracking()
            .Where(s => s.EventId == option.CampEventId)
            .Select(s => new { s.UserId, s.UserRoleId, s.UserRole.Name, s.GroupId })
            .ToDictionaryAsync(s => s.UserId);
        var checkedIn = await db.ScheduleItemAttendances.AsNoTracking()
            .Where(a => a.ScheduleItemId == optionId)
            .Select(a => new { a.GuestAttendeeId, a.UserId })
            .ToListAsync();

        return regs.Select((r, i) =>
        {
            PersonEntry person;
            if (r.GuestAttendee is { } g)
                person = new PersonEntry(g.FirstName, g.LastName, true, null, null, null, g.GuestAttendeeId, null);
            else
            {
                var es = eventRoles.GetValueOrDefault(r.UserId!.Value);
                person = new PersonEntry(r.User!.FirstName, r.User.LastName, false, es?.Name ?? r.User.UserRole?.Name,
                    es?.UserRoleId, es?.GroupId, null, r.UserId);
            }
            var isIn = checkedIn.Any(a => r.GuestAttendeeId.HasValue ? a.GuestAttendeeId == r.GuestAttendeeId : a.UserId == r.UserId);
            return new RegistrationEntry(r.ScheduleItemRegistrationId, person, r.RegisteredAt, r.Method,
                r.RegisteredByUser?.FirstName, option.MaxCapacity is { } cap && i >= cap, isIn);
        }).ToList();
    }

    /// <summary>
    /// Admin puts people in an option, ignoring capacity and self sign-up. Anyone already in an
    /// option of the same slot is skipped (use Move for them).
    /// </summary>
    public async Task<BulkResult> AdminAssignAsync(Guid optionId, IEnumerable<AttendeeRef> people, Guid adminUserId)
    {
        using var db = factory.CreateDbContext();
        var option = await db.ScheduleItems.AsNoTracking().FirstOrDefaultAsync(o => o.ScheduleItemId == optionId);
        if (option?.ParentScheduleItemId is not { } slotId) return new BulkResult(0, 0);

        var taken = await db.ScheduleItemRegistrations.AsNoTracking()
            .Where(r => r.SlotScheduleItemId == slotId)
            .Select(r => new { r.GuestAttendeeId, r.UserId })
            .ToListAsync();
        var takenGuests = taken.Where(t => t.GuestAttendeeId.HasValue).Select(t => t.GuestAttendeeId!.Value).ToHashSet();
        var takenUsers  = taken.Where(t => t.UserId.HasValue).Select(t => t.UserId!.Value).ToHashSet();

        int added = 0, skipped = 0;
        foreach (var p in people.Distinct())
        {
            if (p.GuestAttendeeId.HasValue == p.UserId.HasValue) continue;
            var already = p.GuestAttendeeId is { } gid ? !takenGuests.Add(gid) : !takenUsers.Add(p.UserId!.Value);
            if (already) { skipped++; continue; }
            db.ScheduleItemRegistrations.Add(NewRegistration(option, slotId, p.GuestAttendeeId, p.UserId, AttendanceMethod.Admin, adminUserId));
            added++;
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Someone self-signed-up between our read and write. Fall back to one at a time so
            // everyone else still gets added.
            return await AssignOneByOneAsync(option, slotId, people, adminUserId);
        }
        return new BulkResult(added, skipped);
    }

    private async Task<BulkResult> AssignOneByOneAsync(ScheduleItem option, Guid slotId, IEnumerable<AttendeeRef> people, Guid adminUserId)
    {
        int added = 0, skipped = 0;
        foreach (var p in people.Distinct())
        {
            if (p.GuestAttendeeId.HasValue == p.UserId.HasValue) continue;
            using var db = factory.CreateDbContext();
            if (await ForAttendee(db.ScheduleItemRegistrations, p.GuestAttendeeId, p.UserId).AnyAsync(r => r.SlotScheduleItemId == slotId))
            {
                skipped++;
                continue;
            }
            db.ScheduleItemRegistrations.Add(NewRegistration(option, slotId, p.GuestAttendeeId, p.UserId, AttendanceMethod.Admin, adminUserId));
            try { await db.SaveChangesAsync(); added++; }
            catch (DbUpdateException) { skipped++; }
        }
        return new BulkResult(added, skipped);
    }

    /// <summary>
    /// Adds a walk-in guest by name (same matching as /join), records their visit to the event,
    /// and signs them up for the option. Returns the guest.
    /// </summary>
    public async Task<GuestAttendee?> AddWalkInAsync(Guid optionId, string firstName, string lastName, Guid adminUserId)
    {
        using var db = factory.CreateDbContext();
        var option = await db.ScheduleItems.AsNoTracking().FirstOrDefaultAsync(o => o.ScheduleItemId == optionId);
        if (option?.ParentScheduleItemId is null) return null;
        var guest = await guestSvc.GetOrCreateGuestAsync(firstName, lastName);
        await guestSvc.RecordVisitAsync(guest.GuestAttendeeId, option.CampEventId);
        await AdminAssignAsync(optionId, [new AttendeeRef(guest.GuestAttendeeId, null)], adminUserId);
        return guest;
    }

    /// <summary>Moves a sign-up to another option of the same slot. A check-in on the old option is removed.</summary>
    public async Task MoveAsync(Guid registrationId, Guid toOptionId, Guid adminUserId)
    {
        using var db = factory.CreateDbContext();
        var reg = await db.ScheduleItemRegistrations.FindAsync(registrationId)
            ?? throw new InvalidOperationException("That sign-up no longer exists.");
        var to = await db.ScheduleItems.AsNoTracking().FirstOrDefaultAsync(o => o.ScheduleItemId == toOptionId);
        if (to?.ParentScheduleItemId != reg.SlotScheduleItemId)
            throw new InvalidOperationException("People can only be moved between options of the same slot.");
        if (reg.ScheduleItemId == toOptionId) return;

        await RemoveCheckInAsync(db, reg);
        reg.ScheduleItemId     = toOptionId;
        reg.Method             = AttendanceMethod.Admin;
        reg.RegisteredByUserId = adminUserId;
        reg.RegisteredAt       = CampTime.Now;
        await db.SaveChangesAsync();
    }

    /// <summary>Removes a sign-up (and any check-in on that option).</summary>
    public async Task RemoveAsync(Guid registrationId)
    {
        using var db = factory.CreateDbContext();
        var reg = await db.ScheduleItemRegistrations.FindAsync(registrationId);
        if (reg is null) return;
        await RemoveCheckInAsync(db, reg);
        db.ScheduleItemRegistrations.Remove(reg);
        await db.SaveChangesAsync();
    }

    public async Task SetSelfSignupAsync(Guid slotId, bool allow)
    {
        using var db = factory.CreateDbContext();
        var slot = await db.ScheduleItems.FirstOrDefaultAsync(s => s.ScheduleItemId == slotId && s.IsBreakoutSlot);
        if (slot is null) return;
        slot.AllowSelfSignup = allow;
        await db.SaveChangesAsync();
        scheduleSvc.Invalidate(slot.CampEventId, slot.CampDay);
    }

    // A check-in without a matching sign-up would be orphaned, so moving or removing someone
    // also clears their check-in on the option they're leaving.
    private static async Task RemoveCheckInAsync(AppDbContext db, ScheduleItemRegistration reg)
    {
        var q = db.ScheduleItemAttendances.Where(a => a.ScheduleItemId == reg.ScheduleItemId);
        q = reg.GuestAttendeeId.HasValue
            ? q.Where(a => a.GuestAttendeeId == reg.GuestAttendeeId)
            : q.Where(a => a.UserId == reg.UserId);
        db.ScheduleItemAttendances.RemoveRange(await q.ToListAsync());
    }

    private static ScheduleItemRegistration NewRegistration(
        ScheduleItem option, Guid slotId, Guid? guestId, Guid? userId, AttendanceMethod method, Guid? byUserId) => new()
    {
        ScheduleItemRegistrationId = Guid.NewGuid(),
        ScheduleItemId             = option.ScheduleItemId,
        SlotScheduleItemId         = slotId,
        GuestAttendeeId            = guestId,
        UserId                     = userId,
        RegisteredAt               = CampTime.Now,
        Method                     = method,
        RegisteredByUserId         = byUserId
    };
}
