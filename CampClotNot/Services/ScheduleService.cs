using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public record GroupAssignmentDto(Guid GroupId, Guid? ActivityId, Guid? LocationId, string? Note);

public record ScheduleEventDto(
    Guid ScheduleEventId,
    Guid CampEventId,
    DateOnly CampDay,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    string Title,
    string? Description,
    Guid? LocationId,
    ScheduleEventType EventType,
    bool AppliesToAllGroups,
    int? MaxCapacity,
    List<GroupAssignmentDto> Assignments,
    string? PresenterName = null,
    string? PresenterBio = null
);

public class ScheduleService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<ScheduleEvent>> GetByEventAsync(Guid campEventId)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleEvents
            .Where(e => e.CampEventId == campEventId)
            .Include(e => e.Location)
            .Include(e => e.EventGroups)
                .ThenInclude(eg => eg.Group)
            .Include(e => e.EventGroups)
                .ThenInclude(eg => eg.Activity)
            .Include(e => e.EventGroups)
                .ThenInclude(eg => eg.Location)
            .OrderBy(e => e.CampDay).ThenBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<List<ScheduleEvent>> GetForDayAsync(Guid campEventId, DateOnly day)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleEvents
            .Where(e => e.CampEventId == campEventId && e.CampDay == day)
            .Include(e => e.Location)
            .Include(e => e.EventGroups)
                .ThenInclude(eg => eg.Group)
            .Include(e => e.EventGroups)
                .ThenInclude(eg => eg.Activity)
            .Include(e => e.EventGroups)
                .ThenInclude(eg => eg.Location)
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
                ScheduleEventId    = dto.ScheduleEventId == Guid.Empty ? Guid.NewGuid() : dto.ScheduleEventId,
                CampEventId        = dto.CampEventId,
                CampDay            = dto.CampDay,
                StartTime          = dto.StartTime,
                EndTime            = dto.EndTime,
                Title              = dto.Title,
                Description        = dto.Description,
                LocationId         = dto.LocationId,
                EventType          = dto.EventType,
                AppliesToAllGroups = dto.AppliesToAllGroups,
                MaxCapacity        = dto.MaxCapacity,
                PresenterName      = dto.PresenterName,
                PresenterBio       = dto.PresenterBio,
                CreatedBy          = userId,
                UpdatedAt          = DateTime.UtcNow,
                EventGroups        = dto.Assignments
                    .Select(a => new ScheduleEventGroup
                    {
                        ScheduleEventId = Guid.Empty, // set by EF after insert
                        GroupId         = a.GroupId,
                        ActivityId      = a.ActivityId,
                        LocationId      = a.LocationId,
                        Note            = a.Note
                    }).ToList()
            };
            db.ScheduleEvents.Add(ev);
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
            existing.EventType          = dto.EventType;
            existing.AppliesToAllGroups = dto.AppliesToAllGroups;
            existing.MaxCapacity        = dto.MaxCapacity;
            existing.PresenterName      = dto.PresenterName;
            existing.PresenterBio       = dto.PresenterBio;
            existing.UpdatedAt          = DateTime.UtcNow;

            db.ScheduleEventGroups.RemoveRange(existing.EventGroups);
            existing.EventGroups = dto.Assignments
                .Select(a => new ScheduleEventGroup
                {
                    ScheduleEventId = existing.ScheduleEventId,
                    GroupId         = a.GroupId,
                    ActivityId      = a.ActivityId,
                    LocationId      = a.LocationId,
                    Note            = a.Note
                }).ToList();

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
