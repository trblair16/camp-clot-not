namespace CampClotNot.Data.Entities;

public class ActivityType
{
    public Guid ActivityTypeId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemName { get; set; } = "";

    public ActivityTypeCategory Category { get; set; } = null!;
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
