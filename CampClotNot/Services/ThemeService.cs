using System.Text.Json;
using CampClotNot.Data;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public static class CampTime
{
    private static readonly TimeZoneInfo Central =
        TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

    public static DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Central), DateTimeKind.Utc);
    public static DateOnly Today => DateOnly.FromDateTime(Now);
}

/// <summary>
/// All values that vary between event themes (Mario Party 2026, future events).
/// Consumed as CSS custom properties via ThemeHead.razor and as CascadingParameter
/// so any page/component can read theme values without repeating hardcoded strings.
///
/// Future: load ColorPalette JSON from Theme DB row. For now, mapped by ThemeId.
/// </summary>
public record ThemeConfig(
    string AppTitle,
    string AppSubtitle,
    // Background gradient stops
    string BgStart,
    string BgMid,
    string BgEnd,
    // Core palette
    string Primary,    // gold — coins, highlights
    string Accent,     // red — stars, danger
    string Success,    // green — confirmations
    string Info,       // blue — info
    // Board-specific
    string TrackFill,  // board track interior color
    string TrackBg,    // board loop background tint
    // Currency display
    string Currency1Icon,
    string Currency1Name,
    string Currency2Icon,
    string Currency2Name,
    // Full-size hero image for the Dashboard — distinct from Theme.LogoAssetPath,
    // which is cropped/sized for the compact nav slot. Null falls back to CCN's hero logo.
    string? BannerAssetPath = null,
    // Page chrome — background, panels, text. Defaulted to CCN's exact current values so
    // the Mario theme (and any older serialized palette missing these fields) is unaffected.
    // --black (borders/shadows) is deliberately NOT themed — the thick black border/offset
    // shadow is the constant neo-brutalist signature across every theme, only the colors
    // inside it change.
    string BgBase = "#F2ECD8",
    string BgDot = "#DDD5BE",
    string PanelBg = "#FFFEF7",
    string TextDark = "#1A1A1A",
    string TextMid = "#4A4035",
    string TextLight = "#8A7D6A",
    // The polka-dot page texture is a party/confetti motif, not a neutral pattern —
    // changing its color alone still reads as "Mario Party but recolored." Themes that
    // aren't a kids' party (Men's Retreat, future HBDA events) turn it off entirely for
    // a plain background instead of trying to reskin it.
    bool UseDotPattern = true
)
{
    public string BackgroundGradient =>
        $"linear-gradient(135deg,{BgStart} 0%,{BgMid} 60%,{BgEnd} 100%)";

    // Injected into :root by ThemeHead.razor — use var(--color-primary) etc. in components
    public string CssVariables => $"""
        --bg-start: {BgStart};
        --bg-mid: {BgMid};
        --bg-end: {BgEnd};
        --color-primary: {Primary};
        --color-accent: {Accent};
        --color-success: {Success};
        --color-info: {Info};
        --track-fill: {TrackFill};
        --track-bg: {TrackBg};
        --font-display: 'Fredoka One', cursive;
        --bg-base: {BgBase};
        --bg-dot: {BgDot};
        --bg-dot-image: {(UseDotPattern ? $"radial-gradient(circle, {BgDot} 1.5px, transparent 1.5px)" : "none")};
        --black: #1A1A1A;
        --panel-bg: {PanelBg};
        --panel-shadow: 4px 4px 0 #1A1A1A;
        --text-dark: {TextDark};
        --text-mid: {TextMid};
        --text-light: {TextLight};
        """;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Serialized into Theme.ColorPalette so each event can carry its own ThemeConfig.
    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static ThemeConfig? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ThemeConfig>(json, JsonOpts); }
        catch (JsonException) { return null; }
    }
}

public class ThemeService(IDbContextFactory<AppDbContext> factory, ActiveEventService activeEventSvc)
{
    private static readonly ThemeConfig MarioParty2026 = new(
        AppTitle:      "SUPER CLOT NOT PARTY '26",
        AppSubtitle:   "Super Party '26",   // drives the nav badge text — matches the literal it replaces
        // Deep royal blue-purple — matches Mario Party title screens and Switch UI chrome.
        // The old teal-green (#0d2b1e) read as sci-fi, not Mario.
        BgStart:       "#08091e",
        BgMid:         "#0e1050",
        BgEnd:         "#1a0a50",
        // Official Nintendo Mario brand palette (schemecolor.com / brandpalettes.com)
        Primary:       "#FCCF00",   // Mario yellow — coins, highlights. Much brighter than the old #F39C12.
        Accent:        "#E83030",   // Mario red — stars, danger, excitement
        Success:       "#44AF35",   // Mario green
        Info:          "#009BD9",   // Mario blue
        TrackFill:     "rgba(0,155,217,0.35)",
        TrackBg:       "rgba(14,16,80,0.5)",
        Currency1Icon: "🪙",
        Currency1Name: "Coins",
        Currency2Icon: "⭐",
        Currency2Name: "Stars",
        // Explicit rather than relying on the record's defaults — keeps every event's
        // theme, including CCN's, a fully self-contained palette as more events are added.
        BgBase:    "#F2ECD8",
        BgDot:     "#DDD5BE",
        PanelBg:   "#FFFEF7",
        TextDark:  "#1A1A1A",
        TextMid:   "#4A4035",
        TextLight: "#8A7D6A",
        UseDotPattern: true
    );

    // Default defined after MarioParty2026 to avoid null-before-init warning
    public static readonly ThemeConfig Default = MarioParty2026;

    private ThemeConfig? _active;
    public ThemeConfig Active => _active ?? Default;

    // Kept separate from ThemeConfig/ColorPalette — logo is a DB column on Theme,
    // not part of the JSON-serialized color config. Null falls back to the CCN nav logo.
    public string? LogoAssetPath { get; private set; }

    /// Resolves the active event's Theme row into a ThemeConfig + logo path.
    /// Idempotent per scope/circuit — safe to call from multiple components.
    public async Task LoadAsync()
    {
        if (_active is not null) return;

        var ev = await activeEventSvc.GetActiveEventAsync();
        if (ev is null) { _active = Default; return; }

        using var db = factory.CreateDbContext();
        var themeRow = await db.Themes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.ThemeId == ev.ThemeId);

        _active = ThemeConfig.FromJson(themeRow?.ColorPalette) ?? Default;
        LogoAssetPath = themeRow?.LogoAssetPath;
    }
}
