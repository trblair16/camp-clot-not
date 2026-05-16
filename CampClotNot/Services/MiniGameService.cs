using CampClotNot.Data;
using CampClotNot.Data.Entities;
using CampClotNot.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
    IHubContext<LiveHub> hub)
{
    private const string MiniGameCategory = "MinuteToWinIt";

    public async Task<List<Activity>> GetActivitiesAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.Activities
            .Where(a => a.EventId == eventId &&
                        a.ActivityType.Category.SystemName == MiniGameCategory)
            .Include(a => a.ActivityType)
                .ThenInclude(at => at.Category)
            .OrderBy(a => a.Name)
            .ToListAsync();
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
}
