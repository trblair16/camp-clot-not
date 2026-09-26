namespace CampClotNot.Data.Entities;

/// <summary>
/// A user who is staff at one event. The role here labels them on rosters and drives the
/// "by role" bulk sign-up filter; login and permissions still use <see cref="User.UserRoleId"/>.
/// </summary>
public class EventStaff
{
    public Guid EventStaffId { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid UserRoleId { get; set; }
    public UserRole UserRole { get; set; } = null!;
    public Guid? GroupId { get; set; }      // must belong to EventId (service-enforced)
    public Group? Group { get; set; }
    public DateTime AddedAt { get; set; }
}
