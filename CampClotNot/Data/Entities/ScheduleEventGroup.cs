namespace CampClotNot.Data.Entities;

public class ScheduleEventGroup
{
    public Guid ScheduleEventId { get; set; }
    public ScheduleEvent ScheduleEvent { get; set; } = null!;
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid? ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public string? Note { get; set; }
}
