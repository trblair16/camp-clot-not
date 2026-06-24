using System.Text.Json;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using CampClotNot.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public record BowserFace(string Label, string Description, string Emoji, int CoinDelta, int StarDelta);

public class BowserEventService(
    IDbContextFactory<AppDbContext> factory,
    IHubContext<LiveHub> hub,
    TransactionService txSvc,
    ILogger<BowserEventService> logger)
{
    public static readonly BowserFace[] DefaultFaces =
    [
        new("−50 Coins",  "Bowser steals 50 coins!",         "💀", -50, 0),
        new("−30 Coins",  "Bowser swipes 30 coins!",         "👎", -30, 0),
        new("−20 Coins",  "Bowser nabs 20 coins!",           "😤", -20, 0),
        new("+10 Coins",  "Bowser drops 10 coins!",          "😅", +10, 0),
        new("+15 Coins",  "Bowser fumbles 15 coins!",        "😏", +15, 0),
        new("−5 Coins",   "Bowser pinches 5 coins!",           "😈", -5, 0),
    ];

    private static readonly Random _rng = new();
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public static BowserFace[] ParseFaces(string? facesJson)
    {
        if (string.IsNullOrWhiteSpace(facesJson)) return DefaultFaces;
        try
        {
            var faces = JsonSerializer.Deserialize<BowserFace[]>(facesJson, _json);
            return faces is { Length: > 0 } ? faces : DefaultFaces;
        }
        catch { return DefaultFaces; }
    }

    public async Task<List<BowserScript>> GetScriptsAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.BowserScripts
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task<BowserScript?> GetScriptAsync(Guid scriptId)
    {
        using var db = factory.CreateDbContext();
        return await db.BowserScripts.FindAsync(scriptId);
    }

    public async Task UpsertScriptAsync(BowserScript script)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.BowserScripts.FindAsync(script.BowserScriptId);
        if (existing is null)
        {
            script.BowserScriptId = Guid.NewGuid();
            db.BowserScripts.Add(script);
        }
        else
        {
            existing.Name = script.Name;
            existing.SortOrder = script.SortOrder;
            existing.FacesJson = script.FacesJson;
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteScriptAsync(Guid scriptId)
    {
        using var db = factory.CreateDbContext();
        var script = await db.BowserScripts.FindAsync(scriptId);
        if (script is not null)
        {
            db.BowserScripts.Remove(script);
            await db.SaveChangesAsync();
        }
    }

    public async Task<(int faceIndex, BowserFace face)> RollAsync(Guid groupId, Guid eventId, string rolledBy, Guid? scriptId = null)
    {
        BowserFace[] faces = DefaultFaces;
        if (scriptId.HasValue)
        {
            var script = await GetScriptAsync(scriptId.Value);
            faces = ParseFaces(script?.FacesJson);
        }

        var faceIndex = _rng.Next(faces.Length);
        var face = faces[faceIndex];

        using var db = factory.CreateDbContext();
        var group = await db.Groups.FindAsync(groupId);
        var groupName = group?.Name ?? "Unknown";

        if (face.CoinDelta != 0)
        {
            var coinType = await db.CurrencyTypes.FirstOrDefaultAsync(c => c.SystemName == "Primary");
            if (coinType is not null)
                await txSvc.PostAsync(groupId, coinType.CurrencyTypeId, face.CoinDelta, rolledBy, $"Bowser Event: {face.Label}");
        }

        if (face.StarDelta != 0)
        {
            var starType = await db.CurrencyTypes.FirstOrDefaultAsync(c => c.SystemName == "Prestige");
            if (starType is not null)
                await txSvc.PostAsync(groupId, starType.CurrencyTypeId, face.StarDelta, rolledBy, $"Bowser Event: {face.Label}");
        }

        logger.LogInformation("Bowser roll: {Group} got face {Face} ({Label})", groupName, faceIndex, face.Label);

        return (faceIndex, face);
    }

    public async Task BroadcastRollStart(Guid groupId, string groupName, string groupColor, BowserFace[] faces)
    {
        var facesPayload = faces.Select(f => new { f.Emoji, f.Label }).ToArray();
        await hub.Clients.All.SendAsync("BowserRollStarted", groupId, groupName, groupColor, facesPayload);
    }

    public async Task BroadcastResult(Guid groupId, int faceIndex, string label, string description)
    {
        await hub.Clients.All.SendAsync("BowserRollResult", groupId, faceIndex, label, description);
    }

    public async Task BroadcastReset()
    {
        await hub.Clients.All.SendAsync("BowserReset");
    }
}
