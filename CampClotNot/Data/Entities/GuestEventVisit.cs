namespace CampClotNot.Data.Entities;

public class GuestEventVisit
{
    public Guid GuestEventVisitId { get; set; }
    public Guid GuestAttendeeId { get; set; }
    public GuestAttendee GuestAttendee { get; set; } = null!;
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public DateTime FirstJoinedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
