namespace CampClotNot.Data.Entities;

public class Capability
{
    public Guid CapabilityId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemName { get; set; } = "";

    public ICollection<EventCapability> EventCapabilities { get; set; } = new List<EventCapability>();
}
