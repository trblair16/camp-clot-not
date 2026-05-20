# v0.5.0 Design Spec — Camp Info Hub + PWA

**Date:** 2026-05-19  
**Author:** Tyler Blair (brainstormed with Claude Code)  
**Status:** Approved — ready for implementation planning  
**Target:** ~June 7. Hard defer date: June 13. Camp runs June 20-25.  
**GitHub milestone:** `v0.5.0 — Camp Info Hub`

---

## 1. Overview

v0.5.0 adds a Camp Info Hub — a Yapp replacement for staff-facing camp information — and PWA installability. The Hub ships as a single "📋 Hub" nav tab with four internal sub-sections (Schedule, Announcements, Staff Directory, Info Pages). The PWA layer makes the app installable to staff home screens.

This is a beta feature (time-permitting, not camp-critical). If not complete by June 13, defer all of v0.5.0 to post-camp v1.1.0.

---

## 2. Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| Sub-nav architecture | Page-per-route + shared `HubSubNav` component | Consistent with existing page pattern; no Blazor layout nesting complexity |
| Hub root `/hub` | Redirect to `/hub/schedule` | Schedule is the most frequently consulted during camp |
| Service pattern | Concrete classes (no interfaces) | Consistent with `MiniGameService`, `BoardService`; interface layer buys nothing at camp scale |
| Response caching | Deferred — post-go-live performance concern | 10-15 concurrent users; DB handles load without caching |
| Dashboard pinned banner | Deferred — `GetLatestPinnedAsync()` stub exists | Dashboard.razor untouched in v0.5.0 |
| Display role access | Redirect to `/` | Display role is competition-only; no Hub access |
| `ScheduleEvent.MaxCapacity` | Added silently (nullable int) | Avoids future migration; no UI in v0.5.0 |
| Admin Hub designer | Post-go-live | Inline editing on Hub pages sufficient for camp |

---

## 3. Data Model

### 3.1 New Entities

#### `InfoPage`
```
PageId          Guid (PK)
Slug            string (unique index)
Title           string
Body            string (markdown)
IconEmoji       string
SortOrder       int
UpdatedAt       DateTime
UpdatedByUserId Guid (FK → User)
```

#### `StaffMember`
```
StaffMemberId   Guid (PK)
DisplayName     string
RoleTitle       string
Phone           string? (nullable)
Email           string? (nullable)
AvatarEmoji     string (default "👤")
IsVisible       bool
SortOrder       int
LinkedUserId    Guid? (nullable FK → User)
```
`LinkedUserId` is nullable — a staff card can exist for someone with no app login (visiting speakers, volunteers). It is NOT a link table because the relationship is at most one-to-one.

#### `Announcement`
```
AnnouncementId  Guid (PK)
Title           string
Body            string
Priority        enum AnnouncementPriority { Normal, Urgent }
IsPinned        bool
AuthorId        Guid (FK → User)
CreatedAt       DateTime
ExpiresAt       DateTime? (nullable)
IsArchived      bool
```
Append-only — no hard delete. Archive replaces delete. `ExpiresAt` auto-archives on read in the service layer.

#### `ScheduleEvent`
```
EventId                 Guid (PK)
CampDay                 DateOnly
StartTime               TimeOnly
EndTime                 TimeOnly? (nullable)
Title                   string
Description             string? (nullable)
LocationDisplayName     string? (nullable)
EventType               enum ScheduleEventType { Activity, Meal, Travel, Free, Mandatory }
AppliesToAllGroups      bool
MaxCapacity             int? (nullable — stored silently; no UI in v0.5.0; supports future registration feature)
CreatedBy               Guid (FK → User)
UpdatedAt               DateTime
```

#### `ScheduleEventGroup` (bridge table)
```
EventId   Guid (FK → ScheduleEvent, composite PK)
GroupId   Guid (FK → Group, composite PK)
```
Only populated when `AppliesToAllGroups = false`.

### 3.2 AppDbContext Additions
```csharp
public DbSet<InfoPage> InfoPages => Set<InfoPage>();
public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
public DbSet<Announcement> Announcements => Set<Announcement>();
public DbSet<ScheduleEvent> ScheduleEvents => Set<ScheduleEvent>();
public DbSet<ScheduleEventGroup> ScheduleEventGroups => Set<ScheduleEventGroup>();
```

`OnModelCreating` additions:
- Composite PK on `ScheduleEventGroup` (EventId + GroupId)
- Unique index on `InfoPage.Slug`

### 3.3 Migration Order
Each entity is its own independently deployable migration:
1. `AddInfoPage`
2. `AddStaffMember`
3. `AddAnnouncement`
4. `AddScheduleEvent` (includes `ScheduleEventGroup`)

### 3.4 Seeding

**InfoPage seeds** — 5 rows with stable IDs (`0000000e-000e-000e-000e-*` prefix), placeholder markdown body text:

| ID suffix | Slug | Title | IconEmoji |
|---|---|---|---|
| `000000000001` | `rules` | Camp Rules | 📋 |
| `000000000002` | `faq` | FAQ | ❓ |
| `000000000003` | `medical` | Medical Info | 🏥 |
| `000000000004` | `schedule-overview` | Schedule Overview | 📅 |
| `000000000005` | `packing` | Packing List | 🎒 |

No seeds for StaffMember, Announcement, or ScheduleEvent — those are runtime data.

### 3.5 Group Seed Cleanup (included in v0.5.0)
- Remove `Id.Group5` and `Id.Group6` constants from `SeedService.Id` (dead constants)
- Remove the stale-group cleanup `foreach` loop from `SeedGroupsAsync` — it ran its one-time purpose
- Run the following SQL against `hbda_dev` before first migration:
```sql
DELETE FROM group_board_pos WHERE group_id IN (
    '0000000a-000a-000a-000a-000000000005',
    '0000000a-000a-000a-000a-000000000006'
);
DELETE FROM scripted_block_hits WHERE group_id IN (
    '0000000a-000a-000a-000a-000000000005',
    '0000000a-000a-000a-000a-000000000006'
);
DELETE FROM groups WHERE group_id IN (
    '0000000a-000a-000a-000a-000000000005',
    '0000000a-000a-000a-000a-000000000006'
);
```

### 3.6 Future Entities (not built in v0.5.0, noted for v1.x)

```
GuestAttendee       — GuestId, EventId, FirstName, LastName, CreatedAt,
                      SessionCookieToken (ties to event-code guest login, v1.x)

SessionRegistration — RegistrationId, ScheduleEventId, GuestAttendeeId?,
                      FirstName, LastName, RegisteredAt
                      (works with or without a GuestAttendee — name-capture only mode)
```
`ScheduleEvent.MaxCapacity` exists now to support these without a future migration. The full guest login flow (event code → cookie → Attendee claim) is a v1.x feature.

---

## 4. Service Layer

All four services are concrete classes following the `MiniGameService` pattern: `IDbContextFactory<AppDbContext>` via primary constructor; each method creates and disposes its own context. Registered as `AddScoped` in `Program.cs`.

### `InfoPageService`
- `GetAllAsync()` → all pages ordered by SortOrder
- `GetBySlugAsync(slug)` → single page or null
- `UpdateBodyAsync(pageId, body, updatedByUserId)` → stamps UpdatedAt + UpdatedByUserId

### `StaffDirectoryService`
- `GetVisibleAsync()` → IsVisible = true, ordered by SortOrder
- `GetAllAsync()` → all (admin view, includes hidden)
- `UpsertAsync(dto)` → add or update
- `DeleteAsync(staffMemberId)` → hard delete (no audit trail needed for directory entries)
- `ReorderAsync(orderedIds)` → bulk SortOrder update

### `AnnouncementService`
- `GetFeedAsync()` → non-archived; auto-archives expired rows on read; pinned first → CreatedAt desc
- `GetLatestPinnedAsync()` → returns most recent pinned item or null — **stub for future Dashboard banner**; exists now so Dashboard can call it without a service change later
- `PostAsync(dto, authorId)` → creates announcement; enforces Normal-only for Staff (at page layer)
- `PinAsync(id, isPinned)` → toggle pin state; Admin only (enforced at page layer)
- `ArchiveAsync(id)` → sets IsArchived = true

### `ScheduleService`
- `GetByEventAsync(eventId)` → all events ordered by CampDay then StartTime
- `GetForDayAsync(eventId, day)` → single day's events
- `UpsertAsync(dto, userId)` → add or update; stamps UpdatedAt
- `DeleteAsync(eventId)` → hard delete (schedule entries are not append-only — cancelled sessions can be removed)
- `GetGroupsForEventAsync(eventId)` → GroupIds for events where AppliesToAllGroups = false

---

## 5. Hub Pages & Routing

### 5.1 File Structure
```
CampClotNot/Pages/Hub/
  Hub.razor               @page "/hub"  — redirects to /hub/schedule
  HubSubNav.razor         shared sub-nav component
  Schedule.razor          @page "/hub/schedule"
  Announcements.razor     @page "/hub/announcements"
  Staff.razor             @page "/hub/staff"
  Info.razor              @page "/hub/info"
```

### 5.2 Auth
All Hub pages: `@attribute [Authorize(Roles = "Admin,Staff")]`

`Hub.razor` redirect: uses `IHttpContextAccessor` pattern (same as `RedirectToLogin.razor`) — if Display role user hits `/hub`, redirect to `/`. Never use `Nav.NavigateTo(forceLoad:true)` during prerendering.

### 5.3 `HubSubNav.razor`
Renders four tab buttons: Schedule / Announcements / Staff / Info. Active state derived from `NavigationManager.Uri` using the same `IsActive(href)` pattern as `AppNav`. Neo-brutalist tab style: black border, cream bg, active = black fill + white text. Included at the top of each Hub page.

### 5.4 Page Behavior

**Schedule (`/hub/schedule`)**
- Timeline grouped by CampDay
- Today's day expanded by default; others collapsed
- Event cards: time block, EventType badge (color-coded enum), location, group scope chips
- Staff + Admin: add/edit/delete events via inline form or MudDialog
- Empty state per day: "No events scheduled — tap + to add one"

**Announcements (`/hub/announcements`)**
- Feed: pinned first → CreatedAt descending
- Urgent: red left border + "🚨 URGENT" badge
- Normal announcements: Staff + Admin can post; Urgent + pin: Admin only
- Append-only: archive replaces delete
- `ExpiresAt` auto-archives on read (service layer, not UI)

**Staff Directory (`/hub/staff`)**
- Card grid: 2 columns mobile, 3+ desktop
- Emoji avatar, `tel:` tap-to-call and `mailto:` links
- IsVisible = false hides card from all views
- Admin: add / edit / reorder / toggle visibility; Staff: read-only

**Info Pages (`/hub/info`)**
- Sidebar list sorted by SortOrder; active page via query param (`/hub/info?slug=rules`)
- Body rendered via Markdig: `@((MarkupString)Markdig.Markdown.ToHtml(page.Body))`
- Admin: textarea edit mode with save, stamps UpdatedAt/UpdatedByUserId
- Staff: read-only
- Pages cannot be created or deleted in v0.5.0 — slug list is fixed at seed time

### 5.5 Role Permission Summary

| Feature | Admin | Staff | Display |
|---|---|---|---|
| View all Hub pages | ✅ | ✅ | ❌ (redirect to /) |
| Edit Schedule | ✅ | ✅ | ❌ |
| Post Normal announcement | ✅ | ✅ | ❌ |
| Post Urgent / Pin | ✅ | ❌ | ❌ |
| Edit Staff Directory | ✅ | ❌ | ❌ |
| Edit Info Page body | ✅ | ❌ | ❌ |

---

## 6. Nav Integration (`AppNav.razor`)

### Desktop header
Add Hub link between Activities dropdown and Transactions:
```html
<a href="/hub" class="nav-link" style="@ActiveStyle("/hub", "#4A90D9")">📋 Hub</a>
```
(or appropriate color from the palette — not a hardcoded hex if a token exists)

### Mobile bottom bar
Hub gets a direct tab (no bottom sheet — it links directly to `/hub/schedule`):
```
Dashboard | Activities | Hub | Admin
```
Mobile bar now has 4 tabs. Hub tab active when `IsActive("/hub")`.

Hub is visible to Admin + Staff only. Wrap in `<AuthorizeView Roles="Admin,Staff">`.

---

## 7. PWA

Implement PWA **last**, after all Hub features are functional and manually tested.

### 7.1 `wwwroot/manifest.json`
```json
{
  "name": "Camp Clot Not — Super Party '26",
  "short_name": "Clot Not",
  "description": "Staff dashboard for Super Clot Not Party 2026",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#3d0066",
  "theme_color": "#3d0066",
  "orientation": "portrait-primary",
  "icons": [
    { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "/icons/icon-512-maskable.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

### 7.2 `_Host.cshtml` additions
```html
<link rel="manifest" href="/manifest.json" />
<meta name="theme-color" content="#3d0066" />
<meta name="apple-mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
<meta name="apple-mobile-web-app-title" content="Clot Not" />
<link rel="apple-touch-icon" href="/icons/icon-192.png" />
```

### 7.3 `wwwroot/service-worker.js`
Cache-first for static shell assets (CSS, JS bundles, icons, fonts). Explicit exclusions:
- `/livehub` (SignalR — never cache)
- `/account/login` (auth)
- Any path starting with `/api/`

On fetch failure for a cached asset, fall back to `offline.html`.

### 7.4 `wwwroot/offline.html`
Fully static — zero Blazor dependency. Contains:
- CCN logo
- "You're offline — check your connection and try again"
- Last-known scores from `localStorage` (key written by app on each successful Dashboard load)
- "Try again" button (`window.location.reload()`)
- Inline CSS matching the app's cream/black neo-brutalist theme

### 7.5 Connection Quality Indicator
Small colored dot in the nav header (desktop: near logo; mobile: in the top header strip).
- 🟢 Green = SignalR connected
- 🟡 Yellow = SignalR reconnecting
- 🔴 Red = SignalR disconnected

Wired to `HubConnection.State` change events. Injected into `MainLayout` or `AppNav` via a small `ConnectionIndicator.razor` component with JS interop.

### 7.6 Install Banner (`InstallPromptBanner.razor`)
Included in `MainLayout`. Checks `localStorage` on mount. Shows once per device.

- **Android:** Intercepts `beforeinstallprompt` via JS interop. Banner: "📲 Install this app — tap Install for quick access during camp." Triggers native prompt. Dismissed state in `localStorage`.
- **iOS Safari:** Detects UA via JS interop. Banner: "📲 Tap Share → Add to Home Screen." Share icon inline. Dismissed state in `localStorage`.

Neither banner blocks UI.

### 7.7 Icons (manual step — Tyler generates)
Generate from CCN logo PNG via [PWABuilder](https://www.pwabuilder.com) or [RealFaviconGenerator](https://realfavicongenerator.net):

| File | Size |
|---|---|
| `wwwroot/icons/icon-192.png` | 192×192 |
| `wwwroot/icons/icon-512.png` | 512×512 |
| `wwwroot/icons/icon-512-maskable.png` | 512×512 (logo centered in inner 80%) |

Implementation will include placeholder files with a clear `// TODO: replace with real icons` note.

### 7.8 NuGet Package
Add `Markdig` NuGet package for Info Pages markdown rendering.

---

## 8. Deferred / Out of Scope

| Item | When |
|---|---|
| Dashboard pinned announcement banner | Post-go-live — `GetLatestPinnedAsync()` stub exists |
| `IMemoryCache` response caching (§9.3.3) | Post-go-live performance pass |
| Guest attendee access (event code login) | v1.x — entities designed for it |
| Session registration for breakout sign-ups | v1.x — `MaxCapacity` field exists |
| `/admin/hub` schedule + announcement designer | Post-go-live — inline editing sufficient for camp |
| Push notifications | v1.1 per §9.4 |

---

## 9. Implementation Order

1. **Pre-migration cleanup** — SQL to remove Group 5 & 6 rows from local DB
2. **Group seed cleanup** — remove dead `Id.Group5/6` constants + stale cleanup loop
3. **Entity classes** — 5 new entity files in `Data/Entities/`
4. **AppDbContext** — add 5 new DbSets + OnModelCreating config
5. **Migrations** — 4 migrations in order: InfoPage → StaffMember → Announcement → ScheduleEvent
6. **SeedService** — add InfoPage seed IDs + `SeedInfoPagesAsync()` method
7. **`dotnet ef database update`** — apply all migrations locally
8. **Services** — 4 service classes + `Program.cs` registrations
9. **Add Markdig NuGet package**
10. **HubSubNav.razor** — shared sub-nav component
11. **Hub pages** — Hub.razor (redirect) + Schedule + Announcements + Staff + Info
12. **AppNav.razor** — Hub tab (desktop + mobile)
13. **Manual test** — all four Hub sections, all roles, Display redirect
14. **PWA** — manifest, _Host.cshtml, service-worker.js, offline.html, ConnectionIndicator, InstallPromptBanner
15. **Manual test** — PWA installability on Android + iOS Safari

---

## 10. Files to Create / Modify

| Action | File |
|---|---|
| Modify | `CampClotNot/Data/AppDbContext.cs` |
| Modify | `CampClotNot/Services/SeedService.cs` |
| Modify | `CampClotNot/Shared/AppNav.razor` |
| Modify | `CampClotNot/Program.cs` |
| Modify | `CampClotNot/Pages/_Host.cshtml` |
| Modify | `.gitignore` |
| Create | `CampClotNot/Data/Entities/InfoPage.cs` |
| Create | `CampClotNot/Data/Entities/StaffMember.cs` |
| Create | `CampClotNot/Data/Entities/Announcement.cs` |
| Create | `CampClotNot/Data/Entities/ScheduleEvent.cs` |
| Create | `CampClotNot/Data/Entities/ScheduleEventGroup.cs` |
| Create | `CampClotNot/Data/Migrations/` (4 new migrations) |
| Create | `CampClotNot/Services/InfoPageService.cs` |
| Create | `CampClotNot/Services/StaffDirectoryService.cs` |
| Create | `CampClotNot/Services/AnnouncementService.cs` |
| Create | `CampClotNot/Services/ScheduleService.cs` |
| Create | `CampClotNot/Pages/Hub/Hub.razor` |
| Create | `CampClotNot/Pages/Hub/HubSubNav.razor` |
| Create | `CampClotNot/Pages/Hub/Schedule.razor` |
| Create | `CampClotNot/Pages/Hub/Announcements.razor` |
| Create | `CampClotNot/Pages/Hub/Staff.razor` |
| Create | `CampClotNot/Pages/Hub/Info.razor` |
| Create | `CampClotNot/Shared/ConnectionIndicator.razor` |
| Create | `CampClotNot/Shared/InstallPromptBanner.razor` |
| Create | `wwwroot/manifest.json` |
| Create | `wwwroot/service-worker.js` |
| Create | `wwwroot/offline.html` |
| Create | `wwwroot/icons/icon-192.png` (placeholder) |
| Create | `wwwroot/icons/icon-512.png` (placeholder) |
| Create | `wwwroot/icons/icon-512-maskable.png` (placeholder) |
