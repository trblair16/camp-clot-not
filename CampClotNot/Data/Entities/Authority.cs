namespace CampClotNot.Data.Entities;

public class Authority
{
    public Guid AuthorityId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemName { get; set; } = "";

    public ICollection<UserRoleAuthorityLink> RoleLinks { get; set; } = new List<UserRoleAuthorityLink>();
    public ICollection<UserAuthorityLink> UserLinks { get; set; } = new List<UserAuthorityLink>();
}
