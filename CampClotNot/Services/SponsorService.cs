using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

/// <summary>A sponsor in the "Add from past events" picker: whether it's on this event and where else it's been.</summary>
public record SponsorOption(Sponsor Sponsor, bool OnThisEvent, List<string> EventNames);

/// <summary>
/// Sponsors are entered once and reused (#341): <see cref="Sponsor"/> holds the details,
/// <see cref="EventSponsor"/> which events show it and in what order.
/// </summary>
public class SponsorService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private static string ListKey(Guid eventId) => $"spon.{eventId}";

    public async Task<Sponsor?> GetByIdAsync(Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        return await db.Sponsors.FindAsync(sponsorId);
    }

    /// <summary>The event's sponsors in its order. Logo bytes are left out (served by /sponsors/logo/{id}).</summary>
    public async Task<List<Sponsor>> GetAllForEventAsync(Guid eventId)
    {
        return await cache.GetOrCreateAsync(ListKey(eventId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var db = factory.CreateDbContext();
            return await db.EventSponsors.AsNoTracking()
                .Where(es => es.EventId == eventId)
                .OrderBy(es => es.SortOrder).ThenBy(es => es.Sponsor.Name)
                .Select(es => new Sponsor
                {
                    SponsorId       = es.SponsorId,
                    Name            = es.Sponsor.Name,
                    LogoUrl         = es.Sponsor.LogoUrl,
                    LogoContentType = es.Sponsor.LogoContentType,
                    Website         = es.Sponsor.Website,
                    ContactName     = es.Sponsor.ContactName,
                    Phone           = es.Sponsor.Phone
                })
                .ToListAsync();
        }) ?? [];
    }

    /// <summary>Every sponsor, for the picker: on this event or not, and the events it's been on.</summary>
    public async Task<List<SponsorOption>> GetLibraryAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var rows = await db.Sponsors.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                Sponsor = new Sponsor
                {
                    SponsorId = s.SponsorId, Name = s.Name, LogoUrl = s.LogoUrl, LogoContentType = s.LogoContentType,
                    Website = s.Website, ContactName = s.ContactName, Phone = s.Phone
                },
                OnThisEvent = s.Events.Any(es => es.EventId == eventId),
                EventNames = s.Events.OrderByDescending(es => es.Event.EffDate).Select(es => es.Event.Name).ToList()
            })
            .ToListAsync();
        return rows.Select(r => new SponsorOption(r.Sponsor, r.OnThisEvent, r.EventNames)).ToList();
    }

    /// <summary>How many events show this sponsor (the edit form says changes apply to all of them).</summary>
    public async Task<int> CountEventsAsync(Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        return await db.EventSponsors.CountAsync(es => es.SponsorId == sponsorId);
    }

    /// <summary>An existing sponsor with this name (ignoring case and spaces), so "add new" can point to it instead.</summary>
    public async Task<Sponsor?> FindByNameAsync(string name, Guid exceptSponsorId)
    {
        var key = name.Trim().ToLower();
        using var db = factory.CreateDbContext();
        return await db.Sponsors.AsNoTracking()
            .Where(s => s.SponsorId != exceptSponsorId && s.Name.Trim().ToLower() == key)
            .Select(s => new Sponsor { SponsorId = s.SponsorId, Name = s.Name })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Creates or updates the sponsor's details. A new sponsor is put on <paramref name="addToEventId"/>
    /// (at the end). A null <c>LogoData</c> keeps the current uploaded logo.
    /// </summary>
    public async Task<Sponsor> UpsertAsync(Sponsor s, Guid? addToEventId = null)
    {
        using var db = factory.CreateDbContext();
        var existing = s.SponsorId == Guid.Empty ? null : await db.Sponsors.FindAsync(s.SponsorId);
        if (existing is null)
        {
            if (s.SponsorId == Guid.Empty) s.SponsorId = Guid.NewGuid();
            db.Sponsors.Add(s);
            if (addToEventId is { } eventId)
                db.EventSponsors.Add(new EventSponsor { EventId = eventId, SponsorId = s.SponsorId, SortOrder = await NextSortOrderAsync(db, eventId) });
        }
        else
        {
            existing.Name        = s.Name;
            existing.LogoUrl     = s.LogoUrl;
            existing.Website     = s.Website;
            existing.ContactName = s.ContactName;
            existing.Phone       = s.Phone;
            if (s.LogoData is not null)
            {
                existing.LogoData        = s.LogoData;
                existing.LogoContentType = s.LogoContentType;
            }
        }
        await db.SaveChangesAsync();
        await InvalidateEventsOfAsync(db, s.SponsorId);
        return s;
    }

    /// <summary>Puts an existing sponsor on the event, at the end. A no-op if it's already there.</summary>
    public async Task AddToEventAsync(Guid eventId, Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        if (await db.EventSponsors.AnyAsync(es => es.EventId == eventId && es.SponsorId == sponsorId)) return;
        db.EventSponsors.Add(new EventSponsor { EventId = eventId, SponsorId = sponsorId, SortOrder = await NextSortOrderAsync(db, eventId) });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* a concurrent add won the race — the link exists either way */ }
        cache.Remove(ListKey(eventId));
    }

    /// <summary>Takes the sponsor off this event only. Its details stay for other and future events.</summary>
    public async Task RemoveFromEventAsync(Guid eventId, Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        await db.EventSponsors.Where(es => es.EventId == eventId && es.SponsorId == sponsorId).ExecuteDeleteAsync();
        cache.Remove(ListKey(eventId));
    }

    /// <summary>Saves this event's order. Sponsors not listed keep their position.</summary>
    public async Task ReorderAsync(Guid eventId, IReadOnlyList<Guid> sponsorIdsInOrder)
    {
        using var db = factory.CreateDbContext();
        var links = await db.EventSponsors.Where(es => es.EventId == eventId && sponsorIdsInOrder.Contains(es.SponsorId)).ToListAsync();
        for (var i = 0; i < sponsorIdsInOrder.Count; i++)
            if (links.FirstOrDefault(l => l.SponsorId == sponsorIdsInOrder[i]) is { } l) l.SortOrder = i;
        await db.SaveChangesAsync();
        cache.Remove(ListKey(eventId));
    }

    /// <summary>Deletes a sponsor that isn't on any event. Returns false (and deletes nothing) if it still is.</summary>
    public async Task<bool> DeleteUnusedAsync(Guid sponsorId)
    {
        using var db = factory.CreateDbContext();
        if (await db.EventSponsors.AnyAsync(es => es.SponsorId == sponsorId)) return false;
        await db.Sponsors.Where(s => s.SponsorId == sponsorId).ExecuteDeleteAsync();
        return true;
    }

    public void Invalidate(Guid eventId) => cache.Remove(ListKey(eventId));

    private async Task InvalidateEventsOfAsync(AppDbContext db, Guid sponsorId)
    {
        foreach (var eventId in await db.EventSponsors.Where(es => es.SponsorId == sponsorId).Select(es => es.EventId).ToListAsync())
            cache.Remove(ListKey(eventId));
    }

    private static async Task<int> NextSortOrderAsync(AppDbContext db, Guid eventId) =>
        (await db.EventSponsors.Where(es => es.EventId == eventId).MaxAsync(es => (int?)es.SortOrder) ?? -1) + 1;
}
