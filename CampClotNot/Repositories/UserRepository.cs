using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Repositories;

public class UserRepository(IDbContextFactory<AppDbContext> factory) : IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email)
    {
        using var db = factory.CreateDbContext();
        return await db.Users
            .Include(u => u.UserRole)
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant() && u.IsActive);
    }

    public async Task<List<User>> GetAllAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.Users
            .Include(u => u.UserRole)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();
    }

    public async Task<User> CreateAsync(User user)
    {
        using var db = factory.CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        using var db = factory.CreateDbContext();
        db.Users.Update(user);
        await db.SaveChangesAsync();
    }

    public async Task<UserRole?> GetRoleAsync(Role role)
    {
        using var db = factory.CreateDbContext();
        return await db.UserRoles.FirstOrDefaultAsync(r => r.SystemName == role.ToString());
    }
}
