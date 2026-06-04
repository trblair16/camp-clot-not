namespace CampClotNot.Data.Entities;

public class EventScheduleItemType
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid ScheduleItemTypeId { get; set; }
    public ScheduleItemType ScheduleItemType { get; set; } = null!;
}
