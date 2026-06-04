namespace CampClotNot.Data.Entities;

public class ScheduleItemType
{
    public Guid ScheduleItemTypeId { get; set; }
    public string Name { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}
