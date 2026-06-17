using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class ScheduleItemTypeService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private const string AllKey = "sit.all";
    private static string EventKey(Guid eventId) => $"sit.ev.{eventId}";

    public async Task<List<ScheduleItemType>> GetAllAsync()
    {
        return await cache.GetOrCreateAsync(AllKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var db = factory.CreateDbContext();
            return await db.ScheduleItemTypes
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
                .ToListAsync();
        }) ?? [];
    }

    public async Task<List<ScheduleItemType>> GetForEventAsync(Guid eventId)
    {
        return await cache.GetOrCreateAsync(EventKey(eventId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var db = factory.CreateDbContext();
            return await db.EventScheduleItemTypes
                .Where(e => e.EventId == eventId)
                .Include(e => e.ScheduleItemType)
                .OrderBy(e => e.ScheduleItemType.SortOrder)
                .Select(e => e.ScheduleItemType)
                .ToListAsync();
        }) ?? [];
    }

    public async Task<List<Guid>> GetEnabledIdsForEventAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.EventScheduleItemTypes
            .Where(e => e.EventId == eventId)
            .Select(e => e.ScheduleItemTypeId)
            .ToListAsync();
    }

    public async Task UpsertAsync(ScheduleItemType type)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.ScheduleItemTypes.FindAsync(type.ScheduleItemTypeId);
        if (existing is null)
        {
            if (type.ScheduleItemTypeId == Guid.Empty) type.ScheduleItemTypeId = Guid.NewGuid();
            db.ScheduleItemTypes.Add(type);
        }
        else
        {
            existing.Name        = type.Name;
            existing.Description = type.Description;
            existing.SortOrder   = type.SortOrder;
            existing.BadgeColor  = type.BadgeColor;
        }
        await db.SaveChangesAsync();
        cache.Remove(AllKey);
    }

    public async Task<bool> DeleteAsync(Guid typeId)
    {
        using var db = factory.CreateDbContext();
        var inUse = await db.ScheduleItems.AnyAsync(s => s.ScheduleItemTypeId == typeId);
        if (inUse) return false;
        var type = await db.ScheduleItemTypes.FindAsync(typeId);
        if (type is null) return true;
        db.ScheduleItemTypes.Remove(type);
        await db.SaveChangesAsync();
        cache.Remove(AllKey);
        return true;
    }

    public async Task SetEventTypeEnabledAsync(Guid eventId, Guid typeId, bool enabled)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.EventScheduleItemTypes
            .FindAsync(eventId, typeId);
        if (enabled && existing is null)
        {
            db.EventScheduleItemTypes.Add(new EventScheduleItemType
            {
                EventId            = eventId,
                ScheduleItemTypeId = typeId
            });
        }
        else if (!enabled && existing is not null)
        {
            db.EventScheduleItemTypes.Remove(existing);
        }
        await db.SaveChangesAsync();
        cache.Remove(EventKey(eventId));
    }
}
