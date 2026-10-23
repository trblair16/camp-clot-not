namespace CampClotNot.Data.Entities;

/// <summary>
/// An ad image, entered once and reused across events (#350). Which events show it, in what
/// order, and how often it was tapped there is <see cref="EventAd"/>. A sponsor is optional
/// (HBDA's own promos have none); taps go to <see cref="LinkUrl"/>, else the sponsor's website.
/// </summary>
public class SponsorAd
{
    public Guid SponsorAdId { get; set; }
    public string Title { get; set; } = "";
    public Guid? SponsorId { get; set; }
    public Sponsor? Sponsor { get; set; }
    public string? LinkUrl { get; set; }
    public byte[] ImageData { get; set; } = [];
    public string ImageContentType { get; set; } = "image/webp";
    public DateTime UpdatedAt { get; set; }   // versions the image URL so a replaced image isn't cached
    public ICollection<EventAd> Events { get; set; } = [];
}
