# Admin Event Configurability

**Date:** 2026-09-23
**Status:** Approved, pending implementation
**Driver:** Camp Harvest 2026 (mid-October) is the next chapter event. Vicki and Amanda need to stand
it up and give it its own look from the admin UI, without Tyler editing seed code. This is
sub-project 4 of 4 on the guest roadmap (guest identity → guest push notifications → attendance
tracking → admin event configurability). Tracked in issue #310.

## Context

Three earlier documents cover this feature, and two of them disagree:

- **v1.1.0 Men's Retreat spec** (`2026-07-11-v110-mens-retreat-enablement-design.md`, §4): a
  "Duplicate from…" dropdown on `/admin/events` that copies only `EventScheduleItemType` and
  `EventCapability` rows, and explicitly *not* Groups, Locations, Activities, or ScheduleItems. It was
  never built: there's no plan and no code. The same spec deferred the self-service theme UI (colors,
  logo upload) as "needed before Camp Harvest 2026."
- **v1.1.0 per-event theme plan** (`2026-07-17-v110-3-per-event-theme.md`): the theme mechanism as it
  exists today (see below).
- **CLAUDE.md "v2.0.0 — Self-Service Event Management"**: the broader vision (Event Designer subnav,
  `/admin/theme`, `/admin/capabilities`, schedule templates, setup checklist). It has duplication clone
  groups, locations, and activities, which contradicts the v1.1.0 spec.

What the code actually looks like today:

- **`/admin/events`** already creates and edits events with name, dates, event type, a Theme dropdown,
  the Active toggle, the guest code with a QR link, and capability checkboxes. A separate
  `/admin/capabilities` page would duplicate it.
- **Theme rendering.** `Event.ThemeId` → `Theme` row. `Theme.ColorPalette` is a `ThemeConfig` record
  serialized as JSON. `ThemeService` (scoped) reads the active event's theme once per circuit and
  falls back to the hardcoded Mario Party config when the JSON is null or unparseable. `ThemeHead.razor`
  injects `ThemeConfig.CssVariables` into `:root`.
- **Logos and banners are static files.** `Theme.LogoAssetPath` is a `wwwroot/img/` path for the nav
  badge. The Dashboard banner is `ThemeConfig.BannerAssetPath`, inside the JSON. When either is null,
  the nav, login, loading splash, and Dashboard fall back to **CCN's Mario artwork**, so a new event
  with no logo would show the Camp Clot Not logo.
- **`SeedThemeAsync` overwrites both seeded theme rows on every restart.** Its own comment says this has
  to stop once an admin UI exists. CCN's row stores `ColorPalette = null` and relies on the fallback.
- **Themes are shareable.** Several events can point at one `Theme` row. The existing dropdown lets an
  admin do that, so editing one row can restyle more than one event.
- **Locations and Info pages aren't event-scoped** (no `EventId`), so every event already shares them.
  They never needed duplicating, which settles part of the v1.1.0 vs. v2.0.0 conflict.
- **Event-scoped data:** `EventCapability`, `EventScheduleItemType`, `Group` (with board positions and
  transactions), `Activity` (with board spaces and scripted games), `Sponsor`, `StaffMember` (the public
  directory card), `CampDocument`, `ScheduleItem`, `Announcement`, `IncidentReport`, and guest visits.
- **Changes show up without a redeploy.** `ActiveEventService` caches the active `Event` for 30 seconds.
  `ThemeService` reads the `Theme` row fresh on every new circuit and never caches it across circuits.
  A saved theme edit therefore appears on everyone's next full page load.

### Looking ahead to #311

#311 (breakout slots, session sign-ups, per-event staff) comes next. It adds a per-event staff
assignment (for example `EventStaff`: user, event, role at this event), separate from the public
`StaffMember` directory, and its spec has to decide what duplication copies for it. This design keeps
the copy options as an explicit, named list (`EventCopyOptions`), so #311 can add
`CopyStaffAssignments` as one more checkbox and one more copy step without reworking anything here.

## Roadmap (for traceability)

1. Named guest identity — done (#304)
2. Guest push notifications — done (#306 / PR #307)
3. Attendance tracking — done (#308 / PR #309)
4. **Admin event configurability** (this doc)

## Design Decisions (settled with Tyler before writing)

| Question (from #310) | Decision |
|---|---|
| What does duplication copy? | **Always:** enabled capabilities and enabled schedule item types (the v1.1.0 baseline). **Opt-in checkboxes:** Sponsors, Staff directory cards, and Activities. **Never:** Groups, ScheduleItems, Announcements, Documents, guest data, or the guest code. Locations and Info pages are already shared across events, so there's nothing to copy. This resolves the v1.1.0 vs. v2.0.0 conflict: the v1.1.0 baseline plus three opt-ins. Groups stay excluded as v1.1.0 argued. |
| How much theme control? | **Presets plus key overrides.** Pick a preset (Super Mario Party, Men's Retreat, Classic), then override nine key colors, the heading font (from fonts the app already loads), the dot pattern, and the border style (Bold or Soft, plus a border color for Soft). Board and projector colors, currency names, and the other advanced fields come from the preset and aren't edited directly. |
| Shared themes or one per event? | **One per event.** Creating an event creates its own `Theme` row, copied from a preset or from the event it's duplicated from. Editing one event's theme never changes another. The Theme dropdown on `/admin/events` becomes a "Start from…" choice when creating, and "Edit theme →" when editing. |
| Logo, banner, and text | **Upload the nav logo and the Dashboard banner into the DB**, like sponsor and location images, served from `/theme-logo/{themeId}` and `/theme-banner/{themeId}`. The existing static paths remain the fallback, so CCN and Men's Retreat look the same until someone uploads. **App title and nav subtitle are editable.** |
| Where does the editor live? | **New `/admin/theme` page** with an event picker (defaults to the active event) and a live preview. Each row on `/admin/events` gets a "Theme" link. The nav gets a "Theme" link next to Events. |
| Seed behaviour | **Insert-only.** `SeedThemeAsync` creates missing theme rows and never overwrites existing ones. As a one-time backfill, CCN's row gets its Mario palette written as real JSON (only while `ColorPalette` is still null), so it can be edited like any other. |
| v2.0.0 extras? | **None.** No Event Designer nav group, no `/admin/capabilities` page (capabilities already live on `/admin/events`), no setup checklist, no schedule templates. |

## Data Model

### `Theme` gains five columns

```csharp
public byte[]? LogoData { get; set; }          // uploaded nav/hero logo; wins over LogoAssetPath
public string? LogoContentType { get; set; }
public byte[]? BannerData { get; set; }        // uploaded Dashboard banner; wins over ThemeConfig.BannerAssetPath
public string? BannerContentType { get; set; }
public DateTime? UpdatedAt { get; set; }       // UTC; cache-buster (?v=) on image URLs, "last edited" on the editor
```

`Event.ThemeId` stays a required many-to-one FK. The schema allows sharing, and the app stops creating
it: every event created from now on gets its own row, and the backfill below splits any rows that are
shared today. There's no unique index on `Event.ThemeId`. Adding one would make the migration fail on
any prod DB that currently shares a theme, before the backfill has a chance to run. One row per event
is an app invariant, the same way "exactly one active event" already is.

Migration name: `AddThemeUploads`. Five nullable columns and nothing else, so it can't fail on
existing data.

### `ThemeConfig` gains one field

```csharp
bool UseCcnArtwork = false   // trailing optional parameter
```

When a theme has no logo or banner, this flag decides what shows instead: the Camp Clot Not artwork,
or neutral text. The hardcoded Mario config sets it to `true`. Any older serialized palette that lacks
the field (Men's Retreat's, for example) reads as `false`, which is correct, because Men's Retreat has
its own logo and banner. The field is part of the preset "look". Applying a preset sets it, and there's
no separate control for it.

## Presets

A new static class, `ThemePresets` (in `ThemeService.cs` next to `ThemeConfig`), holds three code-defined
presets. Each one has a `Key`, `Name`, `Description`, a full `ThemeConfig`, and an optional static
`LogoAssetPath`:

| Key | Look | Notes |
|---|---|---|
| `mario` | Today's `MarioParty2026` constant, unchanged | `UseCcnArtwork = true` |
| `mens-retreat` | Today's Men's Retreat palette, moved out of `SeedService` | Static nav logo and banner |
| `classic` | New: neutral HBDA look. Cool light gray page, white panels, sky-blue primary, Poppins headings, Soft borders, no dots | `UseCcnArtwork = false`. The sensible default for a new chapter event |

`ThemeService.Default` stays the Mario config, so the null or bad-JSON fallback doesn't change.

Presets only live in code. Adding a fourth later is a code change, which is fine: presets are starting
points, and each event's own row is what an admin edits.

## Event Theme Rows

### Creating an event

The create form's "Theme" select becomes **"Start from"**, with these options:

- the three presets (default **Classic**)
- **"Same as ‹source event›"**, shown and selected by default when a duplication source is picked

The new `Theme` row gets:

- `Name` = the event name, `Year` = `EffDate.Year`, `Description` = `"Theme for ‹event name›"`
- **From a preset:** the preset's config with `AppTitle` = event name upper-cased and `AppSubtitle` =
  event name. `LogoAssetPath` and `BannerAssetPath` are cleared for every preset, so a new event never
  picks up another event's logo. The Mario preset still shows CCN artwork through `UseCcnArtwork`.
- **From a source event:** a copy of the source theme's `ColorPalette`, `LogoAssetPath`, `LogoData` or
  `LogoContentType`, and `BannerData` or `BannerContentType`. It's a true byte copy, so later edits or
  removals on either event don't affect the other. `AppTitle` and `AppSubtitle` are copied unchanged,
  and the admin renames them on `/admin/theme`.

### Backfill for shared rows (seed, idempotent)

A new `SeedService.SplitSharedThemesAsync()` runs after events are seeded. For each `Theme` referenced
by more than one event, it keeps the row on one "owner" event and gives every other event its own copy,
using the same clone helper as duplication. The owner is the event whose ID is the seeded owner (CCN
2026 for the Mario row); otherwise it's the event with the earliest `EffDate`. After the first run no
row is shared, so later runs do nothing.

### `SeedThemeAsync` becomes insert-only

- It inserts the Mario and Men's Retreat rows only when they're missing, using the preset configs.
- One-time backfill: if the Mario row's `ColorPalette` is null, it writes the Mario preset JSON. The
  render is identical, and the row becomes editable.
- It never overwrites an existing row's palette or logo again. The comment in the method is updated to
  say why.

## `ThemeService` Changes

`LoadAsync` reads the theme with a projection, so it doesn't pull logo or banner bytes on every circuit:

```csharp
.Select(t => new { t.ThemeId, t.ColorPalette, t.LogoAssetPath, t.UpdatedAt,
                   HasLogo = t.LogoData != null, HasBanner = t.BannerData != null })
```

It then resolves:

- **`Active`**: the parsed config. When there's an uploaded banner, it's
  `with { BannerAssetPath = "/theme-banner/{id}?v={ticks}" }`, so the Dashboard needs no new logic to
  prefer uploads.
- **`NavLogoUrl`**: the uploaded logo (`/theme-logo/{id}?v={ticks}`), else `LogoAssetPath`, else
  `/img/ccn-logo-nav.webp` when `UseCcnArtwork`, else `null`.
- **`HeroLogoUrl`** (login page and loading splash): the same chain, but the CCN fallback is
  `/img/ccn-logo-2026.webp?v=2`, which is what those two places show today.

`LogoAssetPath` on the service is replaced by these two properties. The five places that used it
(`AppNav` ×2, `GuestNav` ×2, `Login`, `_Layout.cshtml`) switch over. When the URL is `null`, the nav badge shows
`AppTitle` as a text wordmark in the heading font, and the login page and splash show no image (the
login card already says "Staff Sign In").

**Dashboard hero.** When there's no banner and `UseCcnArtwork` is set, it shows the Mario coins and
stars plus the CCN logo, exactly as today. When there's no banner and the flag is off, it shows
`HeroLogoUrl` if there is one, and otherwise just the welcome text.

Caching doesn't change. The theme is resolved once per circuit, so a saved edit appears on each
viewer's next full page load. The editor says so and has a "Reload to see it" button
(`NavigateTo(..., forceLoad: true)`).

## `/admin/theme`

`@attribute [Authorize(Roles = "Admin")]`. It uses the ccn-panel styling, the same way `/admin/events`
does. It takes an optional `?eventId=` query parameter and otherwise uses the active event.

- **Header:** an event picker (a select listing all events, the active one marked), "last edited …",
  and a link back to `/admin/events`.
- **Branding panel:**
  - App title (text, 60 chars) and nav subtitle (text, 40 chars).
  - Nav logo: current image preview, `InputFile` upload, and a **Remove** button.
  - Dashboard banner: current image preview, `InputFile` upload, and a **Remove** button.
  - Uploads accept PNG, JPEG, or WebP only, up to **2 MB**. The logo appears on every page load, so it
    has to stay small. SVG isn't accepted because it's served same-origin and can carry script.
  - An upload saves right away (its own call), the same way the sponsor and location forms keep the
    bytes out of the text fields' save. **Remove** clears both the uploaded bytes and the static
    fallback path (`LogoAssetPath` or `BannerAssetPath`). The seed is insert-only now, so a removed
    Men's Retreat logo stays removed.
- **Look panel:**
  - **Apply preset**: a select plus an Apply button. It replaces colors, font, dots, border style,
    board/projector colors, currency names, and `UseCcnArtwork` with the preset's values. Title,
    subtitle, logo, and banner are kept. Asks `confirm()` first.
  - **Colors:** nine rows, each a color swatch (`<input type="color">`) plus a hex text box. The rows
    are Primary, Accent, Success, Info, Page background, Panel background, Text (dark), Text (mid), and
    Text (light). Each row has a short hint about where the color is used (for example, "Primary:
    buttons, highlights, Dashboard banner background"). Hex has to match `^#[0-9A-Fa-f]{6}$`, or the
    field shows an error and Save is disabled.
  - **Heading font:** a select with Fredoka One (playful), Poppins (clean), and Nunito (friendly). All
    three are already loaded by `ThemeHead.razor`'s font import, so no new network fonts are needed.
  - **Dot pattern:** a checkbox. The dot color field appears only when it's on.
  - **Border style:** Bold (3px, `#1A1A1A`, hard 4px offset shadow) or Soft (1.5px border in a chosen
    color, soft blurred shadow). The border color picker appears only for Soft. The page detects the
    current style from `BorderWidth == "3px"`.
- **Live preview panel:** a wrapper `<div>` whose `style` attribute is set to the draft config's
  `CssVariables`. Custom properties cascade, so a mini nav band (logo or wordmark plus subtitle), a
  Dashboard banner tile, a panel with a heading and body text, a `ccn-btn`, and a chip all render in the
  draft theme without changing the rest of the page. The preview updates as each field changes.
- **Save:** writes `ColorPalette` (the full `ThemeConfig` JSON) and `UpdatedAt`. It shows a snackbar
  ("Theme saved. Reload to see it everywhere.") and a Reload button when the edited event is the active
  one.

A new scoped `ThemeAdminService` handles persistence: `GetForEventAsync(eventId)`,
`SaveConfigAsync(themeId, ThemeConfig)`, `SetLogoAsync` / `ClearLogoAsync`, and `SetBannerAsync` /
`ClearBannerAsync`. It uses synchronous `CreateDbContext()` (CLAUDE.md pitfall #14) and sets
`UpdatedAt = DateTime.UtcNow` on every write.

### Image endpoints (`Program.cs`)

```
GET /theme-logo/{themeId:guid}     → Theme.LogoData   (404 if null)
GET /theme-banner/{themeId:guid}   → Theme.BannerData (404 if null)
```

`AllowAnonymous()`, because the login page and guest nav show the logo. These follow the
`/location-image/{id}` pattern, plus a `Cache-Control: public, max-age=86400` header. The `?v=` ticks
in the URL change on every upload, so a new image is never served stale.

## Duplication on `/admin/events`

### Form

In **create mode**, the form gets a **"Copy setup from"** select (None, or any existing event) near the
top. Picking a source:

- pre-fills the Capability checkboxes from the source's enabled capabilities. The admin can still
  change them, and what's checked at save time is what gets saved.
- shows the **"Also copy"** checkboxes with counts: `Sponsors (12)`, `Staff directory (8)`,
  `Activities (5)`. All three start unchecked.
- shows a note: "Enabled schedule item types are always copied. Groups, schedule items, announcements,
  and documents aren't copied. Locations and info pages are shared by all events."
- switches "Start from" (theme) to "Same as ‹source›".

Each table row (and each mobile card) gets a **Duplicate** button. It opens the create form (the
overlay on mobile) with that event set as the source, and leaves name and dates empty for the admin to
fill in.

In **edit mode**, the Theme select is replaced by an **"Edit theme →"** link to
`/admin/theme?eventId={id}`. Themes aren't reassigned between events any more.

### `EventSetupService`

A new scoped service creates the event and does all the copying in **one `SaveChangesAsync`**, so a
failure leaves no half-copied event:

```csharp
public record EventCopyOptions(bool Sponsors, bool StaffDirectory, bool Activities);
// #311 will add: bool StaffAssignments

public record NewEventRequest(
    string Name, DateOnly EffDate, DateOnly ExpDate, Guid EventTypeId, bool IsActive, string? GuestCode,
    IReadOnlySet<Guid> CapabilityIds,
    string? ThemePresetKey,          // null when copying the source's theme
    Guid? CopyFromEventId,
    EventCopyOptions Copy);

Task<Guid> CreateAsync(NewEventRequest req);
Task<CopyCounts> GetCopyCountsAsync(Guid sourceEventId);   // for the checkbox labels
```

What each copy does:

| Data | Copied fields | Not copied |
|---|---|---|
| `EventScheduleItemType` | every row for the source (always) | — |
| `EventCapability` | the checked set from the form (pre-filled from the source) | — |
| `Theme` | a new row, per "Event Theme Rows" above | — |
| `Sponsor` | Name, LogoUrl, LogoData or LogoContentType, Website, ContactName, Phone, SortOrder | — |
| `StaffMember` | DisplayName, RoleTitle, Phone, Email, Photo bytes, content type, and position, AvatarEmoji, IsVisible, SortOrder, LinkedUserId | — |
| `Activity` | Name, Description, ActivityTypeId, LocationId, ShowInSpinner | BoardSpaces, ScriptedMiniGames (game setup for the old event) |

Every copied row gets a new primary key and the new event's ID. Activating the new event and the
guest-code uniqueness check work exactly as they do on today's save path. The existing "deactivate
other events" logic runs in the same context. After the save, the service calls
`ActiveEventService.InvalidateCache()` and `CapabilityService.InvalidateCache(newId)`. The other
per-event caches are keyed by event ID, and a brand-new ID has no cache entries yet.

The edit path on `/admin/events` doesn't change, apart from dropping the theme reassignment.

## Explicitly Out of Scope

- An Event Designer nav group, a `/admin/capabilities` page, a setup checklist widget, and schedule
  templates (the v2.0.0 extras).
- Copying Groups, ScheduleItems, board spaces, scripted games, announcements, documents, guest data,
  or attendance.
- Raw editing of every `ThemeConfig` field: board gradient, track colors, custom shadow CSS, currency
  names. They come from presets.
- New web fonts or custom font upload.
- Theming the PWA `manifest.json` or app icons. They're static files shared by every event.
- Live-pushing theme changes to open circuits over SignalR. Edits appear on the next full load.
- Deleting events.
- Anything in #311: per-event staff assignment, breakout slots, sign-ups.

## Testing

No automated test suite exists for this project (established project-wide convention).
Verification is `dotnet build` with no new warnings, plus a manual walkthrough:

- On a fresh DB and on a copy that has one theme shared by two events: the seed splits the shared row,
  CCN's palette is written as JSON, and CCN renders exactly as before (Mario colors, CCN logos, coins
  on the Dashboard).
- Restart the app and confirm theme edits survive (the seed no longer overwrites them).
- `/admin/theme`: change primary and page background, switch the font, turn dots off, switch to Soft
  borders. The preview updates live. After Save and Reload the whole app matches.
- Upload a logo and a banner. The nav, login, splash, and Dashboard show them. Remove each one: the
  logo falls back to the text wordmark (Classic) or the CCN logo (Mario). A 3 MB file and an SVG are
  rejected.
- Apply a preset. Colors change, and title, subtitle, and logo are kept.
- Create "Camp Harvest 2026" from the Classic preset with no source. It gets its own theme row with a
  text wordmark and no CCN art, no schedule item types, and the default capabilities.
- Duplicate Men's Retreat with Sponsors and Staff directory checked. The new event has the same
  capabilities, schedule item types, theme (logo included), sponsors (logos included), and staff cards
  (photos included), and no activities. Editing the copy's theme doesn't change Men's Retreat's.
- Duplicate CCN with Activities checked. Activities are copied without board spaces or scripts.
- Duplicate with the guest code set to one already in use. The save fails with the existing message
  and nothing is created.
