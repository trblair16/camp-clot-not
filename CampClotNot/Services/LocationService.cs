using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class LocationService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private const string AllKey = "loc.all";

    public async Task<List<Location>> GetAllAsync()
    {
        return await cache.GetOrCreateAsync(AllKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var db = factory.CreateDbContext();
            return await db.Locations.OrderBy(l => l.SortOrder).ThenBy(l => l.Name).ToListAsync();
        }) ?? [];
    }

    public async Task<Location> UpsertAsync(Location loc)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.Locations.FindAsync(loc.LocationId);
        if (existing is null)
        {
            if (loc.LocationId == Guid.Empty) loc.LocationId = Guid.NewGuid();
            db.Locations.Add(loc);
        }
        else
        {
            existing.Name        = loc.Name;
            existing.Description = loc.Description;
            existing.Capacity    = loc.Capacity;
            existing.SortOrder   = loc.SortOrder;
            if (loc.ImageData is not null)
            {
                existing.ImageData        = loc.ImageData;
                existing.ImageContentType = loc.ImageContentType;
            }
        }
        await db.SaveChangesAsync();
        cache.Remove(AllKey);
        return loc;
    }

    public async Task DeleteAsync(Guid locationId)
    {
        using var db = factory.CreateDbContext();
        var loc = await db.Locations.FindAsync(locationId);
        if (loc is null) return;
        db.Locations.Remove(loc);
        await db.SaveChangesAsync();
        cache.Remove(AllKey);
    }
}
