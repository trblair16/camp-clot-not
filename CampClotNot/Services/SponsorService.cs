using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class SponsorService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<Sponsor?> GetByIdAsync(Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        return await db.Sponsors.FindAsync(sponsorId);
    }

    public async Task<List<Sponsor>> GetAllForEventAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.Sponsors
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();
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
            existing.Name            = s.Name;
            existing.LogoUrl         = s.LogoUrl;
            existing.Website         = s.Website;
            existing.SortOrder       = s.SortOrder;
            if (s.LogoData is not null)
            {
                existing.LogoData        = s.LogoData;
                existing.LogoContentType = s.LogoContentType;
            }
        }
        await db.SaveChangesAsync();
        return s;
    }

    public async Task DeleteAsync(Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        var s = await db.Sponsors.FindAsync(sponsorId);
        if (s is null) return;
        db.Sponsors.Remove(s);
        await db.SaveChangesAsync();
    }
}
