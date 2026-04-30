namespace CampClotNot.Data.Entities;

public class UserAuthorityLink
{
    public Guid UserId { get; set; }
    public Guid AuthorityId { get; set; }

    public User User { get; set; } = null!;
    public Authority Authority { get; set; } = null!;
}
