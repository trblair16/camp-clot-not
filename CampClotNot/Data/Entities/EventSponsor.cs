namespace CampClotNot.Data.Entities;

/// <summary>A sponsor shown at one event, in that event's order (#341).</summary>
public class EventSponsor
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid SponsorId { get; set; }
    public Sponsor Sponsor { get; set; } = null!;
    public int SortOrder { get; set; }
}
