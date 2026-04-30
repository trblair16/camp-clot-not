namespace CampClotNot.Data.Entities;

public class EventCapability
{
    public Guid EventCapabilityId { get; set; }
    public Guid EventId { get; set; }
    public Guid CapabilityId { get; set; }

    public Event Event { get; set; } = null!;
    public Capability Capability { get; set; } = null!;
}
