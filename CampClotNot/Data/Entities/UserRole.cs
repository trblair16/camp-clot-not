namespace CampClotNot.Data.Entities;

public class UserRole
{
    public Guid UserRoleId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemName { get; set; } = "";

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<UserRoleAuthorityLink> AuthorityLinks { get; set; } = new List<UserRoleAuthorityLink>();
}
