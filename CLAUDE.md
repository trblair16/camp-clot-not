# Camp Clot Not — Claude Code Briefing

## What This Is
A Blazor Server (.NET 8) web app for Camp Clot Not (CCN), a camp for kids with bleeding disorders run by HBDA (Alabama chapter). The 2026 theme is Super Mario Party. The platform is being built to run camp scoring/competition for June 20-25, 2026, with a longer-term goal of replacing the chapter's Yapp subscription (~$1,600/year) for all chapter events.

**Primary spec:** `REQUIREMENTS.md`  
**Schema redesign spec:** `docs/superpowers/specs/2026-04-28-schema-redesign.md`

---

## Current State (as of 2026-06-04)

**Active branch:** `feature/124-v057-info-overhaul-ux-polish` — v0.5.7 in progress (final v0.5.x)
**On dev (pending main PR):** v0.5.6 — table-driven schedule item types + UX polish
**Released to main:** v0.5.5 — photos, sponsor enhancements, schedule improvements, dashboard
**Next:** v0.5.7 (current branch) → then v1.0.0-rc.1 (see versioning below)

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
- These are the activities Vicki/Amanda can configure: "Mushroom Kingdom Trivia Showdown", "Yoshi Egg Rescue Relay", etc.

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
- Dashboard landing page at `/dashboard`: Sponsors widget (prominent per Vicki), today's schedule, latest announcement, quick nav; `Index.razor` redirects here instead of `/hub/schedule`.

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

**v0.5.7 — Planned:**

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

**v1.0.0-rc.1 — Planned (next branch after v0.5.7 merges):**

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

---

**v2.0.0 Vision — Self-Service Event Designer**

*Goal: Vicki can configure and launch a "Men's Retreat" (or any HBDA event) entirely within the admin UI without any developer involvement. No seed changes, no code deploys, no Tyler.*

*What's already there (building blocks exist):*
- `/admin/groups` — group CRUD, event-scoped ✓
- `/admin/locations` — location CRUD ✓
- `/admin/schedule-item-types` — type CRUD + per-event enable/disable ✓
- `/admin/activities` — activity CRUD ✓
- `/admin/schedule` — schedule item CRUD ✓
- `Event` entity has `EffDate`, `ExpDate`, `IsActive`, `EventTypeId`, `ThemeId` ✓
- `EventCapability` join table exists (feature flags per event) ✓

*What needs to be built:*
- **`/admin/events` — Event CRUD**: create, edit, duplicate, set active event; replaces hardcoded `SeedService.Id.EventCcn2026` references with a runtime-selected active event
- **Event duplication**: "Copy this event as a starting point" — clones groups, locations, schedule item types, and activities into a new event shell so Vicki isn't building from scratch
- **`/admin/theme` — Theme config UI**: edit event name/tagline, primary color palette, logo upload; backed by DB `Theme` entity instead of `ThemeService` hardcoded values
- **`/admin/capabilities` — Feature flags UI**: toggle which platform features (`EventCapability`) are active for a given event (e.g. a Men's Retreat might not use the board game)
- **Decouple from hardcoded seed GUIDs**: `SeedService.Id.*` constants and hardcoded `SeedService.Id.EventCcn2026` references throughout pages/services need to become runtime lookups (e.g. `IEventContext.ActiveEventId`) so any event can be "the active one"
- **Admin completion checklist**: dashboard widget on `/admin` showing setup progress — "0 locations added," "no schedule items yet," etc.

*UX — Event Designer subnav under Admin:*

```
Admin → Event Designer
  ├── Events          /admin/events
  ├── Theme           /admin/theme
  ├── Groups          /admin/groups        (already exists, fold in)
  ├── Locations       /admin/locations     (already exists, fold in)
  ├── Schedule Types  /admin/schedule-item-types  (already exists, fold in)
  ├── Activities      /admin/activities    (already exists, fold in)
  └── Capabilities    /admin/capabilities
```

Existing standalone admin pages fold into this subnav rather than being replaced — the URLs stay the same, the navigation wrapper changes. A completion checklist at the top of the Event Designer landing page shows Vicki what's still missing for the active event.

*Schedule templates:*
- **`ScheduleTemplate`** entity — name, description, optional `EventTypeId` scope (e.g. "3-Day Camp," "Weekend Retreat," "Men's Retreat")
- **`ScheduleTemplateItem`** entity — FK to template, `DayOffset (int)` (0 = first day of event), `StartTime (TimeOnly)`, `EndTime (TimeOnly?)`, `ScheduleItemTypeId`, `Title`, `Notes?` — no absolute dates, all relative
- **Apply template** action on `/admin/events`: select a template → system generates `ScheduleItem` rows by adding `Event.EffDate + DayOffset` to each item's times → admin gets a fully populated schedule as a starting point to customize
- **Template CRUD** under Event Designer subnav (`/admin/schedule-templates`) — Admin can build, edit, and share templates across events
- **Save current schedule as template**: on any event, "Save schedule as template" action strips the absolute dates back to relative offsets and creates a new template — natural workflow after the first time you run a given event type
- Templates slot into Event Designer as an optional step between event creation and schedule editing: "Start from template?" → pick one → schedule pre-populated

*This is the primary driver for the 1→2 major version bump.* The shift is from "Tyler configures events in code" to "Vicki configures events in the UI." Once this ships, the platform is genuinely self-service for any HBDA event.

---

## Pitfalls to Avoid (Lessons from v0.4.0)

**1. Never use PowerShell for source file text replacement.**  
PowerShell 5.1's `Get-Content` reads UTF-8-without-BOM files using the system code page (CP1252 on US Windows), corrupting multi-byte characters like emoji. Always use the **Edit tool** for replacements in source files. If you must use PowerShell, use `[System.IO.File]::ReadAllText(path, [System.Text.Encoding]::UTF8)` and `[System.IO.File]::WriteAllText(path, content, [System.Text.Encoding]::UTF8)` explicitly.

**2. Hub rename requires two separate changes.**  
Renaming a SignalR hub class (e.g., `CampHub` → `LiveHub`) and renaming its route (e.g., `/camphub` → `/livehub`) are independent changes. A `replace_all` on the class name will not touch the route string literal. Check and update both explicitly, and grep for both the old class name AND the old route string before closing.

**3. Login page layout — use `LoginLayout`, not `MainLayout`.**  
The login page must use `LoginLayout.razor` (bare layout: ThemeHead only, no nav). Using `MainLayout` puts a 72-80px nav header above the page content, which breaks any `height:100vh` centering attempt. `LoginLayout` must NOT include `MudDialogProvider` or `MudSnackbarProvider` — they render DOM elements that can interfere with positioning.

**4. Centering in Blazor Server — use `position:fixed` on the element itself.**  
Do not rely on `height:100vh` + flex centering on a wrapper inside a Blazor layout. Blazor's component tree and MudBlazor providers introduce DOM elements that break this. For the login card: `position:fixed;top:50%;left:50%;transform:translate(-50%,-50%)` directly on the card element is the only reliable approach. For the logo above it, use a separate `position:fixed` element with `bottom:calc(50% + Npx)`.

**5. `RedirectToLogin` — use `IHttpContextAccessor`, not `Nav.NavigateTo(forceLoad:true)`.**  
`Nav.NavigateTo(forceLoad:true)` during Blazor Server prerendering throws `NavigationException`. The dev exception page catches this before Blazor can process it as a redirect. Fix: check `IHttpContextAccessor.HttpContext` — if non-null and response not started, use `Response.Redirect("/login")` directly. Fall back to `Nav.NavigateTo` for the interactive circuit (where `HttpContext` is null). `IHttpContextAccessor` is already registered in `Program.cs`.

**6. Read the request before touching anything.**  
If asked to "make cells more visible against the cream background," that means style the cells — not replace the entire page background. Do not make sweeping changes beyond the stated scope.

**7. Image background removal requires flood fill, not crop.**    
Python Pillow `img.getbbox()` + `crop()` only removes surrounding whitespace — it does not make the background transparent. Use a flood fill from all four edges with a colour tolerance to make the actual background pixels transparent. See the Python script used for `ccn-logo-2026.png` for the pattern.

**8. The `Display` role is gone — do not re-add it.**  
`/board/display` and `/minigames/display` are `[Authorize(Roles = "Admin")]` only. The `Display` enum value, `ViewDisplay` permission, and `RoleDisplay` seed GUID have all been removed in v0.5.0. Projector pages are Tyler's laptop only — no separate role is needed.

**9. `Location` is a proper entity, not a display string.**  
`ScheduleEvent` has a `LocationId` FK to the `Location` table. The old `LocationDisplayName` string column was dropped in migration `20260523234951_AddLocation`. Admins must create locations at `/admin/locations` before they can be assigned to schedule events or group overrides.

**10. Worktrees do not inherit `appsettings.Development.json`.**  
This file is gitignored and won't exist in a new worktree. EF migrations (`dotnet ef migrations add`) fail at design-time with "No database connection string found." Fix: copy from the main repo before running any EF commands in a worktree: `Copy-Item "..\..\CampClotNot\appsettings.Development.json" ".\CampClotNot\"`. Also set `$env:ASPNETCORE_ENVIRONMENT = "Development"` in the same PowerShell session.

**11. FAB button positioning — always use `ccn-fab-mobile` class.**  
Floating action buttons (like "Log Score" and "Report Incident") must use the class `ccn-fab-mobile` with `position:fixed;bottom:24px;right:24px`. The global `ThemeHead.razor` media query `@media (max-width:768px) { .ccn-fab-mobile { bottom: 80px !important; } }` handles bottom-nav clearance on mobile automatically. Do NOT hardcode `calc(80px + env(safe-area-inset-bottom))` — that is always elevated and misses the desktop position.

**12. Modal dialogs — always use the `fadeIn`/`popIn` pattern from `LogTransactionDialog`.**  
**13. Navigation property names must follow EF Core FK convention or be configured explicitly.**  
If a navigation property `FooUser` references `User` but the FK property is not named `FooUserUserId` or `UserId`, EF Core creates a shadow property (e.g. `FooUserUserId`) instead of using your named property. The shadow property defaults to `Guid.Empty` on insert, causing a NOT NULL FK constraint violation that crashes the Blazor circuit. Fix: add explicit `.HasForeignKey(e => e.YourProperty)` in `OnModelCreating` whenever the FK property name doesn't match the `<NavName><PKName>` convention. This was the root cause of the v0.5.4 schedule save bug.  
All form modals must use: backdrop `animation:fadeIn .2s ease` with `rgba(26,26,26,.65)`, panel `ccn-panel` class with `animation:popIn .25s ease` and `box-shadow:8px 8px 0 var(--black)`, and centered with `display:flex;align-items:center;justify-content:center;padding:20px`. Do not use bottom-sheet patterns for forms — they lack the popIn animation and feel inconsistent. A shared `CcnDialog.razor` wrapper is planned post-v0.5.1 once a third dialog exists.

**14. Always use synchronous `DbFactory.CreateDbContext()` — never the async variant.**  
`await using var db = await DbFactory.CreateDbContextAsync()` introduces an async disposal pattern that conflicts with how services are structured in this codebase. Use `using var db = DbFactory.CreateDbContext()` (synchronous) everywhere. The async variant caused a silent bug on `Dashboard.razor` where the first-day schedule query returned nothing despite data existing. All existing services use the synchronous call — match it.

**15. Razor attribute quote conflict: use single quotes on `@onclick` when the lambda contains `$"..."` interpolated strings.**  
If an `@onclick` (or other `@on*`) lambda contains a C# interpolated string literal, using double quotes for the HTML attribute causes parse errors (CS1525/CS1056). Wrap the outer attribute in single quotes instead:  
`@onclick='() => _field = $"/path/{someId}"'`  
This also applies to any event attribute whose lambda body contains a double-quoted string literal. Root cause of the lightbox onclick bug in `/admin/locations`.

---

## UI Design Direction — Mario Party Reference

Reviewed actual Mario Party Superstars / Mario Party 9 screenshots 2026-05-07. Reference images at `References/` (local only, not committed).

**The correct aesthetic:**
- **Background:** Cream (`#F2ECD8`) dotted pattern for all main app pages. Deep purple-violet only for the board/display projector pages.
- **Leaderboard layout:** Full-width horizontal ROWS, not a grid of cards.
- **Portraits:** Small square with colored border — not a circle.
- **Panels:** Opaque solid-colored rectangles with thick black borders and 3-4px offset box shadow (neo-brutalist). No blur, no translucency.
- **Typography:** Fredoka One for headings/labels. Nunito for body.
- **Score display:** `ccn-coin.png` / `ccn-star.png` image assets — not emoji.

---

## Architecture — Critical Points

- **Railway.app hosting** — PORT env var is injected, never hardcode. postgres:// URI converted in Program.cs. HTTPS terminated externally — no ForceHttpsRedirection in prod.
- **Blazor Server + SignalR** — projector displays receive real-time updates via `LiveHub` at `/livehub`. Block hit and mini-game animations MUST play on the projector via SignalR broadcast triggered from admin tablet. Hub events defined in `Hubs/LiveHub.cs`.
- **No Flask middleware in v1** — service → repository → EF Core → PostgreSQL.
- **Append-only transactions** — coins/stars never deleted, only voided. Totals always computed from non-voided transactions, never stored.
- **Reinstate clears void fields (known trade-off)** — `ReinstateAsync` nulls out `VoidedAt`/`VoidedBy`. If full void history is ever needed, add `ReinstatedAt`/`ReinstatedBy` to `Transaction` and a new migration.
- **Pre-scripted games** — block hit and mini-game spinner are NOT random. Pre-scripted by admin before camp via `/admin/games`.
- **Auth** — BCrypt/cookie for v1. Login uses native HTML form POST to `/account/login` (cookie can't be set from SignalR circuit).
- **3-way split pattern** — all interactive game features follow: trigger page (admin tablet) → projector display page (Admin only, opened in separate tab) → admin scripting page under `/admin/games`. Board game: `/board`, `/board/display`. Mini-game: `/minigames`, `/minigames/display`.

---

## Groups (CCN 2026 — Confirmed)

| ID | Name | Short | Color | Logo |
|---|---|---|---|---|
| Group1 | Mini Marios | MM | #E74C3C (red) | mini-marios-logo.png |
| Group2 | Blue Shell Bandits | BB | #2980B9 (blue) | blue-shell-bandits-logo.png |
| Group3 | Mushroom Militia | MU | #27AE60 (green) | mushroom-militia-logo.png |
| Group4 | Luma Legends | LL | #8E44AD (purple) | luma-legends-logo.png |

Groups 5 & 6 removed from seed. SeedGroupsAsync upserts by ID and purges stale entries on restart.

---

## Schema

| Concept | Tables |
|---|---|
| Event scoping | `EventType`, `Event`, `Theme` |
| Feature flags | `Capability`, `EventCapability` |
| Activities | `ActivityTypeCategory`, `ActivityType`, `Activity` |
| Competition | `CurrencyType`, `Group`, `Transaction`, `BoardSpace`, `GroupBoardPos`, `ScriptedBlockHit`, `ScriptedMiniGame` |
| Awards | `AwardType`, `CamperAward` |
| Auth/RBAC | `UserRole`, `Authority`, `UserRoleAuthorityLink`, `UserAuthorityLink`, `User` |
| Hub (Camp Info) | `Location`, `InfoPage`, `StaffMember`, `Announcement`, `ScheduleItem`, `ScheduleItemType`, `EventScheduleItemType`, `ScheduleItemGroup`, `IncidentReport`, `Sponsor` |

**Column convention:** `Name` + `Description` + `SystemName` on all reference/catalog tables.

**Enum naming (Option C):** `CurrencyType`→`Currency`, `AwardType`→`AwardKind`, `UserRole`→`Role`, `Capability`→`Feature`, `Authority`→`Permission`.

**Seed IDs:** Stable hardcoded GUIDs in `SeedService.Id`. CCN 2026 EventId = `00000009-0009-0009-0009-000000000001`.

**SpaceType is NOT an enum** — board space type comes from `ActivityTypeCategory.SystemName` via the `BoardSpace → Activity → ActivityType → Category` chain.

---

## Branching & Versioning

```
main              — stable releases. Protected. Tagged at each version.
dev               — integration branch (merge features here first)
feature/N-name    — feature branches off dev (N = GitHub issue number, open issue first)
```

**Release flow:** `feature/*` → PR to `dev` → PR to `main` → tag  
**Issue-first rule:** Open a GitHub issue before creating a branch. Branch name must use the issue number GitHub assigns.  
**`gh` CLI:** Installed at `$env:LOCALAPPDATA\Programs\gh\gh.exe`. Use PowerShell (not Bash) to invoke it.

### Version Convention (updated 2026-06-04)

v0.5.7 is the **last v0.5.x release**. With production live and real data being entered, the project moves to a release-candidate convention:

| Version | Meaning |
|---|---|
| `v1.0.0-rc.1` | First RC — reconnect UX + dry run prep |
| `v1.0.0-rc.2`, `rc.3` … | Dry run fixes (June ~14-19) |
| `v1.0.0` | Go-live build — deployed before June 20 camp start |
| `v1.0.1`, `v1.0.2` … | Hotfixes during/after camp |
| `v1.1.0` | Next feature cycle (post-camp chapter Yapp replacement work) |

Branch names follow the same pattern: `feature/N-v100rc1-...`, `feature/N-v100-...`, etc.

---

## Key Files

| File | Purpose |
|---|---|
| `REQUIREMENTS.md` | Full functional spec, animation spec, infrastructure, decisions |
| `CampClotNot/Hubs/LiveHub.cs` | SignalR hub at `/livehub` — all real-time events |
| `CampClotNot/Services/BoardService.cs` | Board logic + block hit SignalR broadcasts |
| `CampClotNot/Services/MiniGameService.cs` | Mini-game logic + spinner SignalR broadcasts |
| `CampClotNot/Services/SeedService.cs` | Startup seed — all reference data + CCN 2026 event |
| `CampClotNot/Services/ThemeService.cs` | CSS variable tokens — `ThemeConfig.CssVariables` injected via `ThemeHead.razor` |
| `CampClotNot/Shared/ThemeHead.razor` | Injects CSS vars into `:root`, defines body styles, shared animations |
| `CampClotNot/Shared/LoginLayout.razor` | Bare layout for login page — ThemeHead only, no nav |
| `CampClotNot/Shared/PrintLayout.razor` | Bare layout for print views — ThemeHead only, no nav, no MudBlazor providers |
| `CampClotNot/Pages/Login.razor` | Login page — uses `LoginLayout`, `position:fixed` centering |
| `CampClotNot/Pages/Board.razor` | Board game trigger — Admin + Staff |
| `CampClotNot/Pages/BoardDisplay.razor` | Board projector display at `/board/display` — Admin + Display |
| `CampClotNot/Pages/MiniGames.razor` | Mini-game trigger at `/minigames` — Admin only |
| `CampClotNot/Pages/MiniGamesDisplay.razor` | Mini-game projector at `/minigames/display` — Admin only |
| `CampClotNot/Pages/Admin/Games.razor` | Combined game admin at `/admin/games` |
| `CampClotNot/Pages/Admin/Sponsors.razor` | Sponsor CRUD at `/admin/sponsors` |
| `CampClotNot/Pages/Admin/Schedule.razor` | Schedule item CRUD at `/admin/schedule` — Admin only |
| `CampClotNot/Pages/Admin/ScheduleItemTypes.razor` | Schedule item type CRUD at `/admin/schedule-item-types` — Admin only |
| `CampClotNot/Services/ScheduleService.cs` | Schedule item CRUD — uses `ScheduleItemDto`, includes `ScheduleItemType` in all queries |
| `CampClotNot/Services/ScheduleItemTypeService.cs` | ScheduleItemType CRUD + `GetForEventAsync(eventId)` for dropdowns |
| `CampClotNot/Pages/Admin/Activities.razor` | MinuteToWinIt activity CRUD at `/admin/activities` — Admin only |
| `CampClotNot/Pages/Hub/HubSubNav.razor` | Shared Hub sub-nav + floating Report Incident FAB + modal |
| `CampClotNot/Pages/Hub/Incidents.razor` | Admin incident list at `/hub/incidents` |
| `CampClotNot/Pages/Hub/IncidentPrint.razor` | Print view at `/hub/incidents/{id}/print` — uses `PrintLayout` |
| `CampClotNot/Pages/Hub/Sponsors.razor` | Public sponsor grid at `/hub/sponsors` |
| `CampClotNot/Services/IncidentReportService.cs` | Submit, list, acknowledge incident reports |
| `CampClotNot/Services/SponsorService.cs` | Sponsor CRUD scoped to CCN 2026 event |
| `CampClotNot/appsettings.Development.json` | Local DB + seed credentials (gitignored — must copy to worktrees manually) |

---

## Local Dev Setup

- **DB:** PostgreSQL local, database `hbda_dev`
- **Connection string:** `CampClotNot/appsettings.Development.json` (gitignored)
- **Run migrations (local):** `dotnet ef database update` from `CampClotNot/`
- **Start app:** `dotnet run` from `CampClotNot/` or F5 in Visual Studio
- **Login (dev):** `tyler@hbda.local` / `DevAdmin1!` (seeded from appsettings.Development.json)
- **gh CLI:** `& "$env:LOCALAPPDATA\Programs\gh\gh.exe" <command>` from PowerShell

## Running Migrations on Production (Railway)

Railway's internal DB URL does not resolve outside Railway's network. Use the `--connection` flag with the **public** TCP proxy URL from Railway → Postgres service → Settings → Public Networking.

```powershell
dotnet ef database update `
  --project "C:\Users\TRBla\source\repos\camp-clot-not\CampClotNot" `
  --connection 'Host=zephyr.proxy.rlwy.net;Port=13245;Database=railway;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true'
```

**Notes:**
- Use **single quotes** around the connection string — prevents PowerShell from expanding `$` characters in the password
- `Port=13245` is the Railway TCP proxy port (not 5432 — Railway never uses the default port publicly)
- `Host=zephyr.proxy.rlwy.net` — get the current host/port from Railway → Postgres → Settings → Public Networking if this changes
- Do NOT set `DATABASE_URL` or `ASPNETCORE_ENVIRONMENT` env vars — the `--connection` flag bypasses Program.cs entirely
- Successful output ends with: `Applying migration 'XXXXXX_MigrationName'` then `Done`
