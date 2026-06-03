namespace CampClotNot.Data.Entities;

public class Sponsor
{
    public Guid SponsorId { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }
    public byte[]? LogoData { get; set; }
    public string? LogoContentType { get; set; }
    public string? Website { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public int SortOrder { get; set; }
}
