using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Repositories;

public class GroupRepository(IDbContextFactory<AppDbContext> factory) : IGroupRepository
{
    public async Task<List<Group>> GetAllAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.Groups.Include(g => g.BoardPos).ToListAsync();
    }

    public async Task<Group?> GetByIdAsync(Guid groupId)
    {
        using var db = factory.CreateDbContext();
        return await db.Groups.Include(g => g.BoardPos).FirstOrDefaultAsync(g => g.GroupId == groupId);
    }

    public async Task<Group> CreateAsync(Group group)
    {
        using var db = factory.CreateDbContext();
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return group;
    }

    public async Task UpdateAsync(Group group)
    {
        using var db = factory.CreateDbContext();
        db.Groups.Update(group);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid groupId)
    {
        using var db = factory.CreateDbContext();
        var group = await db.Groups.FindAsync(groupId);
        if (group is not null)
        {
            db.Groups.Remove(group);
            await db.SaveChangesAsync();
        }
    }
}
