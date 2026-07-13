using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class CapabilityService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private static string EventKey(Guid eventId) => $"cap.ev.{eventId}";

    public async Task<List<Capability>> GetEnabledCapabilitiesAsync(Guid eventId)
    {
        return await cache.GetOrCreateAsync(EventKey(eventId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            using var db = factory.CreateDbContext();
            return await db.EventCapabilities
                .Where(e => e.EventId == eventId)
                .Include(e => e.Capability)
                .Select(e => e.Capability)
                .ToListAsync();
        }) ?? [];
    }

    public async Task<bool> IsEnabledAsync(Guid eventId, Feature feature)
    {
        var enabled = await GetEnabledCapabilitiesAsync(eventId);
        return enabled.Any(c => c.SystemName == feature.ToString());
    }

    public async Task SetEventCapabilityAsync(Guid eventId, Guid capabilityId, bool enabled)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.EventCapabilities
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.CapabilityId == capabilityId);
        if (enabled && existing is null)
        {
            db.EventCapabilities.Add(new EventCapability
            {
                EventCapabilityId = Guid.NewGuid(),
                EventId           = eventId,
                CapabilityId      = capabilityId
            });
        }
        else if (!enabled && existing is not null)
        {
            db.EventCapabilities.Remove(existing);
        }
        await db.SaveChangesAsync();
        cache.Remove(EventKey(eventId));
    }

    public void InvalidateCache(Guid eventId) => cache.Remove(EventKey(eventId));
}
