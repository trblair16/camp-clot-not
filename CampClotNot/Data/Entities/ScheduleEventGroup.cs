namespace CampClotNot.Data.Entities;

public class ScheduleEventGroup
{
    public Guid ScheduleEventId { get; set; }
    public ScheduleEvent ScheduleEvent { get; set; } = null!;
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
}
