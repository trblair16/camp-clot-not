namespace CampClotNot.Data.Entities;

public enum ScheduleEventType { Activity, Meal, Travel, Free, Mandatory }

public class ScheduleEvent
{
    public Guid ScheduleEventId { get; set; }
    public Guid CampEventId { get; set; }       // FK to Event — scopes to CCN 2026
    public Event CampEvent { get; set; } = null!;
    public DateOnly CampDay { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public ScheduleEventType EventType { get; set; }
    public bool AppliesToAllGroups { get; set; } = true;
    public int? MaxCapacity { get; set; }       // stored silently; future registration feature
    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
    public List<ScheduleEventGroup> EventGroups { get; set; } = [];
}
