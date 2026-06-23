using CampClotNot.Data;
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
    public static readonly BowserFace[] Faces =
    [
        new("−50 Coins",  "Bowser steals 50 coins!",         "💀", -50, 0),
        new("−30 Coins",  "Bowser swipes 30 coins!",         "👎", -30, 0),
        new("−20 Coins",  "Bowser nabs 20 coins!",           "😤", -20, 0),
        new("+10 Coins",  "Bowser drops 10 coins!",          "😅", +10, 0),
        new("+15 Coins",  "Bowser fumbles 15 coins!",        "😏", +15, 0),
        new("FREE STAR",  "Bowser accidentally gives a star!","⭐",  0, 1),
    ];

    private static readonly Random _rng = new();

    public async Task<(int faceIndex, BowserFace face)> RollAsync(Guid groupId, Guid eventId, string rolledBy)
    {
        var faceIndex = _rng.Next(Faces.Length);
        var face = Faces[faceIndex];

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

    public async Task BroadcastRollStart(Guid groupId, string groupName, string groupColor)
    {
        await hub.Clients.All.SendAsync("BowserRollStarted", groupId, groupName, groupColor);
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
