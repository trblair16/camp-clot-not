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
    public Guid ScheduleItemTypeId { get; set; }
    public ScheduleItemType ScheduleItemType { get; set; } = null!;
    public string? PresenterName { get; set; }
    public string? PresenterBio { get; set; }
    public bool AppliesToAllGroups { get; set; } = true;
    public int? MaxCapacity { get; set; }
    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
    public List<ScheduleItemGroup> ItemGroups { get; set; } = new();
}
