# Release History

Every production release of the HBDA Events platform (originally "Camp Clot Not Score Tracker"). Current status and roadmap are in `CLAUDE.md`. Each feature's design is in `docs/superpowers/specs/`.

## Releases

Each release is an annotated tag on `main`. Tags `v1.0.0` through `v1.1.1` were added after the fact on 2026-10-08 and point at the merge commits listed here.

| Tag | Date | `main` commit | What |
|---|---|---|---|
| `v0.5.5` | 2026-06-03 | | Staff photos, sponsor enhancements, schedule improvements, dashboard landing page |
| `v0.5.6` | 2026-06-04 | | Table-driven `ScheduleItemType`, `ScheduleEvent` → `ScheduleItem`, `LocationOther`, UX polish |
| `v0.5.7` | 2026-06-05 | | Info section overhaul, PDF uploads, schedule table, UX polish round 2, auth fixes |
| `v1.0.0` | 2026-06-19 | `411934c` (PR #162) | **CCN 2026 go-live.** Reconnect UX overhaul, plus fixes from the `v1.0.0-rc.N` dry runs (RC numbers appear only in PR titles such as #155) |
| `v1.0.1` | 2026-06-24 | `cb7ba7d` (PR #296) | **End of CCN 2026 camp week.** Rolls up 67 hotfix and feature merges (PRs #164–#296) shipped live during camp: push notifications and persistent sessions, the Bowser event, announcement reactions, the first `/admin/events` page, staff directory CRUD, performance work (WebP, compression, cache headers), and hotfixes |
| `v1.1.0` | 2026-07-24 | `3d96dcf` (PR #299) | **Men's Retreat enablement** (#297): active-event wiring, capability gating, per-event theme, guest QR/event-code access (#298) |
| `v1.1.1` | 2026-07-25 | `62d6cf0` (PR #301) | Mobile header fix (#300), plus versioned manifest/icon URLs so Android picks up new branding (#301) |
| *next* | — | — | **Camp Harvest release**, merged from `dev`: see "Current State" in `CLAUDE.md` |

**Camp week (June 19–25) didn't follow the RC convention.** Fixes went `dev` → `main` one PR at a time, often minutes apart, without per-fix tags. `v1.0.1` marks the state at the end of the week rather than one hotfix.

---

## CCN 2026 build-out (v0.1.0 → v1.0.0)

*Kept for reference. Detailed notes from the pre-camp build.*

**RC Versioning Convention (2026-06-05 → go-live):**
- Format: `v1.0.0-rc.N` — increment N for every fix/tweak before camp goes live
- Branch naming: `feature/N-v100rc1-description`, `feature/N-v100rc2-description`, etc. (open issue first)
- **True v1.0.0 go-live date: June 19, 2026** (one day before camp June 20)
- After go-live, any camp-week hotfixes are `v1.0.1`, `v1.0.2`, etc.

**v0.1.0 — Done:**
- Blazor Server project: entities, repositories, services, SignalR hub, MudBlazor pages
- Schema redesign: 23 entity classes, Option C enum naming (`Currency`, `AwardKind`, `Role`, `Feature`, `Permission`), 22-table PostgreSQL schema
- `AddDbContextFactory` pattern (Blazor Server concurrency fix)
- BCrypt/cookie auth: login via native POST to `/account/login`, 24-hour sliding sessions
- RBAC: role + per-user authority claims loaded into session at login
- User management: create, deactivate, reset password (`/admin/users`)
- Group management: create, edit, delete — scoped to CCN 2026 event (`/admin/groups`)
- Startup seed service: all reference data + CCN 2026 event + admin user (idempotent)
- Railway infrastructure: PORT env var, `postgres://` URI conversion, `/health` endpoint

**v0.3.0 — Done (Board & Block Hit):**
- SVG board with winding snake path, BoardComponent, BoardService
- Pre-scripted block hit — admin trigger via `/admin/board`, animation on `/board/display` via SignalR
- ThemeService + CSS variables foundation

**v0.3.1 — Done (UI Overhaul, PR #95):**
- Neo-brutalist redesign — cream bg (`#F2ECD8`), black borders, shadow offsets, rank rows
- ThemeService extended with flat palette CSS vars
- MainLayout: black header band, light MudBlazor theme
- Dashboard: rank rows replacing card grid; score pills with ccn-coin.png / ccn-star.png
- Groups confirmed: Mini Marios (MM, red), Blue Shell Bandits (BB, blue), Mushroom Militia (MU, green), Luma Legends (LL, purple) — 4 groups final

**v0.4.0 — Done (Mini-Game Spinner, feature/96):**
- `LiveHub` (renamed from `CampHub`) at `/livehub` — adds `MiniGameSpinTriggered`, `MiniGameSpinRevealed`, `MiniGameSpinReset` hub events
- `MiniGameService` — GetActivities, GetScripts, Upsert, TriggerSpin, Reveal, ResetDay
- `/admin/games` — combined Game Admin page (absorbs old `/admin/board`); tabs: Board Spaces, Block Hit Scripts, Mini-Game Scripts, Reset
- `/minigames` — admin trigger page; day selector, Start Spin → Reveal Result → log coins/stars
- `/minigames/display` — projector display page (Admin only); yellow cells → hand cycles green → lands green + pulses
- `/board/display` — renamed from `/display` (file: `BoardDisplay.razor`)
- Activities nav is now a dropdown: Board Game (`/board`) + Mini-Game (`/minigames`)
- `LoginLayout.razor` — bare layout for login page (no nav, no MudBlazor providers)
- Login page redesigned: `LoginLayout`, `position:fixed` centering, ccn-logo-2026.png (background made transparent via Python Pillow flood fill)
- `RedirectToLogin.razor` — fixed `NavigationException` with `IHttpContextAccessor`
- Two new MinuteToWinIt activities seeded: "Mushroom Kingdom Trivia Showdown", "Yoshi Egg Rescue Relay"
- `gh` CLI installed at `$env:LOCALAPPDATA\Programs\gh\gh.exe` (user PATH)

**v0.5.0 — Done (Camp Info Hub + PWA, feature/99):**
- `HubSubNav.razor` — shared sub-nav for all Hub pages (Schedule / Announcements / Staff / Info)
- `/hub/schedule` — day-grouped collapsible timeline; per-group Activity + Location + Note overrides via `ScheduleEventGroup`; today auto-expanded; Admin/Staff/Volunteer access
- `/hub/announcements` — priority feed (pinned → newest); urgent red badge; dismissible banner on Dashboard; pin/archive Admin-only
- `/hub/staff` — card grid; emoji avatars; tap-to-call/email links; "Import from Users" admin shortcut; event-scoped
- `/hub/info` — sidebar page list + Markdig markdown body; Admin inline edit; seeded slugs: `rules`, `faq`, `medical` (`schedule-overview` and `packing` removed in v0.5.1)
- `Location` entity + `/admin/locations` — full CRUD; FK referenced by `ScheduleEvent` and `ScheduleEventGroup`; replaces old `LocationDisplayName` string column
- `ScheduleEventGroup` extended beyond spec: per-group Activity, Location override, and Note fields
- `Volunteer` role finalized in `Role` enum; `Display` role removed — projector pages (`/board/display`, `/minigames/display`) are now Admin-only; `ViewDisplay` permission removed
- PWA fixes: `manifest.json` theme/background → `#F2ECD8`; service worker → network-first for navigation, cache-first for static assets; bumped to `ccn-shell-v2`
- `railway.toml` added with `/health` healthcheck, `ON_FAILURE` restart policy
- Railway production: project `camp-clot-not` live; Postgres running (SFO); env vars set (`DATABASE_URL`, `ASPNETCORE_ENVIRONMENT`, `Seed__AdminEmail`, `Seed__AdminPassword`)

**v0.5.1 — Done (feature/103-mobile-pwa-fixes, PR #104/105):**
- User edit dialog on `/admin/users`; PWA `short_name` → "CCN 2026"; `viewport-fit=cover`; desktop nav mobile suppression; bottom nav safe-area padding; Activities → Transactions swap in mobile nav (Admin); PWA icons regenerated from tall 3-row CCN logo; Children's Harbor logo extracted to `wwwroot/img/childrens-harbor-logo.png`
- `IncidentReport` entity + `IncidentReportService` — submit, list, acknowledge
- `Sponsor` entity + `SponsorService` — CRUD, ordered by SortOrder
- `PrintLayout.razor` — bare layout (ThemeHead only, no nav, no MudBlazor providers) for print views
- `/hub/incidents` — Admin-only incident list with Acknowledge button and Print link
- `/hub/incidents/{id}/print` — uses `PrintLayout`; mirrors Children's Harbor paper form
- `/hub/sponsors` — responsive logo tile grid; all roles
- `/admin/sponsors` — Admin CRUD; matches `/admin/locations` pattern
- `HubSubNav.razor` — Sponsors + Incidents (Admin-only) tabs added; floating 🚨 Report Incident FAB + modal
- `AppNav.razor` — `/admin/sponsors` in desktop Admin dropdown and mobile admin sheet
- `SeedService` — `schedule-overview` and `packing` removed from InfoPage seed (and from `SeedService.Id`)
- EF migration `AddIncidentReportAndSponsor` added and applied

**v0.5.2 — Done (feature/108, PR #110/111):**
- Sponsor logo upload: `Sponsor` entity extended with `LogoData (byte[]?)` + `LogoContentType (string?)`; logo stored in DB; `/admin/sponsors` serves logos via a streaming endpoint; display pages render `<img src="/sponsor-logo/{id}">` (or fall back to LogoUrl)
- Nav restructure: Admin dropdown order → Game Admin → Activities → Schedule → Groups → Users → Locations → Sponsors (desktop and mobile sheet)
- `/hub/schedule` day tabs: collapsible accordion replaced with horizontal day tabs; `_selectedDay` tracks active tab; `_campEvent` loaded for min/max date constraints; sets `_selectedDay` after save
- `/admin/schedule` (new page): Admin CRUD for `ScheduleEvent` — form panel + table pattern (same as Locations); date/time inputs use `value`+`@onchange` string pattern; group assignments collapsible section; calls `ScheduleService.UpsertAsync`
- `Index.razor` redirects `/` to `/hub/schedule` via `IHttpContextAccessor`; `Dashboard.razor` route changed from `@page "/"` to `@page "/dashboard"`
- EF migration `AddSponsorLogoUpload` applied

**v0.5.3 — Done (feature/112, PR #113):**
- `/admin/activities` (new page): Admin CRUD for MinuteToWinIt activities (Name + Description) — same form panel + table pattern; delete blocked if activity is referenced by a `ScriptedMiniGame`
- `MiniGameService.UpsertActivityAsync` and `DeleteActivityAsync` added
- These are the activities event admins can configure: "Mushroom Kingdom Trivia Showdown", "Yoshi Egg Rescue Relay", etc.

**v0.5.4 — Done (feature/114, PR #115/116):**
- Bug fix: schedule event save caused Blazor circuit crash (white bar + freeze)
- Root cause: `ScheduleEvent.CreatedByUser` navigation had no explicit FK config; EF Core created shadow property `CreatedByUserUserId`; on insert it defaulted to `Guid.Empty`, violating NOT NULL FK constraint
- Fix: `HasForeignKey(e => e.CreatedBy)` added to `OnModelCreating`
- Migration `FixScheduleEventCreatedByFK`: drops `CreatedByUserUserId` shadow column/index/FK; wires `CreatedBy` as the real FK to `Users.UserId`

**v0.5.5 — Done (feature/118, issue #118):**

*Entities / enums changed:*
- `Role` enum: add `MedicalStaff` (C# only — no schema change; stored via UserRole seed). Permissions: log transactions, view+acknowledge incident reports, all Hub features; no admin panel.
- `ScheduleEventType` enum: add `Presentation` (C# only). `Travel` keeps its code name but displays as "Arrival/Departure" everywhere in the UI.
- `IncidentReport`: add `IncidentLocationId (Guid? FK→Location)` + `IncidentLocationOther (string?)` + `ReportType (IncidentReportType enum: Internal/ChildrensHarbor)`. Incident form shows location dropdown from active event's locations + always-visible "Other" option (reveals free-text when selected). Admins can choose `ChildrensHarbor` type; all other roles default to `Internal`.
- `StaffMember`: add `PhotoData (byte[]?)` + `PhotoContentType (string?)`; served via `/staff-photo/{id}`; shows in `/hub/staff` card (falls back to emoji); uploadable in admin dialog.
- `ScheduleEvent`: add `PresenterName (string?)` + `PresenterBio (string?)`; ONLY shown/editable in admin form when `EventType == Presentation`; only displayed in `/hub/schedule` for Presentation events.
- `Sponsor`: add `ContactName (string?)` + `Phone (string?)`; tap-to-call `tel:` link on `/hub/sponsors`; drag-and-drop sort in `/admin/sponsors` (admin-only, saves `SortOrder` to DB, drives order for all users).
- `Location`: add `ImageData (byte[]?)` + `ImageContentType (string?)`; served via `/location-image/{id}`; upload in `/admin/locations` form.
- `Activity`: add `LocationId (Guid? FK→Location)`; optional location link set in `/admin/activities`; board space rendering (Board.razor, BoardDisplay.razor) uses location image when activity has a location with image.

*Other changes:*
- Remove Mini Marios group from seed → 3 groups: Blue Shell Bandits, Mushroom Militia, Luma Legends. (Prod DB note: purge will fail if Mini Marios has FK-referenced rows — delete transactions/board positions first.)
- 12-hour AM/PM time format everywhere in schedule display and admin.
- Dashboard landing page at `/dashboard`: Sponsors widget (prominent, per chapter request), today's schedule, latest announcement, quick nav; `Index.razor` redirects here instead of `/hub/schedule`.

**Migration:** `AddV055Enhancements` (`20260603051634`) — DONE. Applied to local dev DB. Adds columns to `IncidentReport` (IncidentLocationId FK, IncidentLocationOther, ReportType), `StaffMember` (PhotoData, PhotoContentType), `ScheduleEvent` (PresenterName, PresenterBio), `Sponsor` (ContactName, Phone), `Location` (ImageData, ImageContentType), `Activity` (LocationId FK).

**v0.5.5 progress as of 2026-06-03:**
- [x] All entity files updated
- [x] All enum additions (MedicalStaff in Role, IncidentReportType, Presentation in ScheduleEventType)
- [x] AppDbContext: explicit FK config for Activity.LocationId and IncidentReport.IncidentLocationId
- [x] SeedService: MedicalStaff UserRole + LogTransaction authority link; Mini Marios removed; board positions seed uses Group2/3/4
- [x] Migration AddV055Enhancements created and applied to dev DB

**v0.5.5 completed items:**
- [x] Staff photo: endpoint `GET /staff-photo/{id}` + InputFile upload in admin staff dialog + `<img>` in `/hub/staff` card (fallback to AvatarEmoji)
- [x] Sponsor: ContactName/Phone fields in `/admin/sponsors` form + tap-to-call display in `/hub/sponsors` tile
- [x] Sponsor drag-and-drop sort in `/admin/sponsors` (HTML5 drag events; saves SortOrder to DB via `SponsorService.UpdateSortOrderAsync`)
- [x] Incident form (`HubSubNav.razor` modal): location dropdown + "Other" free-text; Admin-only ReportType field
- [x] `/hub/incidents` list + print view: IncidentLocation column + ReportType badge; MedicalStaff role access
- [x] Schedule display (`/hub/schedule`): Travel → "Arrival/Departure"; Presentation type + PresenterName/Bio; 12-hr time format
- [x] Schedule admin (`/admin/schedule`): same Travel label + Presentation type + conditional presenter fields + 12-hr times
- [x] Location images: `GET /location-image/{id}` in Program.cs + InputFile upload in `/admin/locations` + board space shows location image when available
- [x] `/admin/activities`: Location dropdown (nullable FK to Location for board image purposes)
- [x] Dashboard (`/dashboard`): Sponsors widget, today's schedule, latest announcement, quick nav; `Index.razor` redirects here
- [x] `/hub/incidents` + `/hub/incidents/{id}/print`: `MedicalStaff` role added alongside `Admin`
- [x] `/admin/users` role dropdown: Medical Staff option added

**v0.5.6 — Done (feature/121-v056-schedule-item-types, PR #122, merged to dev):**

*Schema changes:*
- `ScheduleEvent` entity/table → `ScheduleItem`; `ScheduleEventGroup` → `ScheduleItemGroup`; `ScheduleEventType` enum removed
- New **`ScheduleItemType`** entity — `ScheduleItemTypeId`, `Name`, `SystemName`, `Description?`, `SortOrder`; seeded with 6 types (Activity, Meal, Travel, Free, Mandatory, Presentation)
- New **`EventScheduleItemType`** join table — which types are active per event; CCN 2026 gets all 6 enabled by default
- `ScheduleItem`: drop `EventType` enum column; add `ScheduleItemTypeId (Guid FK → ScheduleItemType)`; add `LocationOther (string?)` freetext alongside location dropdown
- `StaffMember`: add `PhotoObjectPosition (string?)` — stored as `"X% Y%"` string, drives `object-position` CSS on hub cards

*Service/page changes:*
- `ScheduleEventDto` → `ScheduleItemDto`; all `ScheduleService` queries include `ScheduleItemType`
- New `ScheduleItemTypeService` — CRUD + `GetForEventAsync(eventId)` for dropdowns
- New `/admin/schedule-item-types` page — Admin CRUD + per-event enable/disable
- Special-case UI: Presenter fields keyed off `SystemName == "Presentation"`; Travel → "Arrival/Departure" keyed off `SystemName == "Travel"`
- Admin nav: 4 section headers in Admin dropdown (Game / Schedule / People / Venue & Content); "Types" shorthand for schedule item types

*UX polish:*
- Tel: links use E.164 format (`+1XXXXXXXXXX`) via `TelHref()` helper — required for Android Chrome dialer; applied to Staff.razor + Sponsors.razor
- Staff phone/email links darkened to `#1a5fa8`
- Staff photo focal point: X/Y position sliders in admin dialog; `PhotoObjectPosition` stored as `"X% Y%"`; `object-position` CSS on hub cards
- Admin Locations: Capacity hidden from UI; location image preview in form; 56×40px thumbnails in table with fullscreen lightbox on click
- Password visibility toggle (👁/🙈) on all 3 password inputs in `/admin/users`
- Schedule item dividers darkened to `3px solid #3a3a3a`; location chip bold requires `font-family:'Fredoka One',cursive` (not just `font-weight`)
- Dashboard: fixed async DbContext bug (`CreateDbContext()` not `CreateDbContextAsync()`); 3-case date logic (before event → first day, during → today, after → last day)
- Hub/schedule: default tab = today (first day if before event, last day if event has ended)
- Admin Users: search input + role filter dropdown + sortable Name/Role columns; default sort = role rank then last name A-Z; `FilteredUsers` computed property
- LocationOther freetext always shown alongside location dropdown; displayed as separate chip in hub view

*EF migrations added:*
- `AddScheduleItemTypes` — table renames, new ScheduleItemType + EventScheduleItemType tables, drop EventType column, add ScheduleItemTypeId FK
- `AddStaffPhotoPosition` — adds `PhotoObjectPosition (text)` to `StaffMembers`
- `AddScheduleItemLocationOther` — adds `LocationOther (text)` to `ScheduleItems`

**v0.5.7 — Done (tag `v0.5.7`):**

*Info section overhaul:*
- Remove `faq` info page (redundant with schedule); `medical` page kept + restructured for emergency contacts
- Add PDF upload support to `InfoPage` — `PdfData (byte[]?)` + `PdfContentType (string?)`; served via `/hub/info/{slug}/pdf`; display via native PDF viewer (`<iframe>`) or new-tab link
- Role-based PDF visibility: per-page visibility flag controlling which roles can download
- Emergency contacts PDF on `medical` page — visible to MedicalStaff + Admin only

*Dashboard:*
- Sponsor widget currently caps at 8; use full available width/whitespace to show all sponsors

*Hub/sponsors (mobile):*
- Remove "Report Incident" FAB on `/hub/sponsors` on mobile only (FAB renders on top of content)

*Staff directory:*
- Fix email addresses overflowing card border (overflow-wrap or truncation with tooltip)
- Fix pixelated staff photos — review stored resolution vs. render size

*Schedule:*
- Show location image thumbnail on schedule item row in admin table (small, with lightbox expand)
- Rearrange schedule item admin layout — currently content squished to left
- Mobile: remove Edit/Delete buttons from `/hub/schedule` items (admin actions belong on admin page)

*Transactions:*
- Apply CCN neo-brutalist table style to transactions table (currently unstyled)

*Schedule item cells:*
- Make schedule item cell backgrounds opaque

**v0.5.7 is the last v0.5.x release.** All subsequent versions use the `v1.0.0-rc.N` → `v1.0.0` convention (see Branching & Versioning).

---

**v1.0.0-rc.1 → v1.0.0 — Done (go-live: PR #162 into `main`, 2026-06-19; RC numbers appear only in PR titles. `feature/160-v100-go-live` kept going during camp week, so its PRs #293/#295 landed later, in v1.0.1):**

*Reconnect UX overhaul (no migrations required — HTML/CSS/JS only):*
- Remove `ConnectionIndicator.razor` and `connection-indicator.js` — the dot never turns red because `invokeMethodAsync` can't call back into .NET when the SignalR circuit is down; a banner makes it redundant anyway
- Add custom `<div id="components-reconnect-modal">` to `_Layout.cshtml` with three styled states:
  - `.components-reconnect-show` — slim non-blocking banner at top ("📶 Reconnecting…"), page content stays visible
  - `.components-reconnect-failed` — red banner with Reload button + auto-reload countdown (~8s)
  - `.components-reconnect-rejected` — auto-reload after ~1.5s with brief "Session expired — reloading…" (most common Railway cause)
- Switch `blazor.server.js` to `autostart="false"` and configure `Blazor.start({ reconnectionOptions: { maxRetries: 12, retryIntervalMilliseconds: (n) => Math.min(n * 2000 + 1000, 20000) } })` for exponential backoff
- Add `visibilitychange` listener: if app was backgrounded (PWA) and circuit already failed, silently `location.reload()` when user returns — app just "opens fresh" with no error shown
- Remove `<ConnectionIndicator />` from `AppNav.razor`
- Style the banner to match CCN neo-brutalist design (`#F5C800` yellow background, black border, Fredoka One font)

*Dry Run prep:*
- End-to-end smoke test of all Hub features, game triggers, board display
- Validate MedicalStaff role access on incidents + info PDF
- Confirm all migrations applied to prod


### Version convention used during the RC phase (2026-06-04 → go-live)

v0.5.7 is the **last v0.5.x release**. With production live and real data being entered, the project moves to a release-candidate convention:

| Version | Meaning |
|---|---|
| `v1.0.0-rc.1` | First RC — reconnect UX + dry run prep |
| `v1.0.0-rc.2`, `rc.3` … | Dry run fixes (June ~14-19) |
| `v1.0.0` | Go-live build — deployed before June 20 camp start |
| `v1.0.1`, `v1.0.2` … | Hotfixes during/after camp |
| `v1.1.0` | Next feature cycle (post-camp chapter-wide event work) |

Branch names follow the same pattern: `feature/N-v100rc1-...`, `feature/N-v100-...`, etc.

