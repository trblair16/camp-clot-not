using CampClotNot.Data;
using CampClotNot.Data.Entities;
using CampClotNot.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public record BoardSpaceView(
    Guid SpaceId,
    int SpaceIndex,
    float XPos,
    float YPos,
    string CategorySystemName,
    string ActivityName,
    Guid? LocationId = null
);

public class BoardService(
    IDbContextFactory<AppDbContext> factory,
    IHubContext<LiveHub> hub)
{
    private const int TotalSpaces = 20;

    public async Task<List<BoardSpaceView>> GetSpacesAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var spaces = await db.BoardSpaces
            .Where(s => s.EventId == eventId)
            .Include(s => s.Activity)
                .ThenInclude(a => a.ActivityType)
                    .ThenInclude(at => at.Category)
            .Include(s => s.Activity)
                .ThenInclude(a => a.Location)
            .OrderBy(s => s.SpaceIndex)
            .ToListAsync();

        return spaces.Select(s => new BoardSpaceView(
            s.SpaceId,
            s.SpaceIndex,
            s.XPos,
            s.YPos,
            s.Activity.ActivityType.Category.SystemName,
            s.Activity.Name,
            s.Activity.Location?.ImageData is not null ? s.Activity.LocationId : null
        )).ToList();
    }

    public async Task<Dictionary<Guid, int>> GetGroupPositionsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var groups = await db.Groups
            .Where(g => g.EventId == eventId)
            .Include(g => g.BoardPos)
            .ToListAsync();

        return groups
            .Where(g => g.BoardPos is not null)
            .ToDictionary(g => g.GroupId, g => g.BoardPos!.SpaceIndex);
    }

    public async Task<List<ScriptedBlockHit>> GetScriptedBlockHitsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.ScriptedBlockHits
            .Where(s => s.EventId == eventId)
            .Include(s => s.Group)
            .ToListAsync();
    }

    public async Task<ScriptedBlockHit?> GetScriptAsync(Guid groupId, Guid eventId, int campDay)
    {
        using var db = factory.CreateDbContext();
        return await db.ScriptedBlockHits
            .FirstOrDefaultAsync(s => s.GroupId == groupId && s.EventId == eventId && s.CampDay == campDay);
    }

    // Upsert: create if absent, update destination if not yet triggered
    public async Task UpsertScriptAsync(Guid groupId, Guid eventId, int campDay, int destinationSpaceIndex)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.ScriptedBlockHits
            .FirstOrDefaultAsync(s => s.GroupId == groupId && s.EventId == eventId && s.CampDay == campDay);

        if (existing is null)
        {
            db.ScriptedBlockHits.Add(new ScriptedBlockHit
            {
                ScriptId               = Guid.NewGuid(),
                GroupId                = groupId,
                EventId                = eventId,
                CampDay                = campDay,
                DestinationSpaceIndex  = destinationSpaceIndex,
                IsTriggered            = false
            });
        }
        else if (!existing.IsTriggered)
        {
            existing.DestinationSpaceIndex = destinationSpaceIndex;
        }

        await db.SaveChangesAsync();
    }

    // ── 3-phase admin-controlled block hit ──────────────────────────────────────
    //
    // Phase 1 (admin clicks "Launch"): broadcast ⁉️ to /display, return step count
    //         so Board.razor can cycle numbers on the dice block UI.
    // Phase 2 (admin clicks the block): broadcast the scripted number to /display.
    // Phase 3 (admin dismisses): step the token one space at a time (fire-and-forget).
    //
    // All three phases share the same script lookup so the number is always consistent.

    public async Task<int> Phase1TriggerAsync(Guid groupId, int campDay, Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var script = await db.ScriptedBlockHits
            .FirstOrDefaultAsync(s => s.GroupId == groupId && s.EventId == eventId && s.CampDay == campDay);
        if (script is null || script.IsTriggered) return -1;

        var pos          = await db.GroupBoardPositions.FindAsync(groupId);
        var currentSpace = pos?.SpaceIndex ?? 0;
        var steps        = ((script.DestinationSpaceIndex - currentSpace) + TotalSpaces) % TotalSpaces;
        if (steps == 0) steps = TotalSpaces;

        await hub.Clients.All.SendAsync("BlockHitTriggered", groupId, campDay);
        return steps;
    }

    public async Task Phase2RevealAsync(Guid groupId, int steps)
    {
        await hub.Clients.All.SendAsync("BlockHitNumberRevealed", groupId, steps);
    }

    // Fire-and-forget: steps the token and updates the DB when done
    public void Phase3StartStepping(Guid groupId, int campDay, Guid eventId)
    {
        _ = Task.Run(() => RunStepsAsync(groupId, campDay, eventId));
    }

    private async Task RunStepsAsync(Guid groupId, int campDay, Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var script = await db.ScriptedBlockHits
            .FirstOrDefaultAsync(s => s.GroupId == groupId && s.EventId == eventId && s.CampDay == campDay);
        if (script is null || script.IsTriggered) return;

        var pos          = await db.GroupBoardPositions.FindAsync(groupId);
        var currentSpace = pos?.SpaceIndex ?? 0;
        var destination  = script.DestinationSpaceIndex;
        var steps        = ((destination - currentSpace) + TotalSpaces) % TotalSpaces;
        if (steps == 0) steps = TotalSpaces;

        for (var i = 1; i <= steps; i++)
        {
            var spaceIndex = (currentSpace + i) % TotalSpaces;
            await hub.Clients.All.SendAsync("TokenMoveStep", groupId, spaceIndex);
            await Task.Delay(600);
        }

        await hub.Clients.All.SendAsync("TokenMoveDone", groupId, destination);

        if (pos is not null)
        {
            pos.SpaceIndex = destination;
            pos.UpdatedAt  = DateTime.UtcNow;
        }
        else
        {
            db.GroupBoardPositions.Add(new GroupBoardPos
            {
                GroupId    = groupId,
                SpaceIndex = destination,
                UpdatedAt  = DateTime.UtcNow
            });
        }

        script.IsTriggered = true;
        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("ScoresUpdated");
    }

    // Dev/testing helper: move all groups back to space 0 and un-trigger all scripts for a given day
    public async Task ResetDayAsync(Guid eventId, int campDay)
    {
        using var db = factory.CreateDbContext();

        var groupIds = await db.Groups
            .Where(g => g.EventId == eventId)
            .Select(g => g.GroupId)
            .ToListAsync();

        // Reset positions to space 0
        var positions = await db.GroupBoardPositions
            .Where(p => groupIds.Contains(p.GroupId))
            .ToListAsync();

        foreach (var p in positions)
        {
            p.SpaceIndex = 0;
            p.UpdatedAt  = DateTime.UtcNow;
        }

        // Un-trigger block hit scripts for the specified day
        var scripts = await db.ScriptedBlockHits
            .Where(s => s.EventId == eventId && s.CampDay == campDay)
            .ToListAsync();

        foreach (var s in scripts)
            s.IsTriggered = false;

        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("ScoresUpdated");
    }
}
