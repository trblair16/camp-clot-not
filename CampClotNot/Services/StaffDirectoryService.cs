using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class StaffDirectoryService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<StaffMember>> GetVisibleAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.StaffMembers
            .Where(s => s.IsVisible)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task<List<StaffMember>> GetAllAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.StaffMembers.OrderBy(s => s.SortOrder).ToListAsync();
    }

    public async Task UpsertAsync(StaffMember member)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.StaffMembers.FindAsync(member.StaffMemberId);
        if (existing is null)
        {
            if (member.StaffMemberId == Guid.Empty)
                member.StaffMemberId = Guid.NewGuid();
            db.StaffMembers.Add(member);
        }
        else
        {
            existing.DisplayName  = member.DisplayName;
            existing.RoleTitle    = member.RoleTitle;
            existing.Phone        = member.Phone;
            existing.Email        = member.Email;
            existing.AvatarEmoji  = member.AvatarEmoji;
            existing.IsVisible    = member.IsVisible;
            existing.SortOrder    = member.SortOrder;
            existing.LinkedUserId = member.LinkedUserId;
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid staffMemberId)
    {
        using var db = factory.CreateDbContext();
        var member = await db.StaffMembers.FindAsync(staffMemberId);
        if (member is null) return;
        db.StaffMembers.Remove(member);
        await db.SaveChangesAsync();
    }

    public async Task ReorderAsync(List<Guid> orderedIds)
    {
        using var db = factory.CreateDbContext();
        var members = await db.StaffMembers
            .Where(s => orderedIds.Contains(s.StaffMemberId))
            .ToListAsync();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var m = members.FirstOrDefault(x => x.StaffMemberId == orderedIds[i]);
            if (m is not null) m.SortOrder = i;
        }
        await db.SaveChangesAsync();
    }
}
