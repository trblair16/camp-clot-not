using CampClotNot.Data;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public record ThemeEditState(Guid ThemeId, Guid EventId, string EventName, ThemeConfig Config,
    string? LogoUrl, string? BannerUrl, DateTime? UpdatedAt);

public enum ImageUploadResult { Ok, TooLarge, BadType }

/// Persistence for /admin/theme. Every event owns its Theme row, so edits here only
/// affect that one event. Viewers pick changes up on their next full page load
/// (ThemeService resolves the theme once per circuit).
public class ThemeAdminService(IDbContextFactory<AppDbContext> factory)
{
    // The logo renders on every page load, so keep uploads small. No SVG: it's served
    // same-origin and can carry script.
    public const long MaxImageBytes = 2 * 1024 * 1024;
    public static readonly string[] AllowedImageTypes = ["image/png", "image/jpeg", "image/webp"];

    public async Task<ThemeEditState?> GetForEventAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var row = await db.Events.AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Select(e => new
            {
                e.EventId, EventName = e.Name, e.ThemeId,
                e.Theme.ColorPalette, e.Theme.LogoAssetPath, e.Theme.UpdatedAt,
                HasLogo = e.Theme.LogoData != null, HasBanner = e.Theme.BannerData != null
            })
            .FirstOrDefaultAsync();
        if (row is null) return null;

        var config = ThemeConfig.FromJson(row.ColorPalette) ?? ThemeService.Default;
        var v = row.UpdatedAt?.Ticks ?? 0;
        // Only what this theme actually has — no CCN fallback, so the editor shows "no logo".
        var logo = row.HasLogo ? $"/theme-logo/{row.ThemeId}?v={v}" : row.LogoAssetPath;
        var banner = row.HasBanner ? $"/theme-banner/{row.ThemeId}?v={v}" : config.BannerAssetPath;
        return new ThemeEditState(row.ThemeId, row.EventId, row.EventName, config, logo, banner, row.UpdatedAt);
    }

    public async Task SaveConfigAsync(Guid themeId, ThemeConfig config)
    {
        using var db = factory.CreateDbContext();
        var theme = await db.Themes.FindAsync(themeId);
        if (theme is null) return;
        theme.ColorPalette = config.ToJson();
        theme.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static ImageUploadResult Validate(long size, string contentType) =>
        size > MaxImageBytes ? ImageUploadResult.TooLarge
        : !AllowedImageTypes.Contains(contentType) ? ImageUploadResult.BadType
        : ImageUploadResult.Ok;

    public async Task<ImageUploadResult> SetLogoAsync(Guid themeId, byte[] data, string contentType)
    {
        var check = Validate(data.LongLength, contentType);
        if (check != ImageUploadResult.Ok) return check;
        using var db = factory.CreateDbContext();
        var theme = await db.Themes.FindAsync(themeId);
        if (theme is null) return ImageUploadResult.Ok;
        theme.LogoData = data;
        theme.LogoContentType = contentType;
        theme.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ImageUploadResult.Ok;
    }

    // Clears the static fallback too — the seed is insert-only now, so a removed logo stays removed.
    public async Task ClearLogoAsync(Guid themeId)
    {
        using var db = factory.CreateDbContext();
        var theme = await db.Themes.FindAsync(themeId);
        if (theme is null) return;
        theme.LogoData = null;
        theme.LogoContentType = null;
        theme.LogoAssetPath = null;
        theme.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<ImageUploadResult> SetBannerAsync(Guid themeId, byte[] data, string contentType)
    {
        var check = Validate(data.LongLength, contentType);
        if (check != ImageUploadResult.Ok) return check;
        using var db = factory.CreateDbContext();
        var theme = await db.Themes.FindAsync(themeId);
        if (theme is null) return ImageUploadResult.Ok;
        theme.BannerData = data;
        theme.BannerContentType = contentType;
        theme.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ImageUploadResult.Ok;
    }

    public async Task ClearBannerAsync(Guid themeId)
    {
        using var db = factory.CreateDbContext();
        var theme = await db.Themes.FindAsync(themeId);
        if (theme is null) return;
        var config = ThemeConfig.FromJson(theme.ColorPalette) ?? ThemeService.Default;
        theme.ColorPalette = (config with { BannerAssetPath = null }).ToJson();
        theme.BannerData = null;
        theme.BannerContentType = null;
        theme.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
