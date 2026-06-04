using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class StaffDirectoryService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<StaffMember>> GetVisibleAsync(Guid campEventId)
    {
        using var db = factory.CreateDbContext();
        return await db.StaffMembers
            .Where(s => s.CampEventId == campEventId && s.IsVisible)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task<List<StaffMember>> GetAllAsync(Guid campEventId)
    {
        using var db = factory.CreateDbContext();
        return await db.StaffMembers
            .Where(s => s.CampEventId == campEventId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    // Users eligible to be imported: active Admin/Staff/Volunteer not already in this event's directory
    public async Task<List<User>> GetEligibleUsersAsync(Guid campEventId)
    {
        using var db = factory.CreateDbContext();
        var linkedIds = await db.StaffMembers
            .Where(s => s.CampEventId == campEventId && s.LinkedUserId != null)
            .Select(s => s.LinkedUserId!.Value)
            .ToListAsync();

        return await db.Users
            .Include(u => u.UserRole)
            .Where(u => u.IsActive
                && (u.UserRole.SystemName == nameof(Role.Admin)
                    || u.UserRole.SystemName == nameof(Role.Staff)
                    || u.UserRole.SystemName == nameof(Role.Volunteer))
                && !linkedIds.Contains(u.UserId))
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();
    }

    public async Task ImportUserAsync(Guid campEventId, Guid userId)
    {
        using var db = factory.CreateDbContext();
        var user = await db.Users.Include(u => u.UserRole)
            .FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return;
        if (await db.StaffMembers.AnyAsync(s => s.CampEventId == campEventId && s.LinkedUserId == userId))
            return;
        db.StaffMembers.Add(new StaffMember
        {
            StaffMemberId = Guid.NewGuid(),
            CampEventId   = campEventId,
            DisplayName   = $"{user.FirstName} {user.LastName}".Trim(),
            RoleTitle     = user.UserRole.SystemName,
            Email         = user.Email,
            AvatarEmoji   = "👤",
            IsVisible     = true,
            SortOrder     = 0,
            LinkedUserId  = userId
        });
        await db.SaveChangesAsync();
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
            if (member.PhotoData is not null)
            {
                existing.PhotoData        = member.PhotoData;
                existing.PhotoContentType = member.PhotoContentType;
            }
            existing.PhotoObjectPosition = member.PhotoObjectPosition;
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
