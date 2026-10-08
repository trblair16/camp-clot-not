# Camp Clot Not — Claude Code Briefing

## What This Is
A Blazor Server (.NET 8) web app for Camp Clot Not (CCN), a camp for kids with bleeding disorders run by HBDA (Alabama chapter). The 2026 theme is Super Mario Party. The platform is being built to run camp scoring/competition for June 20-25, 2026, with a longer-term goal of replacing the chapter's Yapp subscription (~$1,600/year) for all chapter events.

**Primary spec:** `REQUIREMENTS.md`  
**Schema redesign spec:** `docs/superpowers/specs/2026-04-28-schema-redesign.md`

---

## Current State (as of 2026-10-08)

The app is now a multi-event HBDA platform ("HBDA Events"). CCN 2026 (June 19–25) ran on v1.0.0/v1.0.1, and Men's Retreat 2026 (July) ran on v1.1.0. **The next event is Camp Harvest 2026 (mid-October).** Release tags and history: `docs/RELEASES.md`.

**`main` (production) = `v1.1.1`:** last updated 2026-07-25 (PR #301). It has the CCN go-live build plus v1.1.0 Men's Retreat enablement (#297): active-event wiring, capability gating, per-event theme, and guest QR/event-code access.

**`dev`: merged but not yet released to `main`.** This is the Camp Harvest release (87 commits ahead of `main`, 8 migrations):

| Issue / PR | Feature | Spec |
|---|---|---|
| #304 / #305 | Named guest identity: `GuestAttendee`, `GuestEventVisit`, `/admin/guests` | `2026-09-18-named-guest-identity-design.md` |
| #306 / #307 | Guest push notifications for announcements (Web Push, VAPID, `PushSubscription`) | `2026-09-21-guest-push-notifications-design.md` |
| #308 / #309 | Attendance: "I'm here" self check-in, `/admin/attendance` roster, walk-ins, CSV export | `2026-09-23-attendance-tracking-design.md` |
| #310 / #312 | `/admin/theme` editor (presets, colors, logo/banner upload, **one `Theme` row per event**) and "Copy setup from…" event duplication | `2026-09-23-admin-event-configurability-design.md` |
| #125 / #313 | Forgot password, Admin "Email reset link", and invite emails via Resend (`Services/Email/`) | `2026-09-23-password-reset-email-design.md` |
| #311 / #317 | Breakout slots, session sign-ups, per-event staff (`EventStaff`, `/admin/team` replaces `/admin/staff`), per-session QR check-in, and faster schedule setup (smarter form, .xlsx import, copy a past event's schedule) | `2026-09-26-breakout-slots-session-signups-design.md`, `2026-09-26-faster-schedule-setup-design.md` |
| #318 / #319, #320 / #321 | Theme-variable fixes (nav, reconnect banner, loading screen, auth pages), new app icon | — |
| #322 / #323 | Image uploads up to 20 MB with auto-resize (staff photos, sponsor logos, theme, groups) | `2026-10-05-larger-logo-uploads-design.md` |
| #324 / #325 | Guests land on the Dashboard, get a Home tab; bottom-nav highlight fix | — |
| #326 / #327–#333 | Mobile app-shell layout for the iOS PWA (fixed header and bottom bar, `.app-main` scroller, black status bar, content-hashed CSS/JS). Supersedes #153. See pitfalls #22–#23 | — |

**Releasing `dev` → `main`:** two migrations remove data the old code depends on (`AddEventStaff` drops `Users.GroupId`; `MergeStaffDirectoryIntoTeam` drops `StaffMembers` after copying it into `Users`/`EventStaff`). **Back up the Railway Postgres before merging.** Rolling back means restoring that backup and redeploying `v1.1.1`. Redeploying the old code alone won't work. Confirm `Vapid__*` is set on prod. After deploying, trim each event's team on `/admin/team`: the `AddEventStaff` backfill adds every active user to every existing event.

**Before Camp Harvest:** verify on staging (including a real Resend email once `Email__*` is set), back up prod, open the `dev` → `main` release PR, merge and tag it (`v1.2.0`), smoke test production, then set up the Camp Harvest event (theme, logo, guest code, team).

**Open issues:** #315 (Cloudflare Turnstile on login and forgot-password) is the only real backlog item. #311, #314, #318, #320, #324, and #326 are merged into `dev` but still open (see "Closing issues" below). #153 is superseded by #326. Planned-but-unstarted items are in the Roadmap below.

**How work is done here:** every feature has a design spec in `docs/superpowers/specs/` and an implementation plan in `docs/superpowers/plans/`. Read the relevant spec before changing a feature. New work follows the same process: ask the design questions first, write a spec with a "Design Decisions" table, write a plan, implement in logical commits, then open a PR into `dev` with a manual test checklist.

---

## Release History

Tags, dates, and the full v0.1.0 → v1.0.0 build log (the per-version notes that used to live here) are in **`docs/RELEASES.md`**.

---

## Roadmap (status as of 2026-10-08)

✅ = shipped (on `dev` or `main`), 🚧 = in progress, ⬜ = not started. The version labels below are the original plan, not release numbers. Actual releases didn't follow them (v1.1.0 became Men's Retreat enablement, #297, and most of v2.0–v2.2 ships in the Camp Harvest release). `docs/RELEASES.md` has what each tag actually contains.

**v1.1.0 — Post-Camp Quick Wins + Architecture Prep** (all ✅)

- ✅ Forgot password: shipped in #125 / PR #313 using **Resend**, not SendGrid
- ✅ Response caching: `IMemoryCache` in the services (schedule, announcements, sponsors, staff, capabilities, active event)
- ✅ Awards (`Awards` capability, shipped post-camp)
- ✅ `IActiveEventService`: shipped as `ActiveEventService` (30s cache). Pages resolve the active event at runtime. Only `SeedService` uses `SeedService.Id.EventCcn2026`

*Original notes:*
- Forgot password via email (SendGrid free tier — infra now in place post-v1.0)
- Response caching for Hub read endpoints — in-memory cache on server, zero schema changes (schedule 60s, announcements 15s, info 5min, staff 2min); reduces DB load when 150+ Annual Meeting attendees hit the Hub simultaneously. *Note: unrelated to reconnect UX — this is server↔DB performance, not browser↔server connection handling.*
- Awards UI — `AwardType` and `CamperAward` entities already in schema from day one; just needs admin CRUD + a display view + projector page for award ceremony
- `IActiveEventService` — one-file wrapper replacing direct `SeedService.Id.EventCcn2026` references across pages/services with a runtime interface call. No migrations, no UI, no user-facing change — but makes v2.0.0 decoupling a search-and-replace rather than a surgery

**v1.2.0 — Polish + Deeper v2.0 Prep** (all ⬜)

- Cloudflare Turnstile on login and forgot-password (#315); there's no login rate limit today
- Audit log — `AuditEntry` table + interceptor on SaveChanges; Vicki can see "Tyler deleted a schedule item at 3pm." Single migration, high ops value before chapter-scale events
- `ScheduleTemplate` + `ScheduleTemplateItem` entity + migration (data layer only, no admin UI yet) — v2.0.0 branch starts with the schema already done and just builds the interface on top
- Test suite foundation — service-layer unit tests for scoring, auth, and seed. Manual testing only for v1.x; want these in place before v2.0 architectural changes touch everything

---

**v2.0.0 — Self-Service Event Management**

*Goal: Vicki can configure and launch a "Men's Retreat" (or any HBDA event) entirely within the admin UI without any developer involvement. No seed changes, no code deploys, no Tyler.*

*Status:* mostly ✅ ahead of schedule.
- ✅ `/admin/events`: create, edit, set active, capability checkboxes, guest code and QR, "Copy setup from…" duplication (#312)
- ✅ `/admin/theme` (#312)
- **Duplication copies:** capabilities and schedule item types always, and sponsors, staff directory, and activities as opt-ins. It **never** copies groups, schedule, or announcements. Locations and info pages aren't event-scoped. This supersedes the "clone groups/locations/activities" line below.
- ✅ Capabilities UI (it lives on `/admin/events`, so a separate `/admin/capabilities` page was decided against)
- ✅ Faster schedule setup (#317): .xlsx template and import, paste import with preview, and "copy a past event's schedule". This covers most of what schedule templates were for
- ⬜ Schedule templates as their own entity, the setup checklist, and the Event Designer nav group

*What's already there (building blocks exist):*
- `/admin/groups` — group CRUD, event-scoped ✓
- `/admin/locations` — location CRUD ✓
- `/admin/schedule-item-types` — type CRUD + per-event enable/disable ✓
- `/admin/activities` — activity CRUD ✓
- `/admin/schedule` — schedule item CRUD ✓
- `Event` entity has `EffDate`, `ExpDate`, `IsActive`, `EventTypeId`, `ThemeId` ✓
- `EventCapability` join table exists (feature flags per event) ✓
- `ScheduleTemplate` + `ScheduleTemplateItem` entities ⬜ (planned for v1.2.0, not built)

*What needs to be built:*
- **`/admin/events` — Event CRUD**: create, edit, duplicate, set active event; replaces `IActiveEventService` stub (from v1.1.0) with full runtime-selected active event
- **Event duplication**: "Copy this event as a starting point" — clones groups, locations, schedule item types, and activities into a new event shell
- **`/admin/theme` — Theme config UI**: edit event name/tagline, primary color palette, logo upload; backed by DB `Theme` entity instead of `ThemeService` hardcoded values
- **`/admin/capabilities` — Feature flags UI**: toggle which platform features are active per event (e.g. Men's Retreat might not use the board game)
- **Schedule template admin UI**: template CRUD + apply-template action on `/admin/events`; "Save current schedule as template" reverse flow
- **Admin completion checklist**: dashboard widget showing setup progress — "0 locations added," "no schedule items yet," etc.

*UX — Event Designer subnav under Admin:*

```
Admin → Event Designer
  ├── Events              /admin/events
  ├── Theme               /admin/theme
  ├── Groups              /admin/groups              (already exists, fold in)
  ├── Locations           /admin/locations           (already exists, fold in)
  ├── Schedule Types      /admin/schedule-item-types (already exists, fold in)
  ├── Schedule Templates  /admin/schedule-templates  (new)
  ├── Activities          /admin/activities          (already exists, fold in)
  └── Capabilities        /admin/capabilities        (new)
```

*This is the primary driver for the 1→2 major version bump.* The shift is from "Tyler configures events in code" to "Vicki configures events in the UI."

---

**v2.1.0 — Member Access (Yapp Replacement)**

*Goal: Families and chapter members can access event info on their phones with zero friction — same experience as Yapp, but ours.*

*Status:* partly ✅.
- ✅ Event code and QR `/join` (`Event.GuestCode`, PR #298)
- ✅ Named guests (`GuestAttendee`, #304)
- ✅ Guest push notifications (#306)
- ⬜ Persistent "full member" accounts across events

*Tiered identity model:*

| Tier | Identity | How they join | What they can do |
|---|---|---|---|
| Anonymous guest | Device/session only | Event code → instant access | Read-only Hub: schedule, announcements, info, directory |
| Named guest | Name + optional email, event-scoped | Prompted on first identity-requiring action | Above + session signups + personal schedule + check-in |
| Full member | Persistent across events | Optional upgrade from named guest | Above + push notifications + multi-event history |
| Staff/Admin | Full account | Admin-created (existing) | Everything |

*What ships:*
- `EventCode` entity + `/join/{code}` route — validates code, issues a `Member` role cookie carrying `EventId` claim scoped to that event; expires at `EventExpDate + 1 day`; Vicki generates one code per event (`HARVEST26`), posts it on a flyer
- `GuestAttendee` entity — `Name`, `Email?`, `EventId`, `SessionToken` (ties back to cookie); created on first identity-requiring action with a single low-friction "What's your name?" prompt
- Member-facing Hub: read-only schedule, announcements, info pages, staff directory with its own layout/nav tier (no admin controls, no transaction log)
- Push notifications (Web Push + VAPID, `PushSubscription` table, background ASP.NET service) — now that member identity exists; "new announcement" and "schedule change" pushes to both named guests and full members
- Optional "save for future HBDA events" upgrade flow → creates a persistent member account

*Note: CCN campers are not allowed phones — CCN was intentionally the perfect staff-only beta. Camp Harvest and Annual Meeting are the first real member-access events.*

---

**v2.2.0 — Event Operations**

*Goal: Operational depth for larger events — session signups, attendance, awards ceremony, post-event reporting.*

*Status:*
- ✅ Attendance (#308, shipped as `ScheduleItemAttendance`, not `EventAttendance`)
- ✅ Session sign-ups, as breakout slots plus `ScheduleItemRegistration`, and QR check-in per item (#311 / PR #317; `/admin/breakouts`, `/checkin/{code}`)
- ⬜ End-of-event reporting beyond attendance CSV

- **Session signups**: limited-capacity breakout sessions; `SessionSignup` entity FK to either `UserId` (staff) or `GuestAttendeeId` (named guest); capacity counts; roster view for facilitators
- **Attendance tracking**: `EventAttendance` table; check-in flow; admin roster; works across all identity tiers
- **End-of-event reporting/export**: final score summary, transaction log PDF, incident report bundle, attendance sheets — the "give Vicki the paperwork" feature
- Awards UI already shipped in v1.1.0; this release adds ceremony projector display

---

**v3.0.0 Horizon**

*v3.0.0 is not planned — it will announce itself when the platform outgrows its current architecture. Likely triggers:*
- Multi-tenant SaaS: other HBDA chapters (or other organizations) running their own events on the platform — requires tenant isolation in the data model, billing, org-level admin
- True native mobile app: moving off PWA to a dedicated iOS/Android app — requires a separate API layer and new client
- Separate frontend client: if a camper-facing React or WASM app is needed, the Blazor Server backend would be extracted to a proper API at that point

None of these are on the near-term horizon. The entire v2.x roadmap is extending and opening up the same platform.

---

## Pitfalls to Avoid (Lessons Learned)

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
All form modals must use: backdrop `animation:fadeIn .2s ease` with `rgba(26,26,26,.65)`, panel `ccn-panel` class with `animation:popIn .25s ease` and `box-shadow:8px 8px 0 var(--black)`, and centered with `display:flex;align-items:center;justify-content:center;padding:20px`. Do not use bottom-sheet patterns for forms — they lack the popIn animation and feel inconsistent.

**13. Navigation property names must follow EF Core FK convention or be configured explicitly.**  
If a navigation property `FooUser` references `User` but the FK property is not named `FooUserUserId` or `UserId`, EF Core creates a shadow property (e.g. `FooUserUserId`) instead of using your named property. The shadow property defaults to `Guid.Empty` on insert, causing a NOT NULL FK constraint violation that crashes the Blazor circuit. Fix: add explicit `.HasForeignKey(e => e.YourProperty)` in `OnModelCreating` whenever the FK property name doesn't match the `<NavName><PKName>` convention. This was the root cause of the v0.5.4 schedule save bug. Any entity with **two** navigations to the same table (e.g. `ScheduleItemAttendance.User` + `CheckedInByUser`, `PasswordResetToken.User` + `CreatedByUser`) must configure both explicitly. After `dotnet ef migrations add`, read the migration and confirm there are no shadow columns like `UserId1` / `EventId1`.

**14. Always use synchronous `DbFactory.CreateDbContext()` — never the async variant.**  
`await using var db = await DbFactory.CreateDbContextAsync()` introduces an async disposal pattern that conflicts with how services are structured in this codebase. Use `using var db = DbFactory.CreateDbContext()` (synchronous) everywhere. The async variant caused a silent bug on `Dashboard.razor` where the first-day schedule query returned nothing despite data existing. All existing services use the synchronous call — match it.

**15. Razor attribute quote conflict: use single quotes on `@onclick` when the lambda contains `$"..."` interpolated strings.**  
If an `@onclick` (or other `@on*`) lambda contains a C# interpolated string literal, using double quotes for the HTML attribute causes parse errors (CS1525/CS1056). Wrap the outer attribute in single quotes instead:  
`@onclick='() => _field = $"/path/{someId}"'`  
This also applies to any event attribute whose lambda body contains a double-quoted string literal. Root cause of the lightbox onclick bug in `/admin/locations`.

**16. MudBlazor is 6.11 — checkboxes bind with `@bind-Checked`, not `@bind-Value`.**  
Most admin pages use plain `<input type="checkbox" checked="@x" @onchange="...">` instead, which is fine too.

**17. A child component whose parameters never change won't re-render when its parent does.**  
Blazor skips re-rendering components whose parameters are all unchanged primitives. If such a component reads scoped state that loads asynchronously (e.g. `ThemeService`), it must `await ThemeSvc.LoadAsync()` in its own `OnInitializedAsync`, or it keeps showing the pre-load defaults. Root cause of `NavBrand` showing the CCN logo on other events (#312).

**18. `[SupplyParameterFromQuery]` `bool` parameters don't parse `1`.**  
`?sent=1` throws "Cannot parse the value '1' as type 'System.Boolean'" and 500s the page. Use `?sent=true` (or a `string` parameter).

**19. Seed methods must not overwrite rows admins can edit.**  
`SeedService` runs on every startup. Once a table has an admin UI (themes, since #312), its seed must be insert-only. `SeedThemeAsync` is the example, including its one-time backfill guarded by `ColorPalette is null`.

**20. One `Theme` row per event.**  
Themes are owned by a single event (app invariant, not a DB constraint). Creating an event clones a theme via `ThemeCloner` (from a `ThemePresets` preset or the source event); never point two events at the same `ThemeId`. `SeedService.SplitSharedThemesAsync` repairs any shared rows on startup.

**21. Native form posts for anything that sets a cookie.**  
Login, change-password, reset-password, and guest join are HTML `<form method="post">` to minimal-API endpoints in `Program.cs`, because a cookie can't be set from the SignalR circuit. When testing these pages with Playwright, wait ~3s after load before filling inputs: the interactive render replaces the prerendered form and wipes anything typed earlier.

**22. On phones the page doesn't scroll: `.app-main` does (app shell, #326).**  
Below 1024px, `MainLayout` is a flex column pinned with `position:fixed; inset:0`: header, then `.app-main` (the only scroller), then the bottom bar. This stops iOS PWAs from dragging the sticky header on overscroll and from shifting the fixed bottom bar between pages (#153). So `window.scrollY` stays 0 on mobile. Scroll `.app-main` instead (`ccnShell.scrollToTop()`). `MainLayout` resets it on every path change. `.app-content` is 1px taller than `.app-main`, so short pages still bounce (an iOS scroller that fits doesn't respond to touch). Don't make the header or bottom bar `sticky`/`fixed` again inside `.app-frame`. `Ui__AppShell=false` restores the old layout if it ever needs reverting.  
**The status bar style must stay `black`, not `black-translucent`.** With translucent, iOS draws the page under the status bar but reports *and paints* it one status bar short (873 of 932pt on a 430×932 iPhone). Nothing can reach the bottom 59pt: a taller frame (`100lvh`) just gets its bottom clipped, which hid the tab labels. iOS reads this meta when the app is added to the home screen, so changing it means deleting and re-adding the app.

**23. Local CSS/JS URLs are content-hashed: never hand-version them (#326).**  
Static files are served with `Cache-Control: immutable` for a year, so a file whose URL doesn't change when its contents do stays stale on phones. Every local `<link>`/`<script>` in `_Layout.cshtml` uses `asp-append-version="true"` (`?v=<hash>`), and `service-worker.js` is network-first for same-origin files, keeping copies only as an offline fallback (`service-worker.js` itself is served `no-cache`). So a CSS or JS deploy reaches installed apps on their next launch with no reinstall. Add `asp-append-version` to any new script or stylesheet tag. Before this, the worker served `app.css` cache-first from a hand-versioned precache, and a version mismatch shipped pre-shell CSS to phones. The one thing a deploy can't change is the `apple-mobile-web-app-*` meta tags, which iOS reads only when the app is added to the home screen.

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

*Mini Marios (Group1) was removed from the seed in v0.5.5, so CCN 2026 ran with 3 groups (Group2–4). Groups are event-scoped. Most non-camp events have none.*

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
| Hub (Camp Info) | `Location`, `InfoPage`, `Announcement`, `ScheduleItem`, `ScheduleItemType`, `EventScheduleItemType`, `ScheduleItemGroup`, `IncidentReport`, `Sponsor`, `CampDocument` |
| Guests | `GuestAttendee`, `GuestEventVisit`, `PushSubscription`, `ScheduleItemAttendance`, `ScheduleItemRegistration` |
| Per-event staff | `EventStaff` (a person on one event's team: role label, group, Hub directory title/visibility/order). A person is a `User` row (contact details and photo entered once; `CanSignIn` false for listed-only people). The old per-event `StaffMember` directory cards were merged into this (#311). |
| Auth extras | `PasswordResetToken` (SHA-256 hashed, single-use) |
| Games (post-camp) | `BowserScript` |

**Event-scoped vs global:** `Location` and `InfoPage` are **global** (no `EventId`) and shared by every event. Groups, activities, sponsors, team members (`EventStaff`), documents, schedule items, announcements, capabilities, enabled schedule item types, and the theme belong to one event. `User` itself is global. Who is staff at an event is `EventStaff` (#311), and that's what rosters, bulk sign-up, and "all staff" use. Its role is a label only: permissions still come from `User.UserRoleId`.

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
**Tags:** after each `dev` → `main` release PR merges, add an annotated tag on that merge commit (`git tag -a vX.Y.Z <sha> -m "…"`), push it, and add a row to `docs/RELEASES.md`. A feature release bumps the minor version (the Camp Harvest release is `v1.2.0`) and a hotfix bumps the patch. `v1.0.0`–`v1.1.1` were tagged after the fact on 2026-10-08. The `v1.0.0-rc.N` convention ended at go-live (details in `docs/RELEASES.md`).  
**Issue-first rule:** Open a GitHub issue before creating a branch. Branch name must use the issue number GitHub assigns.  
**Closing issues:** `main` is the default branch, so "Closes #N" in a PR into `dev` does **not** auto-close the issue. Close it by hand (with a comment naming the PR) once the PR merges into `dev`.  
**`gh` CLI:** Installed at `$env:LOCALAPPDATA\Programs\gh\gh.exe`. Use PowerShell (not Bash) to invoke it.

---

## Key Files

| File | Purpose |
|---|---|
| `REQUIREMENTS.md` | Full functional spec, animation spec, infrastructure, decisions |
| `CampClotNot/Hubs/LiveHub.cs` | SignalR hub at `/livehub` — all real-time events |
| `CampClotNot/Services/BoardService.cs` | Board logic + block hit SignalR broadcasts |
| `CampClotNot/Services/MiniGameService.cs` | Mini-game logic + spinner SignalR broadcasts |
| `CampClotNot/Services/SeedService.cs` | Startup seed — all reference data + CCN 2026 event |
| `CampClotNot/Services/ThemeService.cs` | `ThemeConfig` (CSS variable tokens injected via `ThemeHead.razor`), `ThemePresets` (Classic / Men's Retreat / Mario), and scoped `ThemeService` resolving the active event's theme + logo URLs |
| `CampClotNot/Services/ThemeAdminService.cs` / `ThemeCloner.cs` | `/admin/theme` persistence and logo/banner uploads / per-event theme row creation |
| `CampClotNot/Services/ActiveEventService.cs` | Resolves the active `Event` (30s `IMemoryCache`); call `InvalidateCache()` after changing which event is active |
| `CampClotNot/Services/CapabilityService.cs` | Per-event feature flags (`Feature` enum) for nav and page gating |
| `CampClotNot/Services/EventSetupService.cs` | Create an event (+ theme + copied rows) in one `SaveChanges`; `EventCopyOptions` holds the opt-in copy flags (sponsors, activities, event staff) |
| `CampClotNot/Services/GuestAccessService.cs` | Guest join, `GuestAttendee` lookup/creation, guest claims (`GuestClaimTypes`) |
| `CampClotNot/Services/AttendanceService.cs` | Self/Admin/QR check-in, check-in codes, roster, CSV export |
| `CampClotNot/Services/RegistrationService.cs` | Breakout sign-ups: self sign-up (row-locked capacity), admin assign/move/remove, unassigned list |
| `CampClotNot/Services/EventStaffService.cs` | Per-event staff list (`EventStaff`) |
| `CampClotNot/Services/ScheduleVisibility.cs` | Which items a person sees given breakout picks (shared by `/hub/schedule` and the Dashboard) |
| `CampClotNot/Services/ScheduleImportService.cs` | Schedule setup: lenient time/day parsing, .xlsx template (ClosedXML), spreadsheet/paste import with preview, copy a past event's schedule |
| `CampClotNot/Pages/CheckIn.razor` | `/checkin/{code}`, the landing page for a session's QR code |
| `CampClotNot/Pages/Admin/Breakouts.razor` / `Team.razor` | `/admin/breakouts` / `/admin/team`: the one page for staffing an event and its Hub staff directory. Add from past events or someone new (with or without sign-in), role and group at this event, edit contact details and photo, directory title and order. `/admin/event-staff` and `/admin/staff` route here |
| `CampClotNot/Services/StaffDirectoryService.cs` | Hub staff directory cards (team rows with `ShowInDirectory`), order, cache |
| `CampClotNot/Services/PushNotificationService.cs` | Web Push (VAPID) to guest subscriptions (singleton) |
| `CampClotNot/Services/PasswordResetService.cs` | Reset/invite tokens, `ForgotPasswordQueue` + background worker, `PublicBaseUrl` |
| `CampClotNot/Services/Email/` | `IEmailSender`, `ResendEmailSender` (HTTPS API), `EmailTemplates` |
| `CampClotNot/Pages/Admin/Events.razor` | Event CRUD, active toggle, capabilities, guest code/QR, duplication |
| `CampClotNot/Pages/Admin/ThemeEditor.razor` | `/admin/theme` (named `ThemeEditor` to avoid clashing with the `Theme` entity) |
| `CampClotNot/Pages/Admin/Guests.razor` / `Attendance.razor` | `/admin/guests` / `/admin/attendance` |
| `CampClotNot/Shared/NavBrand.razor` | Nav badge logo/wordmark + subtitle, shared by `AppNav` and `GuestNav` |
| `CampClotNot/Shared/ThemeHead.razor` | Injects CSS vars into `:root`, defines body styles, shared animations |
| `CampClotNot/Shared/LoginLayout.razor` | Bare layout for login page — ThemeHead only, no nav |
| `CampClotNot/Shared/PrintLayout.razor` | Bare layout for print views — ThemeHead only, no nav, no MudBlazor providers |
| `CampClotNot/Pages/Login.razor` | Login page — uses `LoginLayout`, `position:fixed` centering |
| `CampClotNot/Pages/Board.razor` | Board game trigger — Admin + Staff |
| `CampClotNot/Pages/BoardDisplay.razor` | Board projector display at `/board/display` — Admin only |
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
| `CampClotNot/Services/SponsorService.cs` | Sponsor CRUD scoped to the active event |
| `CampClotNot/appsettings.Development.json` | Local DB + seed credentials (gitignored — must copy to worktrees manually) |

---

## Local Dev Setup (Tyler's Windows machine)

- **DB:** PostgreSQL local, database `hbda_dev`
- **Connection string:** `CampClotNot/appsettings.Development.json` (gitignored)
- **Migrations:** applied automatically at startup by `SeedService.SeedAsync()` → `MigrateAsync()`. `dotnet ef database update` from `CampClotNot/` also works.
- **Start app:** `dotnet run` from `CampClotNot/` or F5 in Visual Studio
- **Login (dev):** `tyler@hbda.local` / `DevAdmin1!` (seeded from appsettings.Development.json)
- **gh CLI:** `& "$env:LOCALAPPDATA\Programs\gh\gh.exe" <command>` from PowerShell

## Cloud Agent Sessions (Claude Code on the web)

- **.NET isn't preinstalled** and `dot.net` is blocked by the network proxy:
  ```bash
  apt-get update && apt-get install -y dotnet-sdk-8.0
  dotnet tool install -g dotnet-ef --version '8.*'
  export PATH="$PATH:$HOME/.dotnet/tools"
  ```
- **Creating a migration** needs a design-time connection string but no database. From `CampClotNot/`:
  ```bash
  ConnectionStrings__DefaultConnection="Host=localhost;Database=dummy;Username=x;Password=x" \
  Vapid__PublicKey=BPlaceholderPlaceholderPlaceholder Vapid__PrivateKey=x \
  dotnet ef migrations add <Name>
  ```
  Then read the migration and check for shadow FK columns (pitfall #13).
- **Build check:** `dotnet build` with no new warnings. The baseline on `dev` is 3 (`Dashboard.razor` CS0414, `Announcements.razor` CS4014, `MainLayout.razor` CS0618). There's no automated test suite, so PRs carry a manual checklist.
- **Running the app** (optional, very useful): `apt-get install -y postgresql`, `service postgresql start`, create a DB, then run with:
  - `ASPNETCORE_URLS=http://localhost:5055`, `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__DefaultConnection=...`, `Seed__AdminEmail`/`Seed__AdminPassword`
  - a **real** P-256 VAPID key pair (`Vapid__PublicKey`/`Vapid__PrivateKey`). The placeholder crashes pages that build the push service.
  - `dotnet run --no-build --no-launch-profile`. The launch profile's HTTPS dev cert crashes Blazor circuits here.
- **Playwright/Chromium** is preinstalled (`executablePath: /opt/pw-browsers/chromium-1194/chrome-linux/chrome`). Google Fonts is blocked, so use `waitUntil: 'domcontentloaded'` instead of `load`. See pitfall #21 for native-form pages.
- **Unreachable from the sandbox:** Railway's Postgres (the egress proxy only carries HTTPS) and `api.resend.com`. Verify those on staging.
- When killing the app, match the binary (`bin/Debug/net8.0/CampClotNot`), not `CampClotNot`. The broader pattern also matches the agent's own shell.

## Production (Railway)

- **Migrations apply automatically at startup.** There's no manual step when deploying.
- **Environment variables:** `DATABASE_URL`, `ASPNETCORE_ENVIRONMENT`, `Seed__AdminEmail`/`Seed__AdminPassword` (only used when the Users table is empty), `Vapid__PublicKey`/`Vapid__PrivateKey`/`Vapid__Subject`, `Email__ResendApiKey`/`Email__From` (password emails; unset means Admins get copyable links instead), and `App__PublicBaseUrl` (optional, the base for emailed links), and `Ui__AppShell` (optional; `false` turns off the mobile app-shell layout from #326 and restores whole-page scrolling).
- **Emergency manual migration** (normally unnecessary): `dotnet ef database update --connection '<public TCP proxy connection string>'` from a machine that can reach Railway's public Postgres proxy (Railway → Postgres → Settings → Public Networking). Use single quotes in PowerShell so `$` in the password isn't expanded.
- **Locked out of an account:** use "Forgot password?" (once email is configured), or have another Admin use "Email reset link" / "Reset PW" on `/admin/users`.
