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
    public bool MustChangePassword { get; set; } = false;

    // Contact details and photo, entered once and shown on every event's Hub staff directory.
    public string? Phone { get; set; }
    public byte[]? PhotoData { get; set; }
    public string? PhotoContentType { get; set; }
    public string? PhotoObjectPosition { get; set; }   // "X% Y%" for object-position
    public string AvatarEmoji { get; set; } = "👤";

    // False for people who are only on a team/directory (a nurse line, a guest speaker) and never
    // sign in. Login, forgot-password, and reset links all refuse them.
    public bool CanSignIn { get; set; } = true;

    public UserRole UserRole { get; set; } = null!;
    public ICollection<UserAuthorityLink> AuthorityLinks { get; set; } = new List<UserAuthorityLink>();
}
