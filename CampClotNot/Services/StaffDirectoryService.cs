using CampClotNot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

/// <summary>One card on the Hub staff page. Photo bytes are served separately by /staff-photo/{UserId}.</summary>
public record DirectoryCard(
    Guid UserId, string Name, string? Title, string? Phone, string? Email,
    bool HasPhoto, string? PhotoObjectPosition, string AvatarEmoji);

/// <summary>
/// The Hub staff directory: the event's team members marked "show in Hub directory", in card
/// order. Contact details and photo come from the person (entered once, reused every event);
/// the title and order are per event.
/// </summary>
public class StaffDirectoryService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private static string Key(Guid eventId) => $"staff.dir.{eventId}";

    public async Task<List<DirectoryCard>> GetVisibleAsync(Guid eventId)
    {
        return await cache.GetOrCreateAsync(Key(eventId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var db = factory.CreateDbContext();
            return await db.EventStaff.AsNoTracking()
                .Where(s => s.EventId == eventId && s.ShowInDirectory && s.User.IsActive)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.User.LastName).ThenBy(s => s.User.FirstName)
                .Select(s => new DirectoryCard(
                    s.UserId,
                    (s.User.FirstName + " " + s.User.LastName).Trim(),
                    s.Title,
                    s.User.Phone,
                    s.User.Email == "" ? null : s.User.Email,
                    s.User.PhotoContentType != null,
                    s.User.PhotoObjectPosition,
                    s.User.AvatarEmoji))
                .ToListAsync();
        }) ?? [];
    }

    /// <summary>Saves the directory order for the event (people not listed keep their position).</summary>
    public async Task ReorderAsync(Guid eventId, IReadOnlyList<Guid> userIdsInOrder)
    {
        using var db = factory.CreateDbContext();
        var rows = await db.EventStaff.Where(s => s.EventId == eventId && userIdsInOrder.Contains(s.UserId)).ToListAsync();
        for (var i = 0; i < userIdsInOrder.Count; i++)
            if (rows.FirstOrDefault(r => r.UserId == userIdsInOrder[i]) is { } r) r.SortOrder = i;
        await db.SaveChangesAsync();
        Invalidate(eventId);
    }

    public void Invalidate(Guid eventId) => cache.Remove(Key(eventId));
}
