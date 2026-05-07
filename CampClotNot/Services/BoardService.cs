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
    string ActivityName
);

public class BoardService(
    IDbContextFactory<AppDbContext> factory,
    IHubContext<CampHub> hub)
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
            .OrderBy(s => s.SpaceIndex)
            .ToListAsync();

        return spaces.Select(s => new BoardSpaceView(
            s.SpaceId,
            s.SpaceIndex,
            s.XPos,
            s.YPos,
            s.Activity.ActivityType.Category.SystemName,
            s.Activity.Name
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

    // Fire-and-forget: returns immediately; the animation sequence runs in background
    public void StartBlockHit(Guid groupId, int campDay, Guid eventId)
    {
        _ = Task.Run(() => RunBlockHitAsync(groupId, campDay, eventId));
    }

    private async Task RunBlockHitAsync(Guid groupId, int campDay, Guid eventId)
    {
        using var db = factory.CreateDbContext();

        var script = await db.ScriptedBlockHits
            .FirstOrDefaultAsync(s => s.GroupId == groupId && s.EventId == eventId && s.CampDay == campDay);

        if (script is null || script.IsTriggered) return;

        var pos         = await db.GroupBoardPositions.FindAsync(groupId);
        var currentSpace = pos?.SpaceIndex ?? 0;
        var destination  = script.DestinationSpaceIndex;

        var steps = ((destination - currentSpace) + TotalSpaces) % TotalSpaces;
        if (steps == 0) steps = TotalSpaces;

        // Phase 1: announce
        await hub.Clients.All.SendAsync("BlockHitTriggered", groupId, campDay);

        // Phase 2: reveal roll number (~500ms for block squish animation)
        await Task.Delay(500);
        await hub.Clients.All.SendAsync("BlockHitNumberRevealed", groupId, steps);

        // Phase 3: step token (~1400ms for number reveal animation to settle)
        await Task.Delay(1400);
        for (var i = 1; i <= steps; i++)
        {
            var spaceIndex = (currentSpace + i) % TotalSpaces;
            await hub.Clients.All.SendAsync("TokenMoveStep", groupId, spaceIndex);
            await Task.Delay(400);
        }

        // Phase 4: land
        await hub.Clients.All.SendAsync("TokenMoveDone", groupId, destination);

        // Persist position and mark triggered
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

        // Landing space coin/star awards are manually logged by staff for now
        // Re-evaluate after Katelyn/Vicki confirm award values per space type
    }
}
