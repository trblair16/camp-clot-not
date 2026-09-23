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
    bool UseDotPattern = true,
    // Typography + border/shadow "shape language" — the other half of what reads as
    // Mario Party independent of color: the bold comic-style heading font and the thick
    // black offset hard-shadow (neo-brutalist game-UI convention). Defaulted to CCN's
    // current exact values so Mario is unaffected.
    string HeadingFont = "'Fredoka One', cursive",
    string BorderColor = "#1A1A1A",
    string BorderWidth = "3px",
    string PanelShadow = "4px 4px 0 #1A1A1A",
    // What shows when the theme has no logo/banner of its own: CCN's Mario artwork (nav logo,
    // hero logo, Dashboard coins/stars) when true, a plain text wordmark when false. Part of a
    // preset's look, so older serialized palettes without it (Men's Retreat) read as false.
    bool UseCcnArtwork = false
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
        --font-display: {HeadingFont};
        --bg-base: {BgBase};
        --bg-dot: {BgDot};
        --bg-dot-image: {(UseDotPattern ? $"radial-gradient(circle, {BgDot} 1.5px, transparent 1.5px)" : "none")};
        --black: #1A1A1A;
        --border-color: {BorderColor};
        --border-width: {BorderWidth};
        --panel-bg: {PanelBg};
        --panel-border: {BorderWidth} solid {BorderColor};
        --panel-shadow: {PanelShadow};
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

/// A code-defined starting look for an event's theme. Admins pick one when creating an event
/// (or apply one later on /admin/theme); the event's own Theme row is what they then edit.
public record ThemePreset(string Key, string Name, string Description, ThemeConfig Config,
                          string? LogoAssetPath = null);

public static class ThemePresets
{
    public static readonly ThemePreset Mario = new(
        "mario", "Super Mario Party", "CCN 2026 — cream dots, bold black borders, Camp Clot Not artwork",
        new ThemeConfig(
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
            UseDotPattern: true,
            HeadingFont: "'Fredoka One', cursive",
            BorderColor: "#1A1A1A",
            BorderWidth: "3px",
            PanelShadow: "4px 4px 0 #1A1A1A",
            UseCcnArtwork: true
        ));

    // Sampled directly from the Men's Retreat flyer (Columbus GA riverwalk photo,
    // HBDA in blue, MEN'S RETREAT in gold, red date ribbon, dark wood sign, tree
    // foliage) so the theme actually matches the logo instead of an approximation.
    public static readonly ThemePreset MensRetreat = new(
        "mens-retreat", "Men's Retreat", "Warm parchment, gold and navy, soft borders",
        new ThemeConfig(
            AppTitle:      "HBDA MEN'S RETREAT",
            AppSubtitle:   "HBDA Men's Retreat 2026",
            BgStart:       "#071c33",   // deep navy — from the flyer's sky blue, darkened
            BgMid:         "#16241a",   // dark forest — from the riverwalk tree foliage
            BgEnd:         "#2b1608",   // dark wood brown — from the wood sign
            Primary:       "#D9A62A",   // gold — matches "MEN'S RETREAT" lettering
            Accent:        "#D71E03",   // red — matches the date ribbon
            Success:       "#4C7A34",   // green — matches sunlit tree foliage
            Info:          "#0F75DC",   // blue — matches "HBDA" lettering exactly
            TrackFill:     "rgba(15,117,220,0.35)",
            TrackBg:       "rgba(22,36,26,0.5)",
            Currency1Icon: "🪙",
            Currency1Name: "Coins",
            Currency2Icon: "⭐",
            Currency2Name: "Stars",
            BannerAssetPath: "/img/mens-retreat-banner.webp",
            // Page chrome — warm khaki/parchment instead of CCN's cream, dark brown text
            // instead of near-black, evoking the wood sign and outdoor riverwalk setting.
            BgBase:    "#EDE0C4",
            BgDot:     "#D4C29A",
            PanelBg:   "#FBF6E8",
            TextDark:  "#2A1D0F",
            TextMid:   "#5C4A32",
            TextLight: "#8C795C",
            // Polka dots read as party/confetti no matter the color — off for a retreat.
            UseDotPattern: false,
            // Warm-professional shape language: clean rounded sans instead of the comic-book
            // display font, thin warm-brown border instead of thick black, soft warm-tinted
            // shadow instead of a hard offset — reads as a nonprofit event app, not a re-skinned
            // party game.
            HeadingFont: "'Poppins', sans-serif",
            BorderColor: "#B89968",
            BorderWidth: "1.5px",
            PanelShadow: "0 4px 14px rgba(42,29,15,0.16)"
        ),
        LogoAssetPath: "/img/mens-retreat-nav-logo.webp");

    // Neutral default for a new chapter event — no party motifs, no event-specific artwork.
    public static readonly ThemePreset Classic = new(
        "classic", "Classic", "Clean light gray and sky blue, soft borders — a neutral HBDA default",
        new ThemeConfig(
            AppTitle:      "HBDA EVENTS",
            AppSubtitle:   "HBDA Events",
            BgStart:       "#0b1a2e",
            BgMid:         "#12304f",
            BgEnd:         "#1b2733",
            Primary:       "#4FA3E0",   // sky blue — readable with the black banner text on top
            Accent:        "#D64545",
            Success:       "#3E9B5B",
            Info:          "#2F6FB0",
            TrackFill:     "rgba(79,163,224,0.35)",
            TrackBg:       "rgba(18,48,79,0.5)",
            Currency1Icon: "🪙",
            Currency1Name: "Coins",
            Currency2Icon: "⭐",
            Currency2Name: "Stars",
            BgBase:    "#F3F5F7",
            BgDot:     "#DCE3EA",
            PanelBg:   "#FFFFFF",
            TextDark:  "#1B2733",
            TextMid:   "#45556A",
            TextLight: "#7A8899",
            UseDotPattern: false,
            HeadingFont: "'Poppins', sans-serif",
            BorderColor: "#C5CED8",
            BorderWidth: "1.5px",
            PanelShadow: "0 4px 14px rgba(27,39,51,0.12)"
        ));

    public static IReadOnlyList<ThemePreset> All { get; } = [Classic, MensRetreat, Mario];

    public static ThemePreset? Get(string? key) => All.FirstOrDefault(p => p.Key == key);
}

public class ThemeService(IDbContextFactory<AppDbContext> factory, ActiveEventService activeEventSvc)
{
    // Fallback when there's no active event or a Theme row's JSON is null/unparseable.
    public static ThemeConfig Default => ThemePresets.Mario.Config;

    private ThemeConfig? _active;
    public ThemeConfig Active => _active ?? Default;

    public Guid? ThemeId { get; private set; }

    // Compact logo for the nav badge. Null means "no logo" — the nav renders AppTitle as a
    // text wordmark instead.
    public string? NavLogoUrl { get; private set; } = "/img/ccn-logo-nav.webp";

    // Larger logo for the login page and loading splash. Null means show no image.
    public string? HeroLogoUrl { get; private set; } = "/img/ccn-logo-2026.webp?v=2";

    /// Resolves the active event's Theme row into a ThemeConfig + logo URLs.
    /// Idempotent per scope/circuit — safe to call from multiple components.
    public async Task LoadAsync()
    {
        if (_active is not null) return;

        var ev = await activeEventSvc.GetActiveEventAsync();
        if (ev is null) { _active = Default; return; }

        using var db = factory.CreateDbContext();
        // Projection — never pull logo/banner bytes just to render a page.
        var row = await db.Themes.AsNoTracking()
            .Where(t => t.ThemeId == ev.ThemeId)
            .Select(t => new
            {
                t.ThemeId, t.ColorPalette, t.LogoAssetPath, t.UpdatedAt,
                HasLogo = t.LogoData != null, HasBanner = t.BannerData != null
            })
            .FirstOrDefaultAsync();

        var config = ThemeConfig.FromJson(row?.ColorPalette) ?? Default;
        if (row is null) { _active = config; return; }

        var v = row.UpdatedAt?.Ticks ?? 0;
        if (row.HasBanner)
            config = config with { BannerAssetPath = $"/theme-banner/{row.ThemeId}?v={v}" };

        var ownLogo = row.HasLogo ? $"/theme-logo/{row.ThemeId}?v={v}" : row.LogoAssetPath;
        ThemeId     = row.ThemeId;
        NavLogoUrl  = ownLogo ?? (config.UseCcnArtwork ? "/img/ccn-logo-nav.webp" : null);
        HeroLogoUrl = ownLogo ?? (config.UseCcnArtwork ? "/img/ccn-logo-2026.webp?v=2" : null);
        _active     = config;
    }
}
