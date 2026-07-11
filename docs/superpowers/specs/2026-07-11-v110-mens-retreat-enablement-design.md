# v1.1.0 — Men's Retreat Enablement

**Date:** 2026-07-11
**Status:** Approved, pending implementation plan
**Driver:** Men's Retreat 2026 goes live in ~2 weeks. This is the first real second event on the platform.

## Context

CCN 2026 (v1.0.0) shipped and ran successfully. Post-camp, `origin/main` picked up
significant work not reflected in local checkouts, including a real `/admin/events`
page (`CampClotNot/Pages/Admin/Events.razor`) that lets Admins create/edit events with
name, dates, event type, and an "Active" toggle.

However, the Active toggle does not yet do anything: **31 call sites across 12 page
files** still hardcode `SeedService.Id.EventCcn2026` instead of resolving whichever
event is marked active. Creating a second event today changes nothing on any
user-facing page. Wiring this up is the prerequisite for everything else in this
release.

Also confirmed during investigation:
- `Theme` is a real EF-mapped table (`CampClotNot/Data/Entities/Theme.cs`), but
  `ThemeService.cs` is a hardcoded singleton unrelated to it — one theme for the
  whole app process, not resolved per-event.
- `Capability` / `EventCapability` tables exist and are seeded (5 rows: `BoardGame`,
  `CoinShop`, `MiniGameSpinner`, `Announcements`, `Itinerary`), but nothing in the app
  reads them — nav items and pages render unconditionally.
- Two features shipped after camp (`Bowser Event`, `Awards`) have no capability flag
  at all yet.
- Guest/QR/anonymous event-scoped access has zero precedent anywhere in the
  codebase — this is greenfield.

## Scope for v1.1.0

Five sub-projects, in dependency order. Each ships incrementally to `dev` so Men's
Retreat can go live once its slice lands, without waiting on the rest.

1. Active-event wiring (foundation)
2. Capability gating (hide game features per event)
3. Per-event theme
4. Extended event management (duplication)
5. Guest QR/event-code access

Explicitly deferred (called out, not forgotten): a self-service `/admin/theme` UI for
logo upload and color picking. Men's Retreat's theme is simple enough to seed by hand;
a proper no-code theme editor is needed before Camp Harvest 2026 and should be scoped
as its own follow-up.

---

## 1. Active-event wiring (foundation)

**Goal:** Make the existing `/admin/events` "Active" toggle actually control what
every page shows.

- New `IActiveEventService` (scoped), single method surface: `GetActiveEventIdAsync()`
  / `GetActiveEventAsync()`.
- Backed by `IMemoryCache` with a short TTL (consistent with the existing
  schedule/announcement caching pattern already in `ScheduleService`), so it doesn't
  hit the DB on every page render.
- Resolves `db.Events.SingleOrDefault(e => e.IsActive)`. The single-active-event
  invariant is already enforced by `Events.razor`'s `SaveAsync` (deactivates others on
  save) — this service can trust that invariant rather than re-validating it.
- Fallback if no event is active (shouldn't happen given the invariant, but is a real
  edge case on a fresh DB before seeding completes): surface a clear "No active event
  configured" state rather than a raw exception.
- Cache invalidation: when `Events.razor` flips which event is active, clear the
  active-event cache entry explicitly (same pattern as `ScheduleService.cs:166-167`
  clearing schedule cache keys on upsert).
- Replace all 31 `SeedService.Id.EventCcn2026` references in the following files with
  calls to `IActiveEventService`:
  - `Pages/Admin/Activities.razor` (2)
  - `Pages/Admin/Games.razor` (1)
  - `Pages/Admin/Schedule.razor` (6)
  - `Pages/Admin/ScheduleItemTypes.razor` (1)
  - `Pages/Admin/Sponsors.razor` (2)
  - `Pages/Admin/Staff.razor` (2)
  - `Pages/Hub/HubSubNav.razor` (1)
  - `Pages/Hub/Incidents.razor` (1)
  - `Pages/Hub/Info.razor` (5)
  - `Pages/Hub/Schedule.razor` (6)
  - `Pages/Hub/Sponsors.razor` (1)
  - `Pages/Hub/Staff.razor` (3)
- `SeedService.cs` keeps using the literal `Id.EventCcn2026` GUID — that's seeding,
  not runtime resolution, and must stay stable for idempotent seed upserts.

**Rollout order:** convert one page at a time; after each, verify CCN 2026 (still the
only event, still active) renders identically. Only create the Men's Retreat event row
once all 12 files are converted and verified — this avoids a half-migrated state where
some pages resolve dynamically and others are still pinned to CCN 2026.

**Risk:** highest blast radius in this release — touches nearly every page in the app.
Mitigated by the page-at-a-time rollout and the fact that CCN 2026 stays the only
event (and stays active) until the wiring is proven out.

---

## 2. Capability gating

**Goal:** Vicki can turn off Men's Retreat's game/scoring features from the admin UI;
hidden features are unreachable, not just hidden from nav.

- Add two missing capabilities to the `Feature` enum + `SeedCapabilitiesAsync`:
  `BowserEvent`, `Awards`. Link both to CCN 2026 in `SeedEventCapabilitiesAsync`
  (all-on, matching CCN's existing 5 capabilities) so camp's historical data/behavior
  is unaffected.
- New `ICapabilityService.IsEnabledAsync(Guid eventId, Feature feature)`, cached like
  the existing schedule/announcement pattern.
- Gate two layers for each of the 5 scoring-related capabilities (`BoardGame`,
  `CoinShop`, `MiniGameSpinner`, `BowserEvent`, `Awards`):
  - **Nav:** `AppNav.razor` dropdown entries only render if the capability is enabled
    for the active event.
  - **Pages:** the underlying pages (`/board`, `/minigames`, `/admin/games`, etc.)
    check the capability on load and redirect/show a friendly "not available for this
    event" state if disabled — closes the URL-guessing gap that nav-only hiding would
    leave open.
- Admin toggle UI: a new "Capabilities" section added to the existing
  `/admin/events` edit form (checkboxes per capability) — not a separate
  `/admin/capabilities` page. Keeps event configuration in one place.
- Men's Retreat 2026 ships with all 5 scoring capabilities off by default; only
  `Announcements` and `Itinerary` stay on.

---

## 3. Per-event theme

**Goal:** Men's Retreat gets a visually distinct, simple theme without touching
Mario Party's CCN 2026 styling.

- Populate `Theme.ColorPalette` (currently null on the one seeded row) for a new
  "Men's Retreat 2026" `Theme` row, linked via a new `Event.ThemeId`.
- Palette direction: deep forest green + warm wood/tan tones — distinct from Mario
  Party's cream/black neo-brutalist look, but reusing the same panel/border/shadow
  system (no new component work needed, just different CSS variable values).
- `ThemeService` changes from `AddSingleton` to request/circuit-scoped, resolving via
  `IActiveEventService` → `Event.ThemeId` → `Theme.ColorPalette` (parsed JSON) →
  existing `ThemeConfig` CSS var shape.
- Fallback: if a `Theme` row's `ColorPalette` is null or fails to parse, fall back to
  the current hardcoded Mario Party `ThemeConfig`. This keeps CCN 2026 working
  unchanged even though its `Theme` row has no `ColorPalette` set today — no seed
  migration required for the existing event.
- No logo upload in this pass — text/typography-only theme. Explicitly deferred:
  `/admin/theme` self-service UI (color picker, logo upload) before Camp Harvest 2026.

---

## 4. Extended event management

**Goal:** Reduce setup effort for Vicki/Amanda when standing up a new event.

- `/admin/events`: add a "Duplicate from…" dropdown, shown when creating a new event.
- Cloning copies:
  - `EventScheduleItemType` rows (which schedule item types are enabled)
  - `EventCapability` rows (which capabilities are enabled)
- Cloning explicitly does **not** copy `Group`, `Location`, `Activity`, or
  `ScheduleItem` rows — these are event-specific in practice (Men's Retreat has no
  competition groups, different venue locations, different activities) and copying
  them would create more cleanup work than starting fresh.
- Groups/Locations/Schedule for the new event are still set up through the existing
  `/admin/groups`, `/admin/locations`, `/admin/schedule` pages — now correctly scoped
  to whichever event is active, once sub-project 1 lands.

---

## 5. Guest QR/event-code access

**Goal:** Attendees scan a QR code or type a code from a flyer, land on read-only
Schedule + Announcements, no login required, access expires after the event.

- New `Event.GuestCode` column (`string?`, unique) — admin sets it directly as a new
  field on the existing `/admin/events` form (e.g. `MENSRETREAT26`). Admin-chosen, not
  auto-generated, so it's memorable/printable.
- New `/join` page — manual code entry (text input + submit), for people who have the
  code but didn't scan.
- New `/join/{code}` route — what the QR encodes directly; validates and redirects
  automatically on load, no typing needed.
- QR image generation: server-side via the `QRCoder` NuGet package, rendering a PNG/SVG
  that encodes the full `/join/{code}` URL. No third-party QR API call — keeps this
  entirely self-hosted. Exposed as a "Generate QR" action on `/admin/events`.
- On valid code: issues a second cookie authentication scheme (`GuestScheme`,
  registered alongside the existing staff cookie scheme in `Program.cs`) carrying an
  `EventId` claim. Expires at `Event.ExpDate + 1 day`. A guest and a staff member can
  be authenticated simultaneously in different sessions — the two schemes don't
  conflict.
- Invalid/expired code: friendly error message. No rate-limiting or lockout — this is
  a low-stakes flyer code for a small trusted in-person audience, not a public signup
  flow.
- Guest-facing pages: `/hub/schedule` and `/hub/announcements` accept either auth
  scheme (`[Authorize(AuthenticationSchemes = "Cookie,GuestScheme")]`), but hide all
  admin/staff affordances (edit buttons, reactions, incident-report FAB) when the
  authenticated principal is a guest.
- Guest nav: minimal — just Schedule and Announcements, no admin chrome, no login
  prompt. New lightweight layout (or a guest-mode flag on the existing layout,
  decided during implementation planning).
- Out of scope for this release (matches the original v2.1.0 "Anonymous guest" tier,
  not the fuller "Named guest"/"Member" tiers): no name capture, no push
  notifications, no session signups, no check-in/attendance tracking.

---

## Testing / Rollout

- Sub-project 1 is the riskiest change (12 files, every page touched) — convert and
  verify page-by-page before creating the Men's Retreat event row.
- Build order matches the dependency chain: 1 → 2 → 3 → 4 → 5. Each can merge to
  `dev` independently; Men's Retreat can go live as soon as 1–3 (and ideally 5) are in,
  even if 4 (duplication) slips.
- No automated test suite exists yet for this project (per CLAUDE.md, that's a
  v1.2.0 goal) — verification is manual: confirm CCN 2026's historical
  pages/data are unaffected after each sub-project, and manually walk the Men's
  Retreat admin setup + guest join flow end-to-end before the retreat.
