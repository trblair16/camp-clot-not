namespace CampClotNot.Data.Entities;

public class ActivityTypeCategory
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemName { get; set; } = "";

    public ICollection<ActivityType> ActivityTypes { get; set; } = new List<ActivityType>();
}
