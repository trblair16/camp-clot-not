namespace CampClotNot.Data.Entities;

public class ScheduleItemAttendance
{
    public Guid ScheduleItemAttendanceId { get; set; }
    public Guid ScheduleItemId { get; set; }
    public ScheduleItem ScheduleItem { get; set; } = null!;
    public Guid? GuestAttendeeId { get; set; }
    public GuestAttendee? GuestAttendee { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CheckedInAt { get; set; }
    public AttendanceMethod Method { get; set; }
    public Guid? CheckedInByUserId { get; set; }
    public User? CheckedInByUser { get; set; }
}
