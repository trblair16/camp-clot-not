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
    string? LocationOther = null
);

public class ScheduleService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    public async Task<List<ScheduleItem>> GetByEventAsync(Guid campEventId)
    {
        return await cache.GetOrCreateAsync($"sched.ev.{campEventId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            using var db = factory.CreateDbContext();
            return await db.ScheduleItems
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
        }) ?? [];
    }

    public async Task<List<ScheduleItem>> GetForDayAsync(Guid campEventId, DateOnly day)
    {
        return await cache.GetOrCreateAsync($"sched.day.{campEventId}.{day:yyyyMMdd}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            using var db = factory.CreateDbContext();
            return await db.ScheduleItems
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
        }) ?? [];
    }

    public async Task<ScheduleItem> UpsertAsync(ScheduleItemDto dto, Guid userId)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.ScheduleItems
            .Include(e => e.ItemGroups)
            .FirstOrDefaultAsync(e => e.ScheduleItemId == dto.ScheduleItemId);

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
                PresenterName      = dto.PresenterName,
                PresenterBio       = dto.PresenterBio,
                CreatedBy          = userId,
                UpdatedAt          = DateTime.UtcNow,
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
            existing.PresenterName      = dto.PresenterName;
            existing.PresenterBio       = dto.PresenterBio;
            existing.UpdatedAt          = DateTime.UtcNow;

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

        cache.Remove($"sched.ev.{dto.CampEventId}");
        cache.Remove($"sched.day.{dto.CampEventId}.{dto.CampDay:yyyyMMdd}");
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
        db.ScheduleItems.Remove(ev);
        await db.SaveChangesAsync();
        cache.Remove($"sched.ev.{eventId}");
        cache.Remove($"sched.day.{eventId}.{day:yyyyMMdd}");
    }
}
