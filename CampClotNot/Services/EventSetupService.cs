using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

/// Opt-in copies for "Copy setup from…" on /admin/events. Enabled capabilities and
/// schedule item types are always copied; these are the event-scoped extras.
/// #311 adds per-event staff assignments here as one more flag + copy step.
public record EventCopyOptions(bool Sponsors, bool StaffDirectory, bool Activities, bool EventStaff = false)
{
    public static readonly EventCopyOptions None = new(false, false, false);
}

public record NewEventRequest(
    string Name, DateOnly EffDate, DateOnly ExpDate, Guid EventTypeId, bool IsActive, string? GuestCode,
    IReadOnlySet<Guid> CapabilityIds,
    string? ThemePresetKey,          // null = copy the source event's theme (when there is a source)
    Guid? CopyFromEventId,
    EventCopyOptions Copy);

public record CopyCounts(int Sponsors, int StaffDirectory, int Activities, int EventStaff);

public class EventSetupService(
    IDbContextFactory<AppDbContext> factory,
    ActiveEventService activeEventSvc,
    CapabilityService capSvc)
{
    public async Task<CopyCounts> GetCopyCountsAsync(Guid sourceEventId)
    {
        using var db = factory.CreateDbContext();
        return new CopyCounts(
            await db.Sponsors.CountAsync(s => s.EventId == sourceEventId),
            await db.StaffMembers.CountAsync(s => s.CampEventId == sourceEventId),
            await db.Activities.CountAsync(a => a.EventId == sourceEventId),
            await db.EventStaff.CountAsync(s => s.EventId == sourceEventId && s.User.IsActive));
    }

    /// Creates the event, its own theme row, and every copied row in one SaveChanges, so a
    /// failure leaves nothing half-copied. Throws InvalidOperationException with a
    /// user-facing message when the guest code is already taken.
    public async Task<Guid> CreateAsync(NewEventRequest req)
    {
        using var db = factory.CreateDbContext();

        var guestCode = string.IsNullOrWhiteSpace(req.GuestCode) ? null : req.GuestCode.Trim().ToUpperInvariant();
        if (guestCode is not null && await db.Events.AnyAsync(e => e.GuestCode == guestCode))
            throw new InvalidOperationException("That guest code is already used by another event.");

        if (req.IsActive)
        {
            foreach (var other in await db.Events.Where(e => e.IsActive).ToListAsync())
                other.IsActive = false;
        }

        var name = req.Name.Trim();
        var eventId = Guid.NewGuid();
        var sourceId = req.CopyFromEventId;

        // Theme — always a new row owned by this event.
        Theme? sourceTheme = null;
        if (sourceId is not null && req.ThemePresetKey is null)
        {
            sourceTheme = await db.Events.Where(e => e.EventId == sourceId)
                .Select(e => e.Theme).FirstOrDefaultAsync();
        }
        var theme = sourceTheme is not null
            ? ThemeCloner.Clone(sourceTheme, name, req.EffDate.Year)
            : ThemeCloner.FromPreset(ThemePresets.Get(req.ThemePresetKey) ?? ThemePresets.Classic, name, req.EffDate.Year);
        db.Themes.Add(theme);

        db.Events.Add(new Event
        {
            EventId     = eventId,
            Name        = name,
            EffDate     = req.EffDate,
            ExpDate     = req.ExpDate,
            EventTypeId = req.EventTypeId,
            ThemeId     = theme.ThemeId,
            IsActive    = req.IsActive,
            GuestCode   = guestCode
        });

        foreach (var capId in req.CapabilityIds)
            db.EventCapabilities.Add(new EventCapability { EventCapabilityId = Guid.NewGuid(), EventId = eventId, CapabilityId = capId });

        if (sourceId is { } src)
        {
            var typeIds = await db.EventScheduleItemTypes.Where(t => t.EventId == src)
                .Select(t => t.ScheduleItemTypeId).ToListAsync();
            foreach (var typeId in typeIds)
                db.EventScheduleItemTypes.Add(new EventScheduleItemType { EventId = eventId, ScheduleItemTypeId = typeId });

            if (req.Copy.Sponsors)
            {
                foreach (var s in await db.Sponsors.AsNoTracking().Where(s => s.EventId == src).ToListAsync())
                {
                    db.Sponsors.Add(new Sponsor
                    {
                        SponsorId = Guid.NewGuid(), EventId = eventId,
                        Name = s.Name, LogoUrl = s.LogoUrl, LogoData = s.LogoData, LogoContentType = s.LogoContentType,
                        Website = s.Website, ContactName = s.ContactName, Phone = s.Phone, SortOrder = s.SortOrder
                    });
                }
            }

            if (req.Copy.StaffDirectory)
            {
                foreach (var m in await db.StaffMembers.AsNoTracking().Where(m => m.CampEventId == src).ToListAsync())
                {
                    db.StaffMembers.Add(new StaffMember
                    {
                        StaffMemberId = Guid.NewGuid(), CampEventId = eventId,
                        DisplayName = m.DisplayName, RoleTitle = m.RoleTitle, Phone = m.Phone, Email = m.Email,
                        PhotoData = m.PhotoData, PhotoContentType = m.PhotoContentType, PhotoObjectPosition = m.PhotoObjectPosition,
                        AvatarEmoji = m.AvatarEmoji, IsVisible = m.IsVisible, SortOrder = m.SortOrder, LinkedUserId = m.LinkedUserId
                    });
                }
            }

            if (req.Copy.Activities)
            {
                // Activities only — board spaces and scripted games are the old event's game setup.
                foreach (var a in await db.Activities.AsNoTracking().Where(a => a.EventId == src).ToListAsync())
                {
                    db.Activities.Add(new Activity
                    {
                        ActivityId = Guid.NewGuid(), EventId = eventId,
                        ActivityTypeId = a.ActivityTypeId, LocationId = a.LocationId,
                        Name = a.Name, Description = a.Description, ShowInSpinner = a.ShowInSpinner
                    });
                }
            }

            if (req.Copy.EventStaff)
            {
                // People and their role at the event. Not groups — groups aren't copied, so the
                // source event's group ids would point at the wrong event.
                var staff = await db.EventStaff.AsNoTracking()
                    .Where(s => s.EventId == src && s.User.IsActive)
                    .Select(s => new { s.UserId, s.UserRoleId })
                    .ToListAsync();
                foreach (var s in staff)
                {
                    db.EventStaff.Add(new EventStaff
                    {
                        EventStaffId = Guid.NewGuid(), EventId = eventId,
                        UserId = s.UserId, UserRoleId = s.UserRoleId, AddedAt = CampTime.Now
                    });
                }
            }
        }

        await db.SaveChangesAsync();

        activeEventSvc.InvalidateCache();
        capSvc.InvalidateCache(eventId);
        return eventId;
    }
}
