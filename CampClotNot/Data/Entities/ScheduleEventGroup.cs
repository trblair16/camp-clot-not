namespace CampClotNot.Data.Entities;

public class ScheduleItemGroup
{
    public Guid ScheduleItemId { get; set; }
    public ScheduleItem ScheduleItem { get; set; } = null!;
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid? ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public string? Note { get; set; }
}
