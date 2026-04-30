using CampClotNot.Data;
using CampClotNot.Data.Entities;
using CampClotNot.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class GroupService(IGroupRepository groups, IDbContextFactory<AppDbContext> factory)
{
    public Task<List<Group>> GetAllAsync() => groups.GetAllAsync();

    public Task<Group?> GetByIdAsync(Guid groupId) => groups.GetByIdAsync(groupId);

    public Task<Group> CreateAsync(string name, string shortName, string color, Guid eventId) =>
        groups.CreateAsync(new Group
        {
            GroupId   = Guid.NewGuid(),
            Name      = name,
            ShortName = shortName,
            Color     = color,
            EventId   = eventId
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
}
