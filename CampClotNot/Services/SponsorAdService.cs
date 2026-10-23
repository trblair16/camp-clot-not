using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

/// <summary>An ad as an event shows it. Image bytes are served by /sponsors/ad/{id}.</summary>
public record AdCard(Guid SponsorAdId, string Title, Guid? SponsorId, string? SponsorName, string? LinkUrl,
                     string? Target, long ImageVersion, int SortOrder, int ClickCount)
{
    public string ImageSrc => $"/sponsors/ad/{SponsorAdId}?v={ImageVersion}";
}

/// <summary>An ad in the "Add from past events" picker.</summary>
public record AdOption(AdCard Ad, bool OnThisEvent, List<string> EventNames);

/// <summary>
/// Sponsor ads (#350): <see cref="SponsorAd"/> holds the image and link, <see cref="EventAd"/>
/// which events show it, in what order, and how many taps it got there.
/// </summary>
public class SponsorAdService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    public const int MaxSide = 1600;
    private static string ListKey(Guid eventId) => $"ads.{eventId}";

    /// <summary>Where a tap goes: the ad's own link, else its sponsor's website, else nowhere.</summary>
    private static string? TargetOf(string? linkUrl, string? sponsorWebsite) =>
        !string.IsNullOrWhiteSpace(linkUrl) ? linkUrl : string.IsNullOrWhiteSpace(sponsorWebsite) ? null : sponsorWebsite;

    /// <summary>The event's ads in its order (cached; click counts are as of the cache fill).</summary>
    public async Task<List<AdCard>> GetForEventAsync(Guid eventId)
    {
        return await cache.GetOrCreateAsync(ListKey(eventId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await LoadForEventAsync(eventId);
        }) ?? [];
    }

    /// <summary>Uncached, with live click counts, for the admin page.</summary>
    public async Task<List<AdCard>> LoadForEventAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var rows = await db.EventAds.AsNoTracking()
            .Where(ea => ea.EventId == eventId)
            .OrderBy(ea => ea.SortOrder).ThenBy(ea => ea.SponsorAd.Title)
            .Select(ea => new
            {
                ea.SponsorAdId, ea.SponsorAd.Title, ea.SponsorAd.SponsorId, SponsorName = ea.SponsorAd.Sponsor!.Name,
                ea.SponsorAd.LinkUrl, Website = ea.SponsorAd.Sponsor!.Website, ea.SponsorAd.UpdatedAt, ea.SortOrder, ea.ClickCount
            })
            .ToListAsync();
        return rows.Select(r => new AdCard(r.SponsorAdId, r.Title, r.SponsorId, r.SponsorName, r.LinkUrl,
            TargetOf(r.LinkUrl, r.Website), r.UpdatedAt.Ticks, r.SortOrder, r.ClickCount)).ToList();
    }

    /// <summary>Every ad, for the picker: on this event or not, and the events it's been on.</summary>
    public async Task<List<AdOption>> GetLibraryAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var rows = await db.SponsorAds.AsNoTracking()
            .OrderBy(a => a.Sponsor!.Name).ThenBy(a => a.Title)
            .Select(a => new
            {
                a.SponsorAdId, a.Title, a.SponsorId, SponsorName = a.Sponsor!.Name, a.LinkUrl, Website = a.Sponsor!.Website, a.UpdatedAt,
                OnThisEvent = a.Events.Any(ea => ea.EventId == eventId),
                EventNames = a.Events.OrderByDescending(ea => ea.Event.EffDate).Select(ea => ea.Event.Name).ToList()
            })
            .ToListAsync();
        return rows.Select(r => new AdOption(
            new AdCard(r.SponsorAdId, r.Title, r.SponsorId, r.SponsorName, r.LinkUrl, TargetOf(r.LinkUrl, r.Website), r.UpdatedAt.Ticks, 0, 0),
            r.OnThisEvent, r.EventNames)).ToList();
    }

    public async Task<(byte[] Data, string ContentType)?> GetImageAsync(Guid sponsorAdId)
    {
        using var db = factory.CreateDbContext();
        var img = await db.SponsorAds.AsNoTracking().Where(a => a.SponsorAdId == sponsorAdId)
            .Select(a => new { a.ImageData, a.ImageContentType }).FirstOrDefaultAsync();
        return img is null ? null : (img.ImageData, img.ImageContentType);
    }

    public async Task<int> CountEventsAsync(Guid sponsorAdId)
    {
        using var db = factory.CreateDbContext();
        return await db.EventAds.CountAsync(ea => ea.SponsorAdId == sponsorAdId);
    }

    /// <summary>
    /// Creates or updates an ad. <paramref name="image"/> null keeps the current image (required
    /// for a new ad). A new ad is put at the end of <paramref name="addToEventId"/>'s list.
    /// </summary>
    public async Task<Guid> UpsertAsync(Guid sponsorAdId, string title, Guid? sponsorId, string? linkUrl,
                                        ShrunkImage? image, Guid? addToEventId = null)
    {
        using var db = factory.CreateDbContext();
        var ad = sponsorAdId == Guid.Empty ? null : await db.SponsorAds.FindAsync(sponsorAdId);
        if (ad is null)
        {
            if (image is null) throw new InvalidOperationException("A new ad needs an image.");
            ad = new SponsorAd { SponsorAdId = Guid.NewGuid() };
            db.SponsorAds.Add(ad);
            if (addToEventId is { } eventId)
                db.EventAds.Add(new EventAd { EventId = eventId, SponsorAdId = ad.SponsorAdId, SortOrder = await NextSortOrderAsync(db, eventId) });
        }
        ad.Title = title.Trim();
        ad.SponsorId = sponsorId;
        ad.LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim();
        if (image is not null)
        {
            ad.ImageData = image.Data;
            ad.ImageContentType = image.ContentType;
        }
        ad.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await InvalidateEventsOfAsync(db, ad.SponsorAdId);
        return ad.SponsorAdId;
    }

    public async Task AddToEventAsync(Guid eventId, Guid sponsorAdId)
    {
        using var db = factory.CreateDbContext();
        if (await db.EventAds.AnyAsync(ea => ea.EventId == eventId && ea.SponsorAdId == sponsorAdId)) return;
        db.EventAds.Add(new EventAd { EventId = eventId, SponsorAdId = sponsorAdId, SortOrder = await NextSortOrderAsync(db, eventId) });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* a concurrent add won the race — the link exists either way */ }
        cache.Remove(ListKey(eventId));
    }

    /// <summary>Takes the ad off this event only (and its tap count there).</summary>
    public async Task RemoveFromEventAsync(Guid eventId, Guid sponsorAdId)
    {
        using var db = factory.CreateDbContext();
        await db.EventAds.Where(ea => ea.EventId == eventId && ea.SponsorAdId == sponsorAdId).ExecuteDeleteAsync();
        cache.Remove(ListKey(eventId));
    }

    public async Task ReorderAsync(Guid eventId, IReadOnlyList<Guid> adIdsInOrder)
    {
        using var db = factory.CreateDbContext();
        var links = await db.EventAds.Where(ea => ea.EventId == eventId && adIdsInOrder.Contains(ea.SponsorAdId)).ToListAsync();
        for (var i = 0; i < adIdsInOrder.Count; i++)
            if (links.FirstOrDefault(l => l.SponsorAdId == adIdsInOrder[i]) is { } l) l.SortOrder = i;
        await db.SaveChangesAsync();
        cache.Remove(ListKey(eventId));
    }

    /// <summary>Deletes an ad that isn't on any event. Returns false (and deletes nothing) if it still is.</summary>
    public async Task<bool> DeleteUnusedAsync(Guid sponsorAdId)
    {
        using var db = factory.CreateDbContext();
        if (await db.EventAds.AnyAsync(ea => ea.SponsorAdId == sponsorAdId)) return false;
        await db.SponsorAds.Where(a => a.SponsorAdId == sponsorAdId).ExecuteDeleteAsync();
        return true;
    }

    /// <summary>
    /// Counts a tap at this event and returns where it should go, or null when the ad isn't on
    /// that event or has nowhere to go. The target always comes from the database.
    /// </summary>
    public async Task<string?> RecordClickAsync(Guid eventId, Guid sponsorAdId)
    {
        using var db = factory.CreateDbContext();
        var target = await db.EventAds.AsNoTracking()
            .Where(ea => ea.EventId == eventId && ea.SponsorAdId == sponsorAdId)
            .Select(ea => new { ea.SponsorAd.LinkUrl, Website = ea.SponsorAd.Sponsor!.Website })
            .FirstOrDefaultAsync();
        var url = target is null ? null : TargetOf(target.LinkUrl, target.Website);
        if (url is null) return null;
        await db.EventAds.Where(ea => ea.EventId == eventId && ea.SponsorAdId == sponsorAdId)
            .ExecuteUpdateAsync(u => u.SetProperty(ea => ea.ClickCount, ea => ea.ClickCount + 1));
        return url;
    }

    public void Invalidate(Guid eventId) => cache.Remove(ListKey(eventId));

    /// <summary>Clears the cached lists of every event showing ads by this sponsor (its website may have changed).</summary>
    public async Task InvalidateForSponsorAsync(Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        foreach (var eventId in await db.EventAds.Where(ea => ea.SponsorAd.SponsorId == sponsorId).Select(ea => ea.EventId).Distinct().ToListAsync())
            cache.Remove(ListKey(eventId));
    }

    private async Task InvalidateEventsOfAsync(AppDbContext db, Guid sponsorAdId)
    {
        foreach (var eventId in await db.EventAds.Where(ea => ea.SponsorAdId == sponsorAdId).Select(ea => ea.EventId).ToListAsync())
            cache.Remove(ListKey(eventId));
    }

    private static async Task<int> NextSortOrderAsync(AppDbContext db, Guid eventId) =>
        (await db.EventAds.Where(ea => ea.EventId == eventId).MaxAsync(ea => (int?)ea.SortOrder) ?? -1) + 1;
}
