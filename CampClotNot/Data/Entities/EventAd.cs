namespace CampClotNot.Data.Entities;

/// <summary>An ad shown at one event, in that event's order, with that event's tap count (#350).</summary>
public class EventAd
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid SponsorAdId { get; set; }
    public SponsorAd SponsorAd { get; set; } = null!;
    public int SortOrder { get; set; }
    public int ClickCount { get; set; }
}
