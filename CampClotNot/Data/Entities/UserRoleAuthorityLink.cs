namespace CampClotNot.Data.Entities;

public class UserRoleAuthorityLink
{
    public Guid UserRoleId { get; set; }
    public Guid AuthorityId { get; set; }

    public UserRole UserRole { get; set; } = null!;
    public Authority Authority { get; set; } = null!;
}
