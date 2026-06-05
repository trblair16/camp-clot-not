using CampClotNot.Data;
using CampClotNot.Data.Entities;
using CampClotNot.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public record MiniGameScriptView(
    Guid ScriptId,
    int CampDay,
    Guid ActivityId,
    string ActivityName,
    bool IsTriggered
);

public class MiniGameService(
    IDbContextFactory<AppDbContext> factory,
    IHubContext<LiveHub> hub,
    IMemoryCache cache)
{
    private const string MiniGameCategory = "MinuteToWinIt";
    private static string ActivitiesKey(Guid eventId) => $"mga.{eventId}";

    public async Task<List<Activity>> GetActivitiesAsync(Guid eventId)
    {
        return await cache.GetOrCreateAsync(ActivitiesKey(eventId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var db = factory.CreateDbContext();
            return await db.Activities
                .Where(a => a.EventId == eventId &&
                            a.ActivityType.Category.SystemName == MiniGameCategory)
                .Include(a => a.ActivityType)
                    .ThenInclude(at => at.Category)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }) ?? [];
    }

    public async Task<List<MiniGameScriptView>> GetScriptsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.ScriptedMiniGames
            .Where(s => s.EventId == eventId)
            .Include(s => s.Activity)
            .OrderBy(s => s.CampDay)
            .Select(s => new MiniGameScriptView(
                s.ScriptId,
                s.CampDay,
                s.ActivityId,
                s.Activity.Name,
                s.IsTriggered))
            .ToListAsync();
    }

    public async Task UpsertScriptAsync(Guid eventId, int campDay, Guid activityId)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.ScriptedMiniGames
            .FirstOrDefaultAsync(s => s.EventId == eventId && s.CampDay == campDay);

        if (existing is null)
        {
            db.ScriptedMiniGames.Add(new ScriptedMiniGame
            {
                ScriptId    = Guid.NewGuid(),
                EventId     = eventId,
                ActivityId  = activityId,
                CampDay     = campDay,
                IsTriggered = false
            });
        }
        else if (!existing.IsTriggered)
        {
            existing.ActivityId = activityId;
        }

        await db.SaveChangesAsync();
    }

    public async Task TriggerSpinAsync(int campDay)
    {
        await hub.Clients.All.SendAsync("MiniGameSpinTriggered", campDay);
    }

    // Returns the scripted activity so the trigger page can display the result locally.
    // Marks IsTriggered = true and broadcasts to all display clients.
    public async Task<(Guid activityId, string activityName)?> RevealAsync(Guid eventId, int campDay)
    {
        using var db = factory.CreateDbContext();
        var script = await db.ScriptedMiniGames
            .Include(s => s.Activity)
            .FirstOrDefaultAsync(s => s.EventId == eventId && s.CampDay == campDay);

        if (script is null || script.IsTriggered) return null;

        script.IsTriggered = true;
        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("MiniGameSpinRevealed", script.ActivityId, script.Activity.Name);
        return (script.ActivityId, script.Activity.Name);
    }

    public async Task ResetDayAsync(Guid eventId, int campDay)
    {
        using var db = factory.CreateDbContext();
        var script = await db.ScriptedMiniGames
            .FirstOrDefaultAsync(s => s.EventId == eventId && s.CampDay == campDay);
        if (script is null) return;
        script.IsTriggered = false;
        await db.SaveChangesAsync();
        await hub.Clients.All.SendAsync("MiniGameSpinReset");
    }

    public async Task UpsertActivityAsync(Guid activityId, Guid eventId, string name, string description, Guid? locationId = null)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.Activities.FindAsync(activityId);
        if (existing is null)
        {
            db.Activities.Add(new Activity
            {
                ActivityId     = activityId == Guid.Empty ? Guid.NewGuid() : activityId,
                EventId        = eventId,
                ActivityTypeId = SeedService.Id.ActTypeMtwiPlaceholder,
                Name           = name,
                Description    = description,
                LocationId     = locationId
            });
        }
        else
        {
            existing.Name        = name;
            existing.Description = description;
            existing.LocationId  = locationId;
        }
        await db.SaveChangesAsync();
        cache.Remove(ActivitiesKey(eventId));
    }

    // Returns false if the activity is referenced by a triggered script (cannot delete).
    public async Task<bool> DeleteActivityAsync(Guid activityId)
    {
        using var db = factory.CreateDbContext();
        var inUse = await db.ScriptedMiniGames.AnyAsync(s => s.ActivityId == activityId);
        if (inUse) return false;
        var activity = await db.Activities
            .Include(a => a.ActivityType)
            .FirstOrDefaultAsync(a => a.ActivityId == activityId);
        if (activity is null) return true;
        var eventId = activity.EventId;
        db.Activities.Remove(activity);
        await db.SaveChangesAsync();
        cache.Remove(ActivitiesKey(eventId));
        return true;
    }
}
