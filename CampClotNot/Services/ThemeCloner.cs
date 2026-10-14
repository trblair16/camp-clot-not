using CampClotNot.Data.Entities;

namespace CampClotNot.Services;

/// Builds the per-event Theme rows. Every event owns its own row so editing one event's
/// theme on /admin/theme never restyles another event.
public static class ThemeCloner
{
    /// A byte-for-byte copy of another event's theme (palette, static logo path, uploaded
    /// logo and banner) under a new ID. Title/subtitle are kept; the admin renames them.
    public static Theme Clone(Theme src, string eventName, int year) => new()
    {
        ThemeId           = Guid.NewGuid(),
        Name              = eventName,
        Year              = year,
        Description       = $"Theme for {eventName}",
        ColorPalette      = src.ColorPalette,
        FontConfig        = src.FontConfig,
        LogoAssetPath     = src.LogoAssetPath,
        LogoData          = src.LogoData?.ToArray(),
        LogoContentType   = src.LogoContentType,
        BannerData        = src.BannerData?.ToArray(),
        BannerContentType = src.BannerContentType,
        UpdatedAt         = DateTime.UtcNow
    };

    /// A fresh theme from a preset's look, titled after the event. The preset's own
    /// logo/banner paths are deliberately dropped so a new event never shows another
    /// event's artwork (the Mario preset still gets CCN art via UseCcnArtwork).
    public static Theme FromPreset(ThemePreset preset, string eventName, int year) => new()
    {
        ThemeId      = Guid.NewGuid(),
        Name         = eventName,
        Year         = year,
        Description  = $"Theme for {eventName}",
        ColorPalette = (preset.Config with
        {
            AppTitle        = eventName.ToUpperInvariant(),
            AppSubtitle     = eventName,
            BannerAssetPath = null
        }).ToJson(),
        UpdatedAt    = DateTime.UtcNow
    };
}
