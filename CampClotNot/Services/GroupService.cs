using CampClotNot.Data;
using CampClotNot.Data.Entities;
using CampClotNot.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class GroupService(IGroupRepository groups, IDbContextFactory<AppDbContext> factory)
{
    public Task<List<Group>> GetAllAsync() => groups.GetAllAsync();

    public Task<Group?> GetByIdAsync(Guid groupId) => groups.GetByIdAsync(groupId);

    public Task<Group> CreateAsync(string name, string shortName, string color, Guid eventId,
        string? tokenAssetPath = null) =>
        groups.CreateAsync(new Group
        {
            GroupId        = Guid.NewGuid(),
            Name           = name,
            ShortName      = shortName,
            Color          = color,
            EventId        = eventId,
            TokenAssetPath = tokenAssetPath
        });

    public async Task UpdateAsync(Guid groupId, string name, string shortName, string color,
        string? tokenAssetPath, string? cabinDisplayName)
    {
        var group = await groups.GetByIdAsync(groupId)
            ?? throw new InvalidOperationException($"Group {groupId} not found.");
        group.Name             = name;
        group.ShortName        = shortName;
        group.Color            = color;
        group.TokenAssetPath   = tokenAssetPath;
        group.CabinDisplayName = cabinDisplayName;
        await groups.UpdateAsync(group);
    }

    public Task DeleteAsync(Guid groupId) => groups.DeleteAsync(groupId);

    public async Task<List<CurrencyType>> GetCurrencyTypesAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.CurrencyTypes.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<int> GetTotalAsync(Guid groupId, Currency currency)
    {
        using var db = factory.CreateDbContext();
        return await db.Transactions
            .Where(t => t.GroupId == groupId
                && t.CurrencyType.SystemName == currency.ToString()
                && t.VoidedAt == null)
            .SumAsync(t => t.Amount);
    }

    // Single DB round-trip for all group scores — use instead of looping GetTotalAsync
    public async Task<Dictionary<Guid, (int Coins, int Stars)>> GetAllScoresAsync(IEnumerable<Guid> groupIds)
    {
        using var db = factory.CreateDbContext();
        var ids      = groupIds.ToList();
        var coinName = nameof(Currency.Primary);
        var starName = nameof(Currency.Prestige);

        var totals = await db.Transactions
            .Where(t => ids.Contains(t.GroupId) && t.VoidedAt == null)
            .GroupBy(t => new { t.GroupId, SystemName = t.CurrencyType.SystemName })
            .Select(g => new { g.Key.GroupId, g.Key.SystemName, Total = g.Sum(t => t.Amount) })
            .ToListAsync();

        return ids.ToDictionary(
            gid => gid,
            gid => (
                totals.FirstOrDefault(t => t.GroupId == gid && t.SystemName == coinName)?.Total ?? 0,
                totals.FirstOrDefault(t => t.GroupId == gid && t.SystemName == starName)?.Total ?? 0
            )
        );
    }
}
