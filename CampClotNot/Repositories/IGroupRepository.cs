using CampClotNot.Data.Entities;

namespace CampClotNot.Repositories;

public interface IGroupRepository
{
    Task<List<Group>> GetAllAsync();
    Task<List<Group>> GetForEventAsync(Guid eventId);
    Task<Group?> GetByIdAsync(Guid groupId);
    Task<Group> CreateAsync(Group group);
    Task UpdateAsync(Group group);
    Task DeleteAsync(Guid groupId);
}
