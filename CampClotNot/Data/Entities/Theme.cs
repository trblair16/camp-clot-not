namespace CampClotNot.Data.Entities;

public class Theme
{
    public Guid ThemeId { get; set; }
    public string Name { get; set; } = "";
    public int Year { get; set; }
    public string Description { get; set; } = "";
    public string? LogoAssetPath { get; set; }
    public string? ColorPalette { get; set; }
    public string? FontConfig { get; set; }

    // Uploaded images (via /admin/theme) win over the static LogoAssetPath /
    // ThemeConfig.BannerAssetPath fallbacks. Served by /theme-logo/{id} and /theme-banner/{id}.
    public byte[]? LogoData { get; set; }
    public string? LogoContentType { get; set; }
    public byte[]? BannerData { get; set; }
    public string? BannerContentType { get; set; }
    // UTC. Cache-buster (?v=ticks) on the image URLs and "last edited" on /admin/theme.
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
