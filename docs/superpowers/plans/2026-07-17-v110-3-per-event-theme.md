# v1.1.0 Sub-project 3: Per-Event Theme Implementation Plan

**Date:** 2026-07-17
**Depends on:** Sub-project 1 (active-event wiring) and sub-project 2 (capability
gating), both merged to `dev`.
**Driver:** Men's Retreat 2026 needs a visually distinct theme (forest green / warm
tan) without touching Mario Party's cream/black neo-brutalist look for CCN 2026.

## Global Constraints

- **No EF migration required.** `Event.ThemeId` (required `Guid` FK) and
  `Theme.ColorPalette` (nullable `string`) both already exist in the schema — this
  release only populates data and changes how it's read.
- `Theme.ColorPalette` stores the full `ThemeConfig` record serialized as JSON
  (`System.Text.Json`, case-insensitive). CCN 2026's seeded `Theme` row keeps
  `ColorPalette = null` — `ThemeService` falls back to the hardcoded
  `MarioParty2026` constant when null/unparseable, so CCN 2026 renders identically
  with zero seed changes to its own row.
- `ThemeService` moves from `AddSingleton` (one theme for the whole process) to
  `AddScoped` (resolved per circuit/request via the active event), matching the
  `ActiveEventService`/`CapabilityService` pattern already in place.
- Only 3 call sites read `ThemeSvc.Active`: `ThemeHead.razor`, `BoardDisplay.razor`,
  `ProjectorOverlay.razor`. Each needs to `await ThemeSvc.LoadAsync()` in
  `OnInitializedAsync` before reading `.Active` — `LoadAsync` is idempotent per
  scope (no-ops if already loaded), so calling it from multiple components in the
  same circuit is safe and cheap.

---

## Task 1: Give `Theme.ColorPalette` a JSON shape and make `ThemeConfig` round-trip

`ThemeConfig` (`CampClotNot/Services/ThemeService.cs`) is a `record` with a
positional constructor and read-only properties — `System.Text.Json` can already
deserialize into it via constructor-parameter matching, no changes to the record
itself needed. Add a small serialize/deserialize helper pair colocated with the
record:

```csharp
public record ThemeConfig(...)
{
    ...

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static ThemeConfig? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ThemeConfig>(json, JsonOpts); }
        catch (JsonException) { return null; }
    }
}
```

`using System.Text.Json;` added to the top of the file.

---

## Task 2: Convert `SeedThemeAsync` to upsert-by-id and add the Men's Retreat theme

Current `SeedThemeAsync` uses the `if (await db.Themes.AnyAsync()) return;` guard —
that's a problem the moment a second theme needs seeding on a DB that already has
the CCN row, so switch it to the upsert-by-id pattern already used by
`SeedCapabilitiesAsync`/`SeedActivityTypeCategoriesAsync`.

Add a new stable ID to `SeedService.Id`:

```csharp
public static readonly Guid ThemeMensRetreat2026 = new("00000004-0004-0004-0004-000000000002");
```

Rewrite `SeedThemeAsync`:

```csharp
private async Task SeedThemeAsync(AppDbContext db)
{
    var mensRetreatPalette = new ThemeConfig(
        AppTitle:      "HBDA MEN'S RETREAT",
        AppSubtitle:   "HBDA Men's Retreat 2026",
        BgStart:       "#0d1f14",
        BgMid:         "#1a3620",
        BgEnd:         "#2e2416",
        Primary:       "#C9A063",   // warm wood/tan
        Accent:        "#8B4A2B",   // rust/burnt orange
        Success:       "#3E7C4A",   // forest green
        Info:          "#5C7A6E",   // muted sage
        TrackFill:     "rgba(62,124,74,0.35)",
        TrackBg:       "rgba(26,54,32,0.5)",
        Currency1Icon: "🪙",
        Currency1Name: "Coins",
        Currency2Icon: "⭐",
        Currency2Name: "Stars"
    );

    var defs = new[]
    {
        new { Id = Id.ThemeSuperMarioParty2026, Name = "Super Mario Party", Year = 2026,
              Description = "Super Mario Party themed camp — CCN 2026", Palette = (string?)null },
        new { Id = Id.ThemeMensRetreat2026, Name = "Men's Retreat", Year = 2026,
              Description = "HBDA Men's Retreat 2026 — forest green & tan", Palette = mensRetreatPalette.ToJson() },
    };

    foreach (var d in defs)
    {
        if (await db.Themes.AnyAsync(t => t.ThemeId == d.Id)) continue;
        db.Themes.Add(new Theme
        {
            ThemeId      = d.Id,
            Name         = d.Name,
            Year         = d.Year,
            Description  = d.Description,
            ColorPalette = d.Palette
        });
    }
    await db.SaveChangesAsync();
    logger.LogInformation("Seeded Themes.");
}
```

Note: CCN 2026's row is included in the `defs` upsert loop but keeps
`Palette = null` — this is only to make the row idempotently present by ID, it does
not touch `ColorPalette` on an existing row (the `AnyAsync` check skips existing
rows entirely, so no risk of nulling out a hand-edited palette later).

`using CampClotNot.Services;` is not needed — `SeedService` is already in that
namespace and `ThemeConfig` is a sibling type in the same namespace.

---

## Task 3: `ThemeService` — singleton to scoped, resolve via active event

Rewrite `CampClotNot/Services/ThemeService.cs`'s `ThemeService` class (the
`ThemeConfig` record and `CampTime` helper are unchanged):

```csharp
public class ThemeService(
    IDbContextFactory<AppDbContext> factory,
    ActiveEventService activeEventSvc)
{
    private static readonly ThemeConfig MarioParty2026 = new(
        // ...unchanged fields...
    );

    public static readonly ThemeConfig Default = MarioParty2026;

    private ThemeConfig? _active;
    public ThemeConfig Active => _active ?? Default;

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
    }
}
```

`_active` is intentionally never invalidated mid-circuit — a theme change while a
user has an open tab is an acceptable edge case (matches how `ActiveEventService`'s
cache already has staleness tolerance built in via its 30s TTL). A hard refresh
picks up the new scope and re-resolves.

---

## Task 4: Registration — `Program.cs`

```csharp
builder.Services.AddScoped<ThemeService>();   // was AddSingleton — now resolves per active event
```

Must be registered after `ActiveEventService` is available for constructor
injection — DI container doesn't care about registration order for constructor
resolution, so this can stay on the same line, just change `AddSingleton` →
`AddScoped`.

---

## Task 5: Update the 3 consumers to load before reading `.Active`

**`CampClotNot/Shared/ThemeHead.razor`** — add a code block:

```razor
@code {
    protected override async Task OnInitializedAsync() => await ThemeSvc.LoadAsync();
}
```

**`CampClotNot/Pages/BoardDisplay.razor`** and
**`CampClotNot/Shared/ProjectorOverlay.razor`** — both already have
`OnInitializedAsync` (loading board state / standings respectively). Add
`await ThemeSvc.LoadAsync();` as the first line of each existing method rather than
adding a second override.

`CampClotNot/Shared/AppNav.razor`, `Pages/Board.razor`, `Pages/Leaderboard.razor`
inject `ThemeService` but never read `.Active` — leave untouched (dead injections,
out of scope for this release).

---

## Task 6: Theme picker on `/admin/events`

Currently `Events.razor` hardcodes new events to `_themes.First().ThemeId`
(`SaveAsync`, line ~328) — with two themes seeded now, "first" is whichever the DB
happens to return first, which is not reliable. Add an explicit dropdown next to
the Event Type selector:

- New field: `private Guid _fThemeId;`
- Form markup (styled to match the existing Event Type `<select>` block):
  ```razor
  <div style="margin-bottom:14px">
      <div style="...label styling...">Theme</div>
      <select value="@_fThemeId.ToString()"
              @onchange="e => _fThemeId = Guid.TryParse(e.Value?.ToString(), out var v) ? v : _fThemeId"
              style="...matches Event Type select styling...">
          @foreach (var th in _themes)
          {
              <option value="@th.ThemeId">@th.Name</option>
          }
      </select>
  </div>
  ```
- `OnInitializedAsync`: default `_fThemeId` to `_themes.First().ThemeId` once themes
  are loaded (same fallback as today, just now admin-overridable).
- `StartEdit(Event ev)`: `_fThemeId = ev.ThemeId;`
- `ResetForm()`: reset `_fThemeId` to the same default as init.
- `SaveAsync`: replace `var themeId = _themes.Any() ? _themes.First().ThemeId : Guid.Empty;`
  with `_fThemeId` for the create branch, and add `existing.ThemeId = _fThemeId;` to
  the edit branch (currently edit doesn't touch `ThemeId` at all — this was a gap
  even before this release, since there was only one theme to have).

---

## Testing / Rollout

No automated test suite exists (per CLAUDE.md, that's a v1.2.0 goal). Manual
verification after implementation, with `dotnet build` as the compile gate since
this session's sandbox has no way to run the app against a live DB:

1. `dotnet build` clean.
2. Push to `dev`, confirm Railway staging build succeeds.
3. On staging: confirm CCN 2026 (still the only *active* event) renders unchanged —
   Mario Party cream/black theme, no visual regression.
4. Create a "Men's Retreat 2026" event on `/admin/events`, select the "Men's
   Retreat" theme in the new dropdown, mark it Active.
5. Confirm the app re-themes to forest green/tan on next page load (new circuit) —
   `ThemeHead`'s CSS vars, and if reachable, `BoardDisplay`/`ProjectorOverlay`
   (expected to be capability-gated off for Men's Retreat per sub-project 2, so this
   is a lower-priority check).
6. Flip the active event back to CCN 2026 and confirm the theme reverts.

---

## Addendum: Men's Retreat logo (added mid-implementation)

The master spec called this release text/typography-only with logo upload deferred
to a later self-service `/admin/theme` UI. Tyler supplied a real Men's Retreat
promo flyer mid-implementation (`file_<hash>.png`, 1536×1024, Columbus GA
riverwalk photo background with "HBDA MEN'S RETREAT — JULY 24TH THROUGH 26TH
2026" baked in) via a commit on `feature/297-v110-mens-retreat-enablement`, so
this got folded in as a small addition rather than deferred:

- `Theme.LogoAssetPath` (already existed on the entity, unused until now) is
  populated for the Men's Retreat theme; `ThemeService.LoadAsync()` reads it
  directly off the `Theme` row (kept separate from the `ColorPalette` JSON blob).
- Two derived assets in `wwwroot/img/`:
  - `mens-retreat-nav-logo.{png,webp}` — cropped to just the "HBDA MEN'S
    RETREAT" wordmark (1516×360, cropped from the original), wired as
    `Theme.LogoAssetPath` and rendered in `AppNav.razor`'s nav badge in place of
    `ccn-logo-nav.webp` when the active event's theme has a `LogoAssetPath`.
    Verified legible at actual nav render scale (~176×42) before wiring in.
  - `mens-retreat-banner.{png,webp}` — the full flyer, saved but **not
    currently referenced anywhere**. Kept for a future full-width banner slot
    (Dashboard/`/admin/events` header) once that UI exists — out of scope for
    this pass.
- `AppNav.razor`'s nav badge subtitle ("SUPER PARTY '26") is now driven by
  `ThemeSvc.Active.AppSubtitle` instead of being a hardcoded literal — CCN
  2026's `AppSubtitle` constant was changed to the exact string it replaces
  (`"Super Party '26"`) specifically to keep this a no-op for CCN's rendering.
- Original flyer file (`CampClotNot/wwwroot/file_00000000c98871f791135a38470263f2.png`,
  3.4MB, dropped at `wwwroot` root by the phone upload) was removed in favor of
  the two named, optimized derivatives above.
