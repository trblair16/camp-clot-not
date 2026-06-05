using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class AnnouncementService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private const string FeedKey = "ann.feed";

    public async Task<List<Announcement>> GetFeedAsync()
    {
        return await cache.GetOrCreateAsync(FeedKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(20);
            using var db = factory.CreateDbContext();
            var now = DateTime.UtcNow;

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

    public async Task<Announcement> PostAsync(string title, string body, AnnouncementPriority priority, Guid authorId)
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
            CreatedAt      = DateTime.UtcNow,
            IsArchived     = false
        };
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync();
        cache.Remove(FeedKey);
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
}
