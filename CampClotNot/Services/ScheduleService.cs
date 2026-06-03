using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

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
    Guid ScheduleItemTypeId,
    bool AppliesToAllGroups,
    int? MaxCapacity,
    List<GroupAssignmentDto> Assignments,
    string? PresenterName = null,
    string? PresenterBio = null
);

public class ScheduleService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<ScheduleItem>> GetByEventAsync(Guid campEventId)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleItems
            .Where(e => e.CampEventId == campEventId)
            .Include(e => e.ScheduleItemType)
            .Include(e => e.Location)
            .Include(e => e.ItemGroups)
                .ThenInclude(eg => eg.Group)
            .Include(e => e.ItemGroups)
                .ThenInclude(eg => eg.Activity)
            .Include(e => e.ItemGroups)
                .ThenInclude(eg => eg.Location)
            .OrderBy(e => e.CampDay).ThenBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<List<ScheduleItem>> GetForDayAsync(Guid campEventId, DateOnly day)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleItems
            .Where(e => e.CampEventId == campEventId && e.CampDay == day)
            .Include(e => e.ScheduleItemType)
            .Include(e => e.Location)
            .Include(e => e.ItemGroups)
                .ThenInclude(eg => eg.Group)
            .Include(e => e.ItemGroups)
                .ThenInclude(eg => eg.Activity)
            .Include(e => e.ItemGroups)
                .ThenInclude(eg => eg.Location)
            .OrderBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<ScheduleItem> UpsertAsync(ScheduleItemDto dto, Guid userId)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.ScheduleItems
            .Include(e => e.ItemGroups)
            .FirstOrDefaultAsync(e => e.ScheduleItemId == dto.ScheduleItemId);

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
                        ScheduleItemId = Guid.Empty, // set by EF after insert
                        GroupId        = a.GroupId,
                        ActivityId     = a.ActivityId,
                        LocationId     = a.LocationId,
                        Note           = a.Note
                    }).ToList()
            };
            db.ScheduleItems.Add(ev);
            await db.SaveChangesAsync();
            return ev;
        }
        else
        {
            existing.CampDay            = dto.CampDay;
            existing.StartTime          = dto.StartTime;
            existing.EndTime            = dto.EndTime;
            existing.Title              = dto.Title;
            existing.Description        = dto.Description;
            existing.LocationId         = dto.LocationId;
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
            return existing;
        }
    }

    public async Task DeleteAsync(Guid scheduleItemId)
    {
        using var db = factory.CreateDbContext();
        var ev = await db.ScheduleItems
            .Include(e => e.ItemGroups)
            .FirstOrDefaultAsync(e => e.ScheduleItemId == scheduleItemId);
        if (ev is null) return;
        db.ScheduleItemGroups.RemoveRange(ev.ItemGroups);
        db.ScheduleItems.Remove(ev);
        await db.SaveChangesAsync();
    }
}
