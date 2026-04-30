namespace CampClotNot.Data.Entities;

public class User
{
    public Guid UserId { get; set; }
    public Guid UserRoleId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public UserRole UserRole { get; set; } = null!;
    public ICollection<UserAuthorityLink> AuthorityLinks { get; set; } = new List<UserAuthorityLink>();
}
