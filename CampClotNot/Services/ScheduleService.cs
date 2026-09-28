using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public record GroupAssignmentDto(Guid GroupId, Guid? ActivityId, Guid? LocationId, string? Note);

public record ScheduleItemDto(
    Guid ScheduleItemId,
    Guid CampEventId,
    DateOnly CampDay,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    string Title,
    string? Description,
    Guid? LocationId,
    Guid? ActivityId,
    Guid ScheduleItemTypeId,
    bool AppliesToAllGroups,
    int? MaxCapacity,
    List<GroupAssignmentDto> Assignments,
    string? PresenterName = null,
    string? PresenterBio = null,
    string? LocationOther = null,
    bool TrackAttendance = false,
    SelfCheckInMode SelfCheckInMode = SelfCheckInMode.ButtonAndQr,
    bool IsBreakoutSlot = false,
    bool AllowSelfSignup = true,
    Guid? ParentScheduleItemId = null
);

public class ScheduleService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    public async Task<List<ScheduleItem>> GetByEventAsync(Guid campEventId)
    {
        return await cache.GetOrCreateAsync($"sched.ev.{campEventId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            using var db = factory.CreateDbContext();
            var items = await db.ScheduleItems
                .AsNoTracking()
                .Where(e => e.CampEventId == campEventId)
                .Include(e => e.ScheduleItemType)
                .Include(e => e.Location)
                .Include(e => e.Activity)
                .Include(e => e.ItemGroups)
                    .ThenInclude(eg => eg.Group)
                .Include(e => e.ItemGroups)
                    .ThenInclude(eg => eg.Activity)
                .Include(e => e.ItemGroups)
                    .ThenInclude(eg => eg.Location)
                .OrderBy(e => e.CampDay).ThenBy(e => e.StartTime)
                .ToListAsync();
            foreach (var item in items)
            {
                if (item.Location is not null) item.Location.ImageData = null;
                foreach (var ig in item.ItemGroups)
                    if (ig.Location is not null) ig.Location.ImageData = null;
            }
            return items;
        }) ?? [];
    }

    public async Task<List<ScheduleItem>> GetForDayAsync(Guid campEventId, DateOnly day)
    {
        return await cache.GetOrCreateAsync($"sched.day.{campEventId}.{day:yyyyMMdd}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            using var db = factory.CreateDbContext();
            var items = await db.ScheduleItems
                .AsNoTracking()
                .Where(e => e.CampEventId == campEventId && e.CampDay == day)
                .Include(e => e.ScheduleItemType)
                .Include(e => e.Location)
                .Include(e => e.Activity)
                .Include(e => e.ItemGroups)
                    .ThenInclude(eg => eg.Group)
                .Include(e => e.ItemGroups)
                    .ThenInclude(eg => eg.Activity)
                .Include(e => e.ItemGroups)
                    .ThenInclude(eg => eg.Location)
                .OrderBy(e => e.StartTime)
                .ToListAsync();
            foreach (var item in items)
            {
                if (item.Location is not null) item.Location.ImageData = null;
                foreach (var ig in item.ItemGroups)
                    if (ig.Location is not null) ig.Location.ImageData = null;
            }
            return items;
        }) ?? [];
    }

    /// <summary>
    /// Throws InvalidOperationException with a user-facing message when the breakout fields
    /// don't fit: one level of nesting, options on their slot's day, and no moving an option
    /// that already has sign-ups to a different slot.
    /// </summary>
    public async Task<ScheduleItem> UpsertAsync(ScheduleItemDto dto, Guid userId)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.ScheduleItems
            .Include(e => e.ItemGroups)
            .FirstOrDefaultAsync(e => e.ScheduleItemId == dto.ScheduleItemId);

        if (dto.ParentScheduleItemId is { } parentId)
        {
            if (dto.IsBreakoutSlot)
                throw new InvalidOperationException("A breakout slot can't also be an option in another slot.");
            var parent = await db.ScheduleItems.AsNoTracking().FirstOrDefaultAsync(e => e.ScheduleItemId == parentId)
                ?? throw new InvalidOperationException("That breakout slot no longer exists.");
            if (!parent.IsBreakoutSlot || parent.CampEventId != dto.CampEventId)
                throw new InvalidOperationException("Options can only be added to a breakout slot in the same event.");
            if (parent.CampDay != dto.CampDay)
                throw new InvalidOperationException("An option has to be on the same day as its slot.");
        }
        if (existing is not null)
        {
            if (existing.ParentScheduleItemId is { } oldParent && oldParent != dto.ParentScheduleItemId
                && await db.ScheduleItemRegistrations.AnyAsync(r => r.ScheduleItemId == existing.ScheduleItemId))
                throw new InvalidOperationException("People are signed up for this option. Move or remove them on Breakouts before changing its slot.");
            if (existing.IsBreakoutSlot && !dto.IsBreakoutSlot
                && await db.ScheduleItems.AnyAsync(e => e.ParentScheduleItemId == existing.ScheduleItemId))
                throw new InvalidOperationException("This slot still has options. Delete them or move them first.");
            if (!existing.IsBreakoutSlot && dto.IsBreakoutSlot && existing.ParentScheduleItemId is null
                && await db.ScheduleItemAttendances.AnyAsync(a => a.ScheduleItemId == existing.ScheduleItemId))
                throw new InvalidOperationException("People have checked in to this item, so it can't become a breakout slot.");
        }
        var oldDay = existing?.CampDay;

        ScheduleItem result;
        if (existing is null)
        {
            var ev = new ScheduleItem
            {
                ScheduleItemId     = dto.ScheduleItemId == Guid.Empty ? Guid.NewGuid() : dto.ScheduleItemId,
                CampEventId        = dto.CampEventId,
                CampDay            = dto.CampDay,
                StartTime          = dto.StartTime,
                EndTime            = dto.EndTime,
                Title              = dto.Title,
                Description        = dto.Description,
                LocationId         = dto.LocationId,
                LocationOther      = dto.LocationOther,
                ActivityId         = dto.ActivityId,
                ScheduleItemTypeId = dto.ScheduleItemTypeId,
                AppliesToAllGroups = dto.AppliesToAllGroups,
                MaxCapacity        = dto.MaxCapacity,
                TrackAttendance    = !dto.IsBreakoutSlot && dto.TrackAttendance,
                SelfCheckInMode    = dto.SelfCheckInMode,
                IsBreakoutSlot     = dto.IsBreakoutSlot,
                AllowSelfSignup    = dto.AllowSelfSignup,
                ParentScheduleItemId = dto.ParentScheduleItemId,
                PresenterName      = dto.PresenterName,
                PresenterBio       = dto.PresenterBio,
                CreatedBy          = userId,
                UpdatedAt          = CampTime.Now,
                ItemGroups         = dto.Assignments
                    .Select(a => new ScheduleItemGroup
                    {
                        ScheduleItemId = Guid.Empty,
                        GroupId        = a.GroupId,
                        ActivityId     = a.ActivityId,
                        LocationId     = a.LocationId,
                        Note           = a.Note
                    }).ToList()
            };
            db.ScheduleItems.Add(ev);
            await db.SaveChangesAsync();
            result = ev;
        }
        else
        {
            existing.CampDay            = dto.CampDay;
            existing.StartTime          = dto.StartTime;
            existing.EndTime            = dto.EndTime;
            existing.Title              = dto.Title;
            existing.Description        = dto.Description;
            existing.LocationId         = dto.LocationId;
            existing.LocationOther      = dto.LocationOther;
            existing.ActivityId         = dto.ActivityId;
            existing.ScheduleItemTypeId = dto.ScheduleItemTypeId;
            existing.AppliesToAllGroups = dto.AppliesToAllGroups;
            existing.MaxCapacity        = dto.MaxCapacity;
            existing.TrackAttendance    = !dto.IsBreakoutSlot && dto.TrackAttendance;
            existing.SelfCheckInMode    = dto.SelfCheckInMode;
            existing.IsBreakoutSlot     = dto.IsBreakoutSlot;
            existing.AllowSelfSignup    = dto.AllowSelfSignup;
            existing.ParentScheduleItemId = dto.ParentScheduleItemId;

            // Options live on their slot's day, so moving a slot moves its options with it.
            if (existing.IsBreakoutSlot && oldDay != dto.CampDay)
            {
                foreach (var o in await db.ScheduleItems.Where(o => o.ParentScheduleItemId == existing.ScheduleItemId).ToListAsync())
                    o.CampDay = dto.CampDay;
            }
            existing.PresenterName      = dto.PresenterName;
            existing.PresenterBio       = dto.PresenterBio;
            existing.UpdatedAt          = CampTime.Now;

            db.ScheduleItemGroups.RemoveRange(existing.ItemGroups);
            existing.ItemGroups = dto.Assignments
                .Select(a => new ScheduleItemGroup
                {
                    ScheduleItemId = existing.ScheduleItemId,
                    GroupId        = a.GroupId,
                    ActivityId     = a.ActivityId,
                    LocationId     = a.LocationId,
                    Note           = a.Note
                }).ToList();

            await db.SaveChangesAsync();
            result = existing;
        }

        Invalidate(dto.CampEventId, dto.CampDay);
        if (oldDay is { } d && d != dto.CampDay) Invalidate(dto.CampEventId, d);
        return result;
    }

    public async Task DeleteAsync(Guid scheduleItemId)
    {
        using var db = factory.CreateDbContext();
        var ev = await db.ScheduleItems
            .Include(e => e.ItemGroups)
            .FirstOrDefaultAsync(e => e.ScheduleItemId == scheduleItemId);
        if (ev is null) return;
        var eventId = ev.CampEventId;
        var day     = ev.CampDay;
        db.ScheduleItemGroups.RemoveRange(ev.ItemGroups);
        // A slot's options go with it (their sign-ups and check-ins cascade in the database).
        var options = await db.ScheduleItems.Include(o => o.ItemGroups)
            .Where(o => o.ParentScheduleItemId == scheduleItemId).ToListAsync();
        foreach (var o in options) db.ScheduleItemGroups.RemoveRange(o.ItemGroups);
        db.ScheduleItems.RemoveRange(options);
        db.ScheduleItems.Remove(ev);
        await db.SaveChangesAsync();
        Invalidate(eventId, day);
    }

    /// <summary>For a slot: how many options and sign-ups a delete would take with it.</summary>
    public async Task<(int Options, int SignUps)> GetSlotDeleteImpactAsync(Guid slotId)
    {
        using var db = factory.CreateDbContext();
        return (await db.ScheduleItems.CountAsync(o => o.ParentScheduleItemId == slotId),
                await db.ScheduleItemRegistrations.CountAsync(r => r.SlotScheduleItemId == slotId));
    }

    /// <summary>Evicts the cached schedule for the event and day (after any write to its items).</summary>
    public void Invalidate(Guid eventId, DateOnly day)
    {
        cache.Remove($"sched.ev.{eventId}");
        cache.Remove($"sched.day.{eventId}.{day:yyyyMMdd}");
    }
}
