using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class ActiveEventService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private const string CacheKey = "active-event";

    public async Task<Event?> GetActiveEventAsync()
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            using var db = factory.CreateDbContext();
            return await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.IsActive);
        });
    }

    public async Task<Guid> GetActiveEventIdAsync()
    {
        var ev = await GetActiveEventAsync();
        if (ev is null)
            throw new InvalidOperationException(
                "No active event is configured. An Admin must mark an event as Active on /admin/events.");
        return ev.EventId;
    }

    public void InvalidateCache() => cache.Remove(CacheKey);
}
