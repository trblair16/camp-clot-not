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

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
