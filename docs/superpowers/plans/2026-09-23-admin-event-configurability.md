# Admin Event Configurability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an Admin stand up and style a new chapter event without code changes. The work has two parts. First, a `/admin/theme` editor: presets, key color overrides, font, dots, border style, title and subtitle, and a DB-stored logo and banner. Second, "Copy setup from…" duplication on `/admin/events`: capabilities and schedule item types always, and sponsors, staff directory cards, and activities as opt-ins. The spec is `docs/superpowers/specs/2026-09-23-admin-event-configurability-design.md` (issue #310).

**Architecture:** Each event owns its own `Theme` row. `Theme` gains uploaded logo and banner bytes plus `UpdatedAt`. Code-defined `ThemePresets` supply starting looks. `ThemeService` resolves nav and hero logo URLs (upload → static path → CCN artwork when the preset calls for it → text wordmark), and prefers an uploaded banner. A new `ThemeAdminService` persists edits for the new `/admin/theme` page. A new `EventSetupService` creates an event, its theme row, and the copied rows in one `SaveChangesAsync`. `SeedThemeAsync` becomes insert-only, and a new `SplitSharedThemesAsync` gives every event its own theme row.

**Tech Stack:** Blazor Server (.NET 8), EF Core/Npgsql, MudBlazor 6.11, minimal APIs.

## Global Constraints

- **One theme row per event.** This is an app invariant, not a DB constraint (see the spec for why). Every create path clones a row, and the seed splits rows that are already shared.
- **The seed never overwrites theme rows again.** The only exception is a one-time write of the Mario JSON into CCN's row while its `ColorPalette` is null.
- **Uploads:** PNG, JPEG, or WebP only, 2 MB max. No SVG, because it's served same-origin.
- **Image URLs carry `?v={UpdatedAt.Ticks}`**, and the endpoints send `Cache-Control: public, max-age=86400`.
- **Duplication is atomic:** one DbContext, one `SaveChangesAsync`.
- No automated test suite exists for this project. Verification is `dotnet build` with no new warnings (baseline: 4), plus the manual walkthrough in the PR description (final task).
- Never use PowerShell for source file text edits (CLAUDE.md pitfall #1).
- Always use synchronous `factory.CreateDbContext()` / `DbFactory.CreateDbContext()`, never the async variant (pitfall #14).
- Primary-constructor DI style for new services.
- No new FKs are introduced. The existing `Event.ThemeId` FK stays as is (pitfall #13 still applies if that changes).
- Modals use the `fadeIn`/`popIn` pattern (pitfall #12). Razor `@onclick` lambdas containing `$"..."` use single-quoted attributes (pitfall #15). MudBlazor 6.11 checkboxes bind with `@bind-Checked`.
- Scope is limited to the spec. No Event Designer nav group, no `/admin/capabilities`, no setup checklist, no schedule templates, and nothing from #311.
- Work happens on branch `feature/310-admin-event-configurability` (off latest `dev`). The PR targets `dev`.

### Environment (cloud sessions)

```bash
apt-get update && apt-get install -y dotnet-sdk-8.0
dotnet tool install -g dotnet-ef --version '8.*'
export PATH="$PATH:$HOME/.dotnet/tools"
```

Migrations are applied automatically at startup (`SeedService.SeedAsync()` → `MigrateAsync()`). Creating one needs a design-time connection string but no live DB:

```bash
cd CampClotNot
ConnectionStrings__DefaultConnection="Host=localhost;Database=dummy;Username=x;Password=x" \
Vapid__PublicKey=BPlaceholderPlaceholderPlaceholder Vapid__PrivateKey=x \
dotnet ef migrations add <Name>
```

---

### Task 1: Data model: theme upload columns, `UseCcnArtwork`, migration

**Files:**
- Modify: `CampClotNot/Data/Entities/Theme.cs`
- Modify: `CampClotNot/Services/ThemeService.cs` (the `ThemeConfig` record)
- Create: EF migration `AddThemeUploads`

**Interfaces:**
- Produces: `Theme.LogoData/LogoContentType/BannerData/BannerContentType/UpdatedAt` and `ThemeConfig.UseCcnArtwork`, used by Tasks 2–6.

- [ ] **Step 1:** Add the five nullable properties to `Theme` (see the spec's Data Model section).
- [ ] **Step 2:** Add `bool UseCcnArtwork = false` as the last parameter of `ThemeConfig`, with a comment explaining the fallback rule. Set `UseCcnArtwork: true` on the Mario constant.
- [ ] **Step 3:** Generate `AddThemeUploads`. Read it and confirm it adds exactly five nullable columns to `Themes` (`bytea`, `text`, `bytea`, `text`, `timestamp with time zone`), with no shadow columns and no other table changes.
- [ ] **Step 4:** `dotnet build` shows 0 errors and no new warnings.
- [ ] **Step 5:** Commit: `feat: Theme upload columns and UseCcnArtwork flag`

---

### Task 2: Presets and logo resolution in `ThemeService`

**Files:**
- Modify: `CampClotNot/Services/ThemeService.cs`
- Modify: `CampClotNot/Shared/AppNav.razor`, `CampClotNot/Shared/GuestNav.razor`, `CampClotNot/Pages/Login.razor`, `CampClotNot/Pages/_Layout.cshtml`, `CampClotNot/Pages/Dashboard.razor`

**Interfaces:**
- Produces: `ThemePreset` record, `ThemePresets.All/Get(key)/Mario/MensRetreat/Classic`, `ThemeService.NavLogoUrl`, `ThemeService.HeroLogoUrl`, `ThemeService.ThemeId`
- Removes: `ThemeService.LogoAssetPath`

- [ ] **Step 1: Presets.** Add these to `ThemeService.cs`:

```csharp
public record ThemePreset(string Key, string Name, string Description, ThemeConfig Config,
                          string? LogoAssetPath = null);

public static class ThemePresets
{
    public static readonly ThemePreset Mario = new("mario", "Super Mario Party", "...", <Mario config>);
    public static readonly ThemePreset MensRetreat = new("mens-retreat", "Men's Retreat", "...",
        <config moved from SeedService>, "/img/mens-retreat-nav-logo.webp");
    public static readonly ThemePreset Classic = new("classic", "Classic", "...", <new config>);
    public static IReadOnlyList<ThemePreset> All { get; } = [Classic, MensRetreat, Mario];
    public static ThemePreset? Get(string? key) => All.FirstOrDefault(p => p.Key == key);
}
```

Move the Mario constant out of `ThemeService` into `ThemePresets.Mario.Config`, and keep `ThemeService.Default => ThemePresets.Mario.Config`. Move the Men's Retreat `ThemeConfig` out of `SeedService.SeedThemeAsync` without changes. Classic uses cool gray `#F3F5F7`, white panels, sky-blue primary `#4FA3E0`, accent `#D64545`, success `#3E9B5B`, info `#2F6FB0`, text `#1B2733/#45556A/#7A8899`, Poppins, a Soft border `#C5CED8`, the shadow `0 4px 14px rgba(27,39,51,0.12)`, no dots, and a navy board gradient.

- [ ] **Step 2: Resolution.** Rewrite `LoadAsync` to use a projection (never load the bytes), then set `Active`, `ThemeId`, `NavLogoUrl`, and `HeroLogoUrl`, following the spec's chain. When there's an uploaded banner, `Active` becomes `config with { BannerAssetPath = $"/theme-banner/{id}?v={ticks}" }`.
- [ ] **Step 3: Consumers.**
  - `AppNav.razor` (×2) and `GuestNav.razor` (×2): when `NavLogoUrl` is set, `<img src=NavLogoUrl>`. Otherwise show a `<span>` wordmark with `AppTitle`, using `font-family:var(--font-display)`, bold, `color:var(--color-primary)`, sized to fit the badge.
  - `Login.razor` and `_Layout.cshtml`: only render the `<img>` when `HeroLogoUrl` is set.
  - `Dashboard.razor`: show the coins and stars and the CCN logo only when `BannerAssetPath is null && UseCcnArtwork`. When there's no banner and the flag is off, show `HeroLogoUrl` if set, otherwise nothing.
- [ ] **Step 4:** Grep the code for `LogoAssetPath` and confirm only `SeedService`, `Theme.cs`, and the presets still use it.
- [ ] **Step 5:** `dotnet build` shows no new warnings.
- [ ] **Step 6:** Commit: `feat: Theme presets and uploaded-logo resolution`

---

### Task 3: Insert-only theme seed and shared-row split

**Files:**
- Modify: `CampClotNot/Services/SeedService.cs`
- Create: `CampClotNot/Services/ThemeCloner.cs` (static helpers shared with Task 6)

**Interfaces:**
- Produces: `ThemeCloner.Clone(Theme src, string eventName, int year) → Theme` (copies palette, static logo path, and logo and banner bytes, with a new ID and `UpdatedAt = UtcNow`) and `ThemeCloner.FromPreset(ThemePreset p, string eventName, int year) → Theme` (sets `AppTitle`/`AppSubtitle` from the event name and clears the logo and banner paths).

- [ ] **Step 1:** `SeedThemeAsync` inserts missing rows only (Mario row with the Mario JSON; Men's Retreat row with the preset JSON and logo path). When the Mario row's `ColorPalette` is null, it writes the Mario JSON (one-time backfill). Replace the method comment with one explaining that admins now own theme rows. This also removes the existing CS8619 anonymous-type warning, because the `defs` array goes away.
- [ ] **Step 2:** Add `SplitSharedThemesAsync(db)` and call it right after `SeedEventAsync`. For each `ThemeId` used by more than one event, the owner is the CCN event for the Mario row, otherwise the earliest `EffDate`. Every other event gets `ThemeCloner.Clone(...)`. The method logs how many rows it split.
- [ ] **Step 3:** `dotnet build` shows no new warnings.
- [ ] **Step 4:** Commit: `feat: Insert-only theme seed; give every event its own theme row`

---

### Task 4: Theme image endpoints

**Files:**
- Modify: `CampClotNot/Program.cs` (next to `/location-image/{id}`)

- [ ] **Step 1:** Add `GET /theme-logo/{themeId:guid}` and `GET /theme-banner/{themeId:guid}`. They return 404 when the bytes are null, otherwise `Results.File(bytes, contentType)`, plus `Cache-Control: public, max-age=86400` and `.AllowAnonymous()`.
- [ ] **Step 2:** Commit (together with Task 5 is fine).

---

### Task 5: `ThemeAdminService` and the `/admin/theme` page

**Files:**
- Create: `CampClotNot/Services/ThemeAdminService.cs`
- Create: `CampClotNot/Pages/Admin/ThemeEditor.razor` with `@page "/admin/theme"`. It's not named `Theme.razor` because that would generate a component class `Theme` that collides with the `Theme` entity type.
- Modify: `CampClotNot/Program.cs` (register `AddScoped<ThemeAdminService>()`)
- Modify: `CampClotNot/Shared/AppNav.razor` (a "Theme" link after Events, in both the desktop dropdown and the mobile sheet)

**Interfaces:**
- Produces:

```csharp
public record ThemeEditState(Guid ThemeId, Guid EventId, string EventName, ThemeConfig Config,
    string? LogoUrl, string? BannerUrl, DateTime? UpdatedAt);
public enum ImageUploadResult { Ok, TooLarge, BadType }

Task<ThemeEditState?> GetForEventAsync(Guid eventId);
Task SaveConfigAsync(Guid themeId, ThemeConfig config);
Task<ImageUploadResult> SetLogoAsync(Guid themeId, byte[] data, string contentType);
Task ClearLogoAsync(Guid themeId);      // clears bytes + LogoAssetPath
Task<ImageUploadResult> SetBannerAsync(Guid themeId, byte[] data, string contentType);
Task ClearBannerAsync(Guid themeId);    // clears bytes + ColorPalette.BannerAssetPath
public const long MaxImageBytes = 2 * 1024 * 1024;
public static readonly string[] AllowedImageTypes = ["image/png", "image/jpeg", "image/webp"];
```

`LogoUrl`/`BannerUrl` in the edit state resolve without the CCN fallback. They show only what this theme actually has (the upload or the static path).

- [ ] **Step 1:** Write the service. Every write sets `UpdatedAt = DateTime.UtcNow`. `ClearBannerAsync` parses the config, saves it back `with { BannerAssetPath = null }`, and nulls the bytes. If the JSON is null (it shouldn't be after the seed), it treats it as `ThemeService.Default`.
- [ ] **Step 2:** Write the page following the spec's `/admin/theme` section: the event picker (reads `?eventId=` through `[SupplyParameterFromQuery]`), a Branding panel, a Look panel, a Live preview, and Save. Draft state lives in page fields (`_title`, `_subtitle`, nine colors, `_font`, `_dots`, `_dotColor`, `_bold`, `_borderColor`). `BuildDraft()` returns `_base with { ... }`, where `_base` is the loaded config, so fields not shown in the editor (board colors, currency) survive a save. Apply preset sets `_base = preset.Config with { AppTitle = _title, AppSubtitle = _subtitle, BannerAssetPath = _base.BannerAssetPath }` and reloads the fields from it. Save and Apply are disabled while any hex field is invalid. Uploads check `file.Size` and `file.ContentType` before reading, and read with `OpenReadStream(MaxImageBytes)`.
- [ ] **Step 3:** Add the Theme nav link in both nav places.
- [ ] **Step 4:** `dotnet build` shows no new warnings.
- [ ] **Step 5:** Commit: `feat: /admin/theme editor with presets, colors, and logo/banner upload`

---

### Task 6: `EventSetupService` and duplication on `/admin/events`

**Files:**
- Create: `CampClotNot/Services/EventSetupService.cs`
- Modify: `CampClotNot/Program.cs` (register `AddScoped<EventSetupService>()`)
- Modify: `CampClotNot/Pages/Admin/Events.razor`

**Interfaces:**
- Produces: `EventCopyOptions`, `NewEventRequest`, `CopyCounts(int Sponsors, int StaffDirectory, int Activities)`, `EventSetupService.CreateAsync`, `EventSetupService.GetCopyCountsAsync` (signatures in the spec)

- [ ] **Step 1: Service.** In one context: validate the guest code's uniqueness (throw `InvalidOperationException` with the existing message, which the page catches), deactivate other events if `IsActive`, add the `Event`, the theme row (`ThemeCloner.Clone` of the source's theme when `ThemePresetKey` is null and a source exists, otherwise `ThemeCloner.FromPreset`, falling back to Classic), the `EventCapability` rows for `CapabilityIds`, the source's `EventScheduleItemType` rows, and the opt-in copies (Sponsors, StaffMembers, Activities, with fields per the spec table). One `SaveChangesAsync`. Then `ActiveEventSvc.InvalidateCache()` and `CapSvc.InvalidateCache(newId)`. Leave a comment on `EventCopyOptions` marking where #311's `StaffAssignments` flag goes.
- [ ] **Step 2: Form, create mode.**
  - A "Copy setup from" select (None + events). On change: load `GetCopyCountsAsync`, prefill `_fEnabledCapIds` from `CapSvc.GetEnabledCapabilitiesAsync(source)`, set `_fThemeStart = "source"`, and reset the three opt-ins to false. Choosing None resets capabilities to the defaults and the theme start to `classic`.
  - "Also copy" checkboxes with counts, plus the note text.
  - The Theme select becomes "Start from" with `Same as ‹source›` (only when a source is set) plus `ThemePresets.All`.
- [ ] **Step 3: Form, edit mode.** Replace the Theme select with an "Edit theme →" link to `/admin/theme?eventId={id}`. Remove `existing.ThemeId = _fThemeId` from save.
- [ ] **Step 4: Duplicate buttons.** Table rows and mobile cards get a "Duplicate" button: `ResetForm()`, set the source (the same handler as the select), and open the mobile overlay on mobile. Table rows also get a "Theme" link.
- [ ] **Step 5: Save.** The create branch calls `EventSetup.CreateAsync(...)`, and the edit branch keeps its current code. Remove the now-unused `_themes` load and `_fThemeId`.
- [ ] **Step 6:** `dotnet build` shows no new warnings.
- [ ] **Step 7:** Commit: `feat: Duplicate events with opt-in sponsors, staff directory, activities`

---

### Task 7: Verify, document, PR

- [ ] **Step 1:** Run the app against a local Postgres (see the attendance plan for the recipe) and walk through the spec's Testing list with Playwright/Chromium. Include a DB with two events sharing one theme row, to exercise the split.
- [ ] **Step 2:** Mark the spec implemented and add "Implementation Notes" for any deviations.
- [ ] **Step 3:** Push and open a PR into `dev` with the manual checklist. Put anything noticed but out of scope in the PR description, not the diff.
