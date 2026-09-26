namespace CampClotNot.Data.Entities;

/// <summary>
/// A sign-up for one breakout option. Same ownership shape as <see cref="ScheduleItemAttendance"/>:
/// exactly one of GuestAttendeeId / UserId. SlotScheduleItemId is the option's parent, copied here
/// so the database can enforce one option per person per slot.
/// </summary>
public class ScheduleItemRegistration
{
    public Guid ScheduleItemRegistrationId { get; set; }
    public Guid ScheduleItemId { get; set; }
    public ScheduleItem ScheduleItem { get; set; } = null!;
    public Guid SlotScheduleItemId { get; set; }
    public ScheduleItem SlotScheduleItem { get; set; } = null!;
    public Guid? GuestAttendeeId { get; set; }
    public GuestAttendee? GuestAttendee { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public DateTime RegisteredAt { get; set; }        // CampTime.Now
    public AttendanceMethod Method { get; set; }      // Self | Admin
    public Guid? RegisteredByUserId { get; set; }
    public User? RegisteredByUser { get; set; }
}
