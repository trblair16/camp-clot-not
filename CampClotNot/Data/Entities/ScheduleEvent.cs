namespace CampClotNot.Data.Entities;

public class ScheduleItem
{
    public Guid ScheduleItemId { get; set; }
    public Guid CampEventId { get; set; }
    public Event CampEvent { get; set; } = null!;
    public DateOnly CampDay { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public string? LocationOther { get; set; }
    public Guid? ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public Guid ScheduleItemTypeId { get; set; }
    public ScheduleItemType ScheduleItemType { get; set; } = null!;
    public string? PresenterName { get; set; }
    public string? PresenterBio { get; set; }
    public bool AppliesToAllGroups { get; set; } = true;
    public int? MaxCapacity { get; set; }
    public bool TrackAttendance { get; set; }
    public SelfCheckInMode SelfCheckInMode { get; set; }
    public string? CheckInCode { get; set; }          // random token in the check-in QR URL; created on first use

    // Breakouts: a slot is a placeholder item; its options are ordinary items pointing back at it.
    // One level only — a slot has no parent and an option is never itself a slot.
    public bool IsBreakoutSlot { get; set; }
    public bool AllowSelfSignup { get; set; } = true;  // only meaningful on a slot
    public Guid? ParentScheduleItemId { get; set; }
    public ScheduleItem? ParentScheduleItem { get; set; }
    public List<ScheduleItem> Options { get; set; } = new();
    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
    public List<ScheduleItemGroup> ItemGroups { get; set; } = new();
}
