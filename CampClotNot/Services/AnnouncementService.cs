using System.Text.Json;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class AnnouncementService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache, PushNotificationService pushService)
{
    private const string FeedKey = "ann.feed";

    public async Task<List<Announcement>> GetFeedAsync()
    {
        return await cache.GetOrCreateAsync(FeedKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(20);
            using var db = factory.CreateDbContext();
            var now = CampTime.Now;

            var expired = await db.Announcements
                .Where(a => !a.IsArchived && a.ExpiresAt != null && a.ExpiresAt <= now)
                .ToListAsync();
            foreach (var a in expired) a.IsArchived = true;
            if (expired.Count > 0) await db.SaveChangesAsync();

            return await db.Announcements
                .Where(a => !a.IsArchived)
                .Include(a => a.Author)
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();
        }) ?? [];
    }

    public async Task<Announcement?> GetLatestPinnedAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.Announcements
            .Where(a => !a.IsArchived && a.IsPinned)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Announcement> PostAsync(string title, string body, AnnouncementPriority priority, Guid authorId, string[]? targetRoles = null)
    {
        using var db = factory.CreateDbContext();
        var announcement = new Announcement
        {
            AnnouncementId = Guid.NewGuid(),
            Title          = title,
            Body           = body,
            Priority       = priority,
            IsPinned       = false,
            AuthorId       = authorId,
            CreatedAt      = CampTime.Now,
            IsArchived     = false
        };
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync();
        cache.Remove(FeedKey);
        try
        {
            var pushTitle = priority == AnnouncementPriority.Urgent ? $"🚨 {title}" : $"📢 {title}";
            var pushBody = body.Length > 120 ? body[..117] + "..." : body;
            if (targetRoles is not null && targetRoles.Length > 0)
                await pushService.SendToRolesAsync(targetRoles, pushTitle, pushBody, "/hub/announcements");
            else
                await pushService.SendToAllAsync(pushTitle, pushBody, "/hub/announcements");
        }
        catch { }
        return announcement;
    }

    public async Task PinAsync(Guid id, bool isPinned)
    {
        using var db = factory.CreateDbContext();
        var a = await db.Announcements.FindAsync(id);
        if (a is null) return;
        a.IsPinned = isPinned;
        await db.SaveChangesAsync();
        cache.Remove(FeedKey);
    }

    public async Task ArchiveAsync(Guid id)
    {
        using var db = factory.CreateDbContext();
        var a = await db.Announcements.FindAsync(id);
        if (a is null) return;
        a.IsArchived = true;
        await db.SaveChangesAsync();
        cache.Remove(FeedKey);
    }

    public async Task ToggleReactionAsync(Guid announcementId, string emoji, Guid userId)
    {
        using var db = factory.CreateDbContext();
        var a = await db.Announcements.FindAsync(announcementId);
        if (a is null) return;

        var reactions = string.IsNullOrEmpty(a.ReactionsJson)
            ? new Dictionary<string, List<Guid>>()
            : JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(a.ReactionsJson) ?? new();

        if (!reactions.ContainsKey(emoji))
            reactions[emoji] = new List<Guid>();

        if (reactions[emoji].Contains(userId))
            reactions[emoji].Remove(userId);
        else
            reactions[emoji].Add(userId);

        if (reactions[emoji].Count == 0)
            reactions.Remove(emoji);

        a.ReactionsJson = reactions.Count > 0 ? JsonSerializer.Serialize(reactions) : null;
        await db.SaveChangesAsync();
        cache.Remove(FeedKey);
    }

    public static Dictionary<string, List<Guid>> ParseReactions(string? json) =>
        string.IsNullOrEmpty(json)
            ? new()
            : JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(json) ?? new();
}
