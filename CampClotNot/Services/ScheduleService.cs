using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public record ScheduleEventDto(
    Guid ScheduleEventId,
    Guid CampEventId,
    DateOnly CampDay,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    string Title,
    string? Description,
    string? LocationDisplayName,
    ScheduleEventType EventType,
    bool AppliesToAllGroups,
    int? MaxCapacity,
    List<Guid> GroupIds
);

public class ScheduleService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<ScheduleEvent>> GetByEventAsync(Guid campEventId)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleEvents
            .Where(e => e.CampEventId == campEventId)
            .Include(e => e.EventGroups).ThenInclude(eg => eg.Group)
            .OrderBy(e => e.CampDay).ThenBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<List<ScheduleEvent>> GetForDayAsync(Guid campEventId, DateOnly day)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleEvents
            .Where(e => e.CampEventId == campEventId && e.CampDay == day)
            .Include(e => e.EventGroups).ThenInclude(eg => eg.Group)
            .OrderBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<ScheduleEvent> UpsertAsync(ScheduleEventDto dto, Guid userId)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.ScheduleEvents
            .Include(e => e.EventGroups)
            .FirstOrDefaultAsync(e => e.ScheduleEventId == dto.ScheduleEventId);

        if (existing is null)
        {
            var ev = new ScheduleEvent
            {
                ScheduleEventId     = dto.ScheduleEventId == Guid.Empty ? Guid.NewGuid() : dto.ScheduleEventId,
                CampEventId         = dto.CampEventId,
                CampDay             = dto.CampDay,
                StartTime           = dto.StartTime,
                EndTime             = dto.EndTime,
                Title               = dto.Title,
                Description         = dto.Description,
                LocationDisplayName = dto.LocationDisplayName,
                EventType           = dto.EventType,
                AppliesToAllGroups  = dto.AppliesToAllGroups,
                MaxCapacity         = dto.MaxCapacity,
                CreatedBy           = userId,
                UpdatedAt           = DateTime.UtcNow
            };
            if (!dto.AppliesToAllGroups)
                ev.EventGroups = dto.GroupIds
                    .Select(gid => new ScheduleEventGroup { ScheduleEventId = ev.ScheduleEventId, GroupId = gid })
                    .ToList();
            db.ScheduleEvents.Add(ev);
            await db.SaveChangesAsync();
            return ev;
        }
        else
        {
            existing.CampDay             = dto.CampDay;
            existing.StartTime           = dto.StartTime;
            existing.EndTime             = dto.EndTime;
            existing.Title               = dto.Title;
            existing.Description         = dto.Description;
            existing.LocationDisplayName = dto.LocationDisplayName;
            existing.EventType           = dto.EventType;
            existing.AppliesToAllGroups  = dto.AppliesToAllGroups;
            existing.MaxCapacity         = dto.MaxCapacity;
            existing.UpdatedAt           = DateTime.UtcNow;

            db.ScheduleEventGroups.RemoveRange(existing.EventGroups);
            existing.EventGroups = dto.AppliesToAllGroups
                ? []
                : dto.GroupIds
                    .Select(gid => new ScheduleEventGroup { ScheduleEventId = existing.ScheduleEventId, GroupId = gid })
                    .ToList();

            await db.SaveChangesAsync();
            return existing;
        }
    }

    public async Task DeleteAsync(Guid scheduleEventId)
    {
        using var db = factory.CreateDbContext();
        var ev = await db.ScheduleEvents
            .Include(e => e.EventGroups)
            .FirstOrDefaultAsync(e => e.ScheduleEventId == scheduleEventId);
        if (ev is null) return;
        db.ScheduleEventGroups.RemoveRange(ev.EventGroups);
        db.ScheduleEvents.Remove(ev);
        await db.SaveChangesAsync();
    }
}
