using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class AnnouncementService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<Announcement>> GetFeedAsync()
    {
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
    }

    // Stub for future Dashboard banner — not called from Dashboard in v0.5.0
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
        return announcement;
    }

    public async Task PinAsync(Guid id, bool isPinned)
    {
        using var db = factory.CreateDbContext();
        var a = await db.Announcements.FindAsync(id);
        if (a is null) return;
        a.IsPinned = isPinned;
        await db.SaveChangesAsync();
    }

    public async Task ArchiveAsync(Guid id)
    {
        using var db = factory.CreateDbContext();
        var a = await db.Announcements.FindAsync(id);
        if (a is null) return;
        a.IsArchived = true;
        await db.SaveChangesAsync();
    }
}
