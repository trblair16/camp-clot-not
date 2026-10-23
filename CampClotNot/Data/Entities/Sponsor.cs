namespace CampClotNot.Data.Entities;

/// <summary>
/// A sponsor, entered once and reused across events (#341). Which events show it, and in what
/// order, is <see cref="EventSponsor"/>. Editing these details changes every event it's on.
/// </summary>
public class Sponsor
{
    public Guid SponsorId { get; set; }
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }
    public byte[]? LogoData { get; set; }
    public string? LogoContentType { get; set; }
    public string? Website { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public ICollection<EventSponsor> Events { get; set; } = [];
}
