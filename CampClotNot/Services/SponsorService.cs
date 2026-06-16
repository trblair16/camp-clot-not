using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class SponsorService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private static string ListKey(Guid eventId) => $"spon.{eventId}";

    public async Task<Sponsor?> GetByIdAsync(Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        return await db.Sponsors.FindAsync(sponsorId);
    }

    public async Task<List<Sponsor>> GetAllForEventAsync(Guid eventId)
    {
        return await cache.GetOrCreateAsync(ListKey(eventId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var db = factory.CreateDbContext();
            return await db.Sponsors
                .Where(s => s.EventId == eventId)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .Select(s => new Sponsor
                {
                    SponsorId       = s.SponsorId,
                    EventId         = s.EventId,
                    Name            = s.Name,
                    LogoUrl         = s.LogoUrl,
                    LogoContentType = s.LogoContentType,
                    Website         = s.Website,
                    ContactName     = s.ContactName,
                    Phone           = s.Phone,
                    SortOrder       = s.SortOrder
                    // LogoData excluded — served on demand via /sponsor-logo/{id}
                })
                .ToListAsync();
        }) ?? [];
    }

    public async Task<Sponsor> UpsertAsync(Sponsor s)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.Sponsors.FindAsync(s.SponsorId);
        if (existing is null)
        {
            if (s.SponsorId == Guid.Empty) s.SponsorId = Guid.NewGuid();
            db.Sponsors.Add(s);
        }
        else
        {
            existing.Name        = s.Name;
            existing.LogoUrl     = s.LogoUrl;
            existing.Website     = s.Website;
            existing.ContactName = s.ContactName;
            existing.Phone       = s.Phone;
            existing.SortOrder   = s.SortOrder;
            if (s.LogoData is not null)
            {
                existing.LogoData        = s.LogoData;
                existing.LogoContentType = s.LogoContentType;
            }
        }
        await db.SaveChangesAsync();
        cache.Remove(ListKey(s.EventId));
        return s;
    }

    public async Task UpdateSortOrderAsync(List<Sponsor> ordered)
    {
        using var db = factory.CreateDbContext();
        var ids = ordered.Select(s => s.SponsorId).ToList();
        var existing = await db.Sponsors.Where(s => ids.Contains(s.SponsorId)).ToListAsync();
        for (int i = 0; i < ordered.Count; i++)
        {
            var match = existing.FirstOrDefault(s => s.SponsorId == ordered[i].SponsorId);
            if (match is not null) match.SortOrder = i;
        }
        await db.SaveChangesAsync();
        if (ordered.Count > 0)
            cache.Remove(ListKey(ordered[0].EventId));
    }

    public async Task DeleteAsync(Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        var s = await db.Sponsors.FindAsync(sponsorId);
        if (s is null) return;
        var eventId = s.EventId;
        db.Sponsors.Remove(s);
        await db.SaveChangesAsync();
        cache.Remove(ListKey(eventId));
    }
}
