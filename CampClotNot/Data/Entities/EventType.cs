namespace CampClotNot.Data.Entities;

public class EventType
{
    public Guid EventTypeId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemName { get; set; } = "";

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
