using CampClotNot.Data;
using CampClotNot.Data.Entities;

namespace CampClotNot.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetAllAsync();
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task<UserRole?> GetRoleAsync(Role role);
}
