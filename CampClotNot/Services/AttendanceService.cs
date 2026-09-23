using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public enum CheckInResult { CheckedIn, AlreadyCheckedIn, NotTracked, OutsideWindow, NotAllowed }

public record RosterEntry(
    string FirstName,
    string LastName,
    bool IsGuest,
    string? RoleName,
    Guid? GuestAttendeeId,
    Guid? UserId,
    Guid? AttendanceId,
    DateTime? CheckedInAt,
    AttendanceMethod? Method,
    string? CheckedInByName)
{
    public bool IsCheckedIn => AttendanceId.HasValue;
    public string FullName => $"{FirstName} {LastName}";
    public string TypeLabel => IsGuest ? "Guest" : RoleName ?? "Staff";
}

public class AttendanceService(IDbContextFactory<AppDbContext> factory, GuestAccessService guestSvc)
{
    public static readonly TimeSpan OpensBefore     = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromHours(2);

    /// <summary>
    /// Self check-in is open from 30 min before start until the end time (or start + 2h when there
    /// is no end time), in camp wall-clock time. An end time earlier than the start crosses midnight.
    /// </summary>
    public static bool IsSelfCheckInOpen(ScheduleItem item, DateTime campNow)
    {
        if (!item.TrackAttendance) return false;
        var start = item.CampDay.ToDateTime(item.StartTime);
        var end = item.EndTime is { } e
            ? item.CampDay.ToDateTime(e).AddDays(e < item.StartTime ? 1 : 0)
            : start + DefaultDuration;
        return campNow >= start - OpensBefore && campNow <= end;
    }

    // Guest cookies carry GuestAttendeeId; staff cookies carry NameIdentifier. A legacy guest cookie
    // from before named guest identity (#304) has neither and can't check in.
    private (Guid? GuestId, Guid? UserId) ResolveAttendee(ClaimsPrincipal user)
    {
        var guestId = guestSvc.GetGuestAttendeeId(user);
        if (guestId.HasValue) return (guestId, null);
        if (guestSvc.GetGuestEventId(user).HasValue) return (null, null);
        return Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
            ? (null, uid)
            : (null, null);
    }

    private static IQueryable<ScheduleItemAttendance> ForAttendee(
        IQueryable<ScheduleItemAttendance> q, Guid? guestId, Guid? userId) =>
        guestId.HasValue
            ? q.Where(a => a.GuestAttendeeId == guestId)
            : q.Where(a => a.UserId == userId);

    // ── Hub ──────────────────────────────────────────────────────────────────

    public async Task<HashSet<Guid>> GetMyCheckedInItemIdsAsync(ClaimsPrincipal user, Guid eventId)
    {
        var (guestId, userId) = ResolveAttendee(user);
        if (guestId is null && userId is null) return [];

        using var db = factory.CreateDbContext();
        var ids = await ForAttendee(db.ScheduleItemAttendances, guestId, userId)
            .Where(a => a.ScheduleItem.CampEventId == eventId)
            .Select(a => a.ScheduleItemId)
            .ToListAsync();
        return ids.ToHashSet();
    }

    public async Task<CheckInResult> SelfCheckInAsync(ClaimsPrincipal user, Guid scheduleItemId)
    {
        var (guestId, userId) = ResolveAttendee(user);
        if (guestId is null && userId is null) return CheckInResult.NotAllowed;

        using var db = factory.CreateDbContext();
        var item = await db.ScheduleItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ScheduleItemId == scheduleItemId);
        if (item is null || !item.TrackAttendance) return CheckInResult.NotTracked;

        // Guests are scoped to the event they joined; staff users aren't event-scoped.
        if (guestId.HasValue && item.CampEventId != guestSvc.GetGuestEventId(user))
            return CheckInResult.NotAllowed;

        if (!IsSelfCheckInOpen(item, CampTime.Now)) return CheckInResult.OutsideWindow;

        return await InsertAsync(scheduleItemId, guestId, userId, AttendanceMethod.Self, null);
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    public async Task<List<ScheduleItem>> GetTrackedItemsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleItems.AsNoTracking()
            .Where(i => i.CampEventId == eventId && i.TrackAttendance)
            .OrderBy(i => i.CampDay).ThenBy(i => i.StartTime)
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, int>> GetCountsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleItemAttendances
            .Where(a => a.ScheduleItem.CampEventId == eventId)
            .GroupBy(a => a.ScheduleItemId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    /// <summary>
    /// Everyone who could attend the item — guests who joined its event, all active staff users —
    /// plus anyone already checked in who wouldn't otherwise be listed (e.g. a since-deactivated user).
    /// </summary>
    public async Task<List<RosterEntry>> GetRosterAsync(Guid scheduleItemId)
    {
        using var db = factory.CreateDbContext();
        var item = await db.ScheduleItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ScheduleItemId == scheduleItemId);
        if (item is null) return [];

        var checkIns = await db.ScheduleItemAttendances.AsNoTracking()
            .Where(a => a.ScheduleItemId == scheduleItemId)
            .Include(a => a.GuestAttendee)
            .Include(a => a.User).ThenInclude(u => u!.UserRole)
            .Include(a => a.CheckedInByUser)
            .ToListAsync();
        var guests = await db.GuestEventVisits.AsNoTracking()
            .Where(v => v.EventId == item.CampEventId)
            .Select(v => v.GuestAttendee)
            .ToListAsync();
        var users = await db.Users.AsNoTracking()
            .Include(u => u.UserRole)
            .Where(u => u.IsActive)
            .ToListAsync();

        var guestCheckIns = checkIns.Where(a => a.GuestAttendeeId.HasValue).ToDictionary(a => a.GuestAttendeeId!.Value);
        var userCheckIns  = checkIns.Where(a => a.UserId.HasValue).ToDictionary(a => a.UserId!.Value);

        var guestMap = guests.ToDictionary(g => g.GuestAttendeeId);
        foreach (var a in guestCheckIns.Values) guestMap.TryAdd(a.GuestAttendeeId!.Value, a.GuestAttendee!);
        var userMap = users.ToDictionary(u => u.UserId);
        foreach (var a in userCheckIns.Values) userMap.TryAdd(a.UserId!.Value, a.User!);

        var entries = new List<RosterEntry>();
        foreach (var g in guestMap.Values)
        {
            var a = guestCheckIns.GetValueOrDefault(g.GuestAttendeeId);
            entries.Add(new RosterEntry(g.FirstName, g.LastName, true, null, g.GuestAttendeeId, null,
                a?.ScheduleItemAttendanceId, a?.CheckedInAt, a?.Method, a?.CheckedInByUser?.FirstName));
        }
        foreach (var u in userMap.Values)
        {
            var a = userCheckIns.GetValueOrDefault(u.UserId);
            entries.Add(new RosterEntry(u.FirstName, u.LastName, false, u.UserRole?.Name, null, u.UserId,
                a?.ScheduleItemAttendanceId, a?.CheckedInAt, a?.Method, a?.CheckedInByUser?.FirstName));
        }

        return entries
            .OrderBy(e => e.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Admin check-in ignores the self check-in window (e.g. back-filling from a paper sheet).</summary>
    public async Task<CheckInResult> AdminCheckInAsync(Guid scheduleItemId, Guid? guestAttendeeId, Guid? userId, Guid adminUserId)
    {
        if (guestAttendeeId.HasValue == userId.HasValue)
            throw new ArgumentException("Exactly one of guestAttendeeId or userId must be set.");

        using var db = factory.CreateDbContext();
        var tracked = await db.ScheduleItems
            .AnyAsync(i => i.ScheduleItemId == scheduleItemId && i.TrackAttendance);
        if (!tracked) return CheckInResult.NotTracked;

        return await InsertAsync(scheduleItemId, guestAttendeeId, userId, AttendanceMethod.Admin, adminUserId);
    }

    /// <summary>
    /// Adds a guest by name (same normalization/matching as /join), records their visit to the
    /// item's event so they show on /admin/guests and other rosters, then checks them in.
    /// </summary>
    public async Task<CheckInResult> CheckInWalkInAsync(Guid scheduleItemId, string firstName, string lastName, Guid adminUserId)
    {
        using var db = factory.CreateDbContext();
        var item = await db.ScheduleItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ScheduleItemId == scheduleItemId);
        if (item is null || !item.TrackAttendance) return CheckInResult.NotTracked;

        var guest = await guestSvc.GetOrCreateGuestAsync(firstName, lastName);
        await guestSvc.RecordVisitAsync(guest.GuestAttendeeId, item.CampEventId);
        return await InsertAsync(scheduleItemId, guest.GuestAttendeeId, null, AttendanceMethod.Admin, adminUserId);
    }

    public async Task UndoCheckInAsync(Guid scheduleItemAttendanceId)
    {
        using var db = factory.CreateDbContext();
        var row = await db.ScheduleItemAttendances.FindAsync(scheduleItemAttendanceId);
        if (row is null) return;
        db.ScheduleItemAttendances.Remove(row);
        await db.SaveChangesAsync();
    }

    private async Task<CheckInResult> InsertAsync(
        Guid scheduleItemId, Guid? guestId, Guid? userId, AttendanceMethod method, Guid? checkedInByUserId)
    {
        using var db = factory.CreateDbContext();
        var exists = await ForAttendee(db.ScheduleItemAttendances, guestId, userId)
            .AnyAsync(a => a.ScheduleItemId == scheduleItemId);
        if (exists) return CheckInResult.AlreadyCheckedIn;

        db.ScheduleItemAttendances.Add(new ScheduleItemAttendance
        {
            ScheduleItemAttendanceId = Guid.NewGuid(),
            ScheduleItemId           = scheduleItemId,
            GuestAttendeeId          = guestId,
            UserId                   = userId,
            CheckedInAt              = CampTime.Now,
            Method                   = method,
            CheckedInByUserId        = checkedInByUserId
        });

        try
        {
            await db.SaveChangesAsync();
            return CheckInResult.CheckedIn;
        }
        catch (DbUpdateException)
        {
            // A concurrent request (double tap, two tabs, admin + self at once) won the race to insert
            // the same (ScheduleItemId, attendee) pair, tripping the DB-level unique index. The original
            // context may be in a bad state after the failed save, so re-query with a fresh context.
            using var db2 = factory.CreateDbContext();
            var winner = await ForAttendee(db2.ScheduleItemAttendances, guestId, userId)
                .AnyAsync(a => a.ScheduleItemId == scheduleItemId);
            if (winner) return CheckInResult.AlreadyCheckedIn;

            // Not a duplicate — don't silently swallow it.
            throw;
        }
    }

    // ── CSV export ───────────────────────────────────────────────────────────

    /// <summary>One row per roster entry (checked in or not), so it doubles as a sign-in sheet.</summary>
    public async Task<(string FileName, byte[] Content)?> BuildItemCsvAsync(Guid scheduleItemId)
    {
        using var db = factory.CreateDbContext();
        var item = await db.ScheduleItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ScheduleItemId == scheduleItemId);
        if (item is null) return null;

        var roster = await GetRosterAsync(scheduleItemId);
        var sb = new StringBuilder();
        sb.AppendLine(Csv("Last Name", "First Name", "Type", "Checked In", "Checked In At", "Method", "Checked In By"));
        foreach (var r in roster)
            sb.AppendLine(Csv(
                r.LastName, r.FirstName, r.TypeLabel,
                r.IsCheckedIn ? "Yes" : "No",
                r.CheckedInAt?.ToString("yyyy-MM-dd h:mm tt"),
                r.Method?.ToString(),
                r.CheckedInByName));

        return ($"attendance-{item.CampDay:yyyy-MM-dd}-{Slug(item.Title)}.csv", Utf8WithBom(sb));
    }

    /// <summary>One row per check-in across every tracked item in the event.</summary>
    public async Task<(string FileName, byte[] Content)?> BuildEventCsvAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return null;

        var rows = await db.ScheduleItemAttendances.AsNoTracking()
            .Where(a => a.ScheduleItem.CampEventId == eventId)
            .Include(a => a.ScheduleItem)
            .Include(a => a.GuestAttendee)
            .Include(a => a.User).ThenInclude(u => u!.UserRole)
            .Include(a => a.CheckedInByUser)
            .ToListAsync();

        var ordered = rows
            .Select(a => new
            {
                a,
                First = a.GuestAttendee?.FirstName ?? a.User?.FirstName ?? "",
                Last  = a.GuestAttendee?.LastName  ?? a.User?.LastName  ?? "",
                Type  = a.GuestAttendeeId.HasValue ? "Guest" : a.User?.UserRole?.Name ?? "Staff"
            })
            .OrderBy(x => x.a.ScheduleItem.CampDay)
            .ThenBy(x => x.a.ScheduleItem.StartTime)
            .ThenBy(x => x.a.ScheduleItem.Title)
            .ThenBy(x => x.Last, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.First, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine(Csv("Date", "Start", "Item", "Last Name", "First Name", "Type", "Checked In At", "Method", "Checked In By"));
        foreach (var x in ordered)
            sb.AppendLine(Csv(
                x.a.ScheduleItem.CampDay.ToString("yyyy-MM-dd"),
                x.a.ScheduleItem.StartTime.ToString("h:mm tt"),
                x.a.ScheduleItem.Title,
                x.Last, x.First, x.Type,
                x.a.CheckedInAt.ToString("yyyy-MM-dd h:mm tt"),
                x.a.Method.ToString(),
                x.a.CheckedInByUser?.FirstName));

        return ($"attendance-{Slug(ev.Name)}.csv", Utf8WithBom(sb));
    }

    // RFC 4180 quoting, plus a leading apostrophe on anything a spreadsheet would treat as a
    // formula — guest names are user-typed.
    private static string Csv(params string?[] fields) =>
        string.Join(",", fields.Select(f =>
        {
            var v = f ?? "";
            if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }));

    // BOM so Excel on Windows reads accented names as UTF-8.
    private static byte[] Utf8WithBom(StringBuilder sb) =>
        [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];

    private static string Slug(string s)
    {
        var slug = Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "export" : slug;
    }
}
