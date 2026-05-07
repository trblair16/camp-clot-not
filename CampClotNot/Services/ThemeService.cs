namespace CampClotNot.Services;

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
    string Currency2Name
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
        """;
}

public class ThemeService
{
    private static readonly ThemeConfig MarioParty2026 = new(
        AppTitle:      "SUPER CLOT NOT PARTY '26",
        AppSubtitle:   "Camp Clot Not · Super Mario Party",
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
        Currency2Name: "Stars"
    );

    // Default defined after MarioParty2026 to avoid null-before-init warning
    public static readonly ThemeConfig Default = MarioParty2026;
    public ThemeConfig Active { get; } = MarioParty2026;
}
