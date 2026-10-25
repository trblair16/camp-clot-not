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

    // Set when this visit's guest matches a CanSignIn=false EventStaff record on this event
    // (#354) — lets Attendance show one row tagged with their team role instead of two.
    public Guid? LinkedEventStaffId { get; set; }
    public EventStaff? LinkedEventStaff { get; set; }
}
