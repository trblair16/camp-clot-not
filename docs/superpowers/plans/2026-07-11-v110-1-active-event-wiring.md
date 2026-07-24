# v1.1.0 Sub-project 1: Active-Event Wiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing `/admin/events` "Active" toggle actually control which event's data every page loads, by replacing all hardcoded `SeedService.Id.EventCcn2026` runtime references with a resolved active event.

**Architecture:** One new scoped service, `ActiveEventService`, wraps a cached lookup of `Events.FirstOrDefault(e => e.IsActive)`. Every page that currently hardcodes the CCN 2026 GUID injects this service and resolves the event ID at the top of whichever method needs it, instead of referencing the constant. `SeedService` itself is untouched — it must keep using the literal GUID to stay idempotent.

**Tech Stack:** Blazor Server (.NET 8), EF Core, `IMemoryCache` (already registered via `AddMemoryCache()` in `Program.cs:53`).

## Global Constraints

- Follow the existing `ScheduleService` caching pattern exactly: primary-constructor DI (`IDbContextFactory<AppDbContext> factory, IMemoryCache cache`), `cache.GetOrCreateAsync(key, async entry => {...})` with a short `AbsoluteExpirationRelativeToNow`, explicit `cache.Remove(key)` on write.
- This codebase's `Services/*.cs` classes are **plain concrete classes with no interface** (see `ScheduleService`, `ThemeService`, `GroupService` — all registered in `Program.cs` as `AddScoped<ConcreteType>()`, never `AddScoped<IType, ConcreteType>()`). `ActiveEventService` follows the same convention: no `IActiveEventService` interface.
- `SeedService.cs`'s 8 occurrences of `Id.EventCcn2026` (lines 326, 345, 370, 471, 489, 537, 671, 675) are **not** touched by this plan — seeding must stay pinned to the literal GUID.
- There is no automated test suite in this project (manual testing only, per `CLAUDE.md`). Every task's verification step is `dotnet build` (must succeed) plus a manual functional check described in the step.
- Never use PowerShell for source file text edits in this repo — always use the Edit tool (see `CLAUDE.md` Pitfall #1, UTF-8/emoji corruption risk).
- Always use synchronous `DbFactory.CreateDbContext()`, never `CreateDbContextAsync()` (see `CLAUDE.md` Pitfall #14).

---

### Task 1: Create `ActiveEventService`

**Files:**
- Create: `CampClotNot/Services/ActiveEventService.cs`

**Interfaces:**
- Produces: `ActiveEventService.GetActiveEventAsync() : Task<Event?>`, `ActiveEventService.GetActiveEventIdAsync() : Task<Guid>`, `ActiveEventService.InvalidateCache() : void`. Every later task in this plan consumes these three members.

- [ ] **Step 1: Write the service**

```csharp
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class ActiveEventService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private const string CacheKey = "active-event";

    public async Task<Event?> GetActiveEventAsync()
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            using var db = factory.CreateDbContext();
            return await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.IsActive);
        });
    }

    public async Task<Guid> GetActiveEventIdAsync()
    {
        var ev = await GetActiveEventAsync();
        if (ev is null)
            throw new InvalidOperationException(
                "No active event is configured. An Admin must mark an event as Active on /admin/events.");
        return ev.EventId;
    }

    public void InvalidateCache() => cache.Remove(CacheKey);
}
```

- [ ] **Step 2: Build to verify it compiles**

Run (from `CampClotNot/`): `dotnet build`
Expected: `Build succeeded.` — no errors referencing `ActiveEventService`.

- [ ] **Step 3: Commit**

```bash
git add CampClotNot/Services/ActiveEventService.cs
git commit -m "feat: add ActiveEventService for resolving the active event"
```

---

### Task 2: Register the service and wire cache invalidation

**Files:**
- Modify: `CampClotNot/Program.cs:61` (add DI registration)
- Modify: `CampClotNot/Pages/Admin/Events.razor` (inject service, invalidate cache on save)

**Interfaces:**
- Consumes: `ActiveEventService` from Task 1.

- [ ] **Step 1: Register the service in `Program.cs`**

In `CampClotNot/Program.cs`, find this line (currently line 61):

```csharp
    builder.Services.AddSingleton<ThemeService>();   // one active theme per app instance
```

Add immediately after it:

```csharp
    builder.Services.AddSingleton<ThemeService>();   // one active theme per app instance
    builder.Services.AddScoped<ActiveEventService>();
```

- [ ] **Step 2: Inject the service into `Events.razor`**

In `CampClotNot/Pages/Admin/Events.razor`, find the top `@inject` block (currently lines 3-4):

```razor
@inject IDbContextFactory<AppDbContext> DbFactory
@using CampClotNot.Data
```

Add an injection line:

```razor
@inject IDbContextFactory<AppDbContext> DbFactory
@inject ActiveEventService ActiveEventSvc
@using CampClotNot.Data
```

- [ ] **Step 3: Invalidate the cache after save**

In the same file, find `SaveAsync()` (currently ends around line 303-307):

```csharp
        await db.SaveChangesAsync();
        ResetForm();
        _mobileFormOpen = false;
        await Reload();
    }
```

Change to:

```csharp
        await db.SaveChangesAsync();
        ActiveEventSvc.InvalidateCache();
        ResetForm();
        _mobileFormOpen = false;
        await Reload();
    }
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add CampClotNot/Program.cs "CampClotNot/Pages/Admin/Events.razor"
git commit -m "feat: register ActiveEventService, invalidate cache on event save"
```

---

### Task 3: Convert the 5 game pages (static-field pattern)

These 5 files share the exact same shape: a `private static readonly Guid EventId = SeedService.Id.EventCcn2026;` field, computed once at class load, referenced throughout the file. Since it can no longer be `static readonly` (resolving it requires an async DB/cache call), it becomes a plain instance field populated at the start of `OnInitializedAsync`.

**Files:**
- Modify: `CampClotNot/Pages/Board.razor`
- Modify: `CampClotNot/Pages/BoardDisplay.razor`
- Modify: `CampClotNot/Pages/MiniGames.razor`
- Modify: `CampClotNot/Pages/MiniGamesDisplay.razor`
- Modify: `CampClotNot/Pages/Admin/Games.razor`

**Interfaces:**
- Consumes: `ActiveEventService.GetActiveEventIdAsync() : Task<Guid>` from Task 1.

- [ ] **Step 1: `Board.razor`**

Add to the `@inject` block (currently lines 3-7, after `@inject BoardService BoardSvc`):

```razor
@inject BoardService BoardSvc
@inject ActiveEventService ActiveEventSvc
```

Replace line 241:

```csharp
    private static readonly Guid EventId = SeedService.Id.EventCcn2026;
```

with:

```csharp
    private Guid EventId;
```

Find `OnInitializedAsync` (currently lines 252-260):

```csharp
    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        _isAdmin = auth.User.IsInRole(nameof(Role.Admin));

        _spaces    = await BoardSvc.GetSpacesAsync(EventId);
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        EventId = await ActiveEventSvc.GetActiveEventIdAsync();

        var auth = await AuthState.GetAuthenticationStateAsync();
        _isAdmin = auth.User.IsInRole(nameof(Role.Admin));

        _spaces    = await BoardSvc.GetSpacesAsync(EventId);
```

Every other usage of `EventId` later in the file (lines 259, 297, 307, 338, 355, 398) stays exactly as-is — they now read the instance field instead of the static one.

- [ ] **Step 2: `BoardDisplay.razor`**

Add to the `@inject` block (currently lines 4-7, after `@inject BoardService BoardSvc`):

```razor
@inject BoardService BoardSvc
@inject ActiveEventService ActiveEventSvc
```

Replace line 200:

```csharp
    private static readonly Guid EventId = SeedService.Id.EventCcn2026;
```

with:

```csharp
    private Guid EventId;
```

Find `OnInitializedAsync` (currently lines 202-206):

```csharp
    protected override async Task OnInitializedAsync()
    {
        _spaces = await BoardSvc.GetSpacesAsync(EventId);
        await RefreshAsync();
        _loading = false;
    }
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        EventId = await ActiveEventSvc.GetActiveEventIdAsync();
        _spaces = await BoardSvc.GetSpacesAsync(EventId);
        await RefreshAsync();
        _loading = false;
    }
```

- [ ] **Step 3: `MiniGames.razor`**

Add to the `@inject` block (currently lines 3-4, after `@inject MiniGameService MiniGameSvc`):

```razor
@inject MiniGameService MiniGameSvc
@inject ActiveEventService ActiveEventSvc
```

Replace line 173:

```csharp
    private static readonly Guid EventId = SeedService.Id.EventCcn2026;
```

with:

```csharp
    private Guid EventId;
```

Find `OnInitializedAsync` (currently lines 175-179):

```csharp
    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        _loading = false;
    }
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        EventId = await ActiveEventSvc.GetActiveEventIdAsync();
        await LoadAsync();
        _loading = false;
    }
```

`LoadAsync()` (lines 181-189) already reads the `EventId` field and needs no further change — it will now see the resolved value since `OnInitializedAsync` sets it first.

- [ ] **Step 4: `MiniGamesDisplay.razor`**

Add to the `@inject` block (currently lines 4-5, after `@inject MiniGameService MiniGameSvc`):

```razor
@inject MiniGameService MiniGameSvc
@inject ActiveEventService ActiveEventSvc
```

Replace line 112:

```csharp
    private static readonly Guid EventId = SeedService.Id.EventCcn2026;
```

with:

```csharp
    private Guid EventId;
```

Find `OnInitializedAsync` (currently lines 114-118):

```csharp
    protected override async Task OnInitializedAsync()
    {
        _activities = await MiniGameSvc.GetSpinnerActivitiesAsync(EventId);
        _loading    = false;
    }
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        EventId     = await ActiveEventSvc.GetActiveEventIdAsync();
        _activities = await MiniGameSvc.GetSpinnerActivitiesAsync(EventId);
        _loading    = false;
    }
```

- [ ] **Step 5: `Admin/Games.razor`**

Add to the `@inject` block (currently lines 3-6, after `@inject BoardService BoardSvc`):

```razor
@inject BoardService BoardSvc
@inject ActiveEventService ActiveEventSvc
```

Replace line 435:

```csharp
    private static readonly Guid EventId = SeedService.Id.EventCcn2026;
```

with:

```csharp
    private Guid EventId;
```

Find `OnInitializedAsync` (currently lines 437-444) — **this one matters**: three loaders run in parallel via `Task.WhenAll` and all three read `EventId`, so it must be resolved *before* the `WhenAll` call, not inside any of the parallel tasks:

```csharp
    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            LoadBoardDataAsync(),
            LoadMiniGameDataAsync(),
            LoadBowserDataAsync()
        );
    }
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        EventId = await ActiveEventSvc.GetActiveEventIdAsync();
        await Task.WhenAll(
            LoadBoardDataAsync(),
            LoadMiniGameDataAsync(),
            LoadBowserDataAsync()
        );
    }
```

Every other usage of `EventId` later in the file (lines 448, 450, 455, 456, 462, 463, 474, 475, 487, 488, 501, 502, 514, 575, 581, 597) stays exactly as-is.

- [ ] **Step 6: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 7: Manual verification**

Run the app (`dotnet run` from `CampClotNot/`), log in as the seeded dev admin, and confirm each converted page still loads and behaves identically to before the change (CCN 2026 is still the only event, still active, so behavior must be unchanged):
- `/board` — board renders, positions load
- `/board/display` — projector view renders
- `/minigames` — spinner page loads, activities/scripts present
- `/minigames/display` — projector view renders
- `/admin/games` — Board Spaces / Block Hit Scripts / Mini-Game Scripts / Bowser tabs all load their existing data

- [ ] **Step 8: Commit**

```bash
git add CampClotNot/Pages/Board.razor CampClotNot/Pages/BoardDisplay.razor CampClotNot/Pages/MiniGames.razor CampClotNot/Pages/MiniGamesDisplay.razor "CampClotNot/Pages/Admin/Games.razor"
git commit -m "feat: resolve active event on game pages instead of hardcoded CCN 2026"
```

---

### Task 4: Convert `BowserEvent.razor`

**Files:**
- Modify: `CampClotNot/Pages/BowserEvent.razor`

- [ ] **Step 1: Inject the service**

Add to the `@inject` block (currently lines 3-5, after `@inject BowserEventService BowserSvc`):

```razor
@inject BowserEventService BowserSvc
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 2: Resolve in `OnInitializedAsync`**

Find (currently lines 105-111):

```csharp
        var authState = await Auth.GetAuthenticationStateAsync();
        _userName = authState.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Admin";
        _groups = await GroupSvc.GetAllAsync();
        _scripts = await BowserSvc.GetScriptsAsync(SeedService.Id.EventCcn2026);
        _loading = false;
    }
```

Change to:

```csharp
        var authState = await Auth.GetAuthenticationStateAsync();
        _userName = authState.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Admin";
        _eventId = await ActiveEventSvc.GetActiveEventIdAsync();
        _groups = await GroupSvc.GetAllAsync();
        _scripts = await BowserSvc.GetScriptsAsync(_eventId);
        _loading = false;
    }
```

Add the backing field near the other private fields at the top of `@code` (wherever `_userName` is declared):

```csharp
    private Guid _eventId;
```

- [ ] **Step 3: Update the roll handler**

Find (currently line 138):

```csharp
        var (faceIndex, face) = await BowserSvc.RollAsync(group.GroupId, SeedService.Id.EventCcn2026, _userName, _selectedScriptId);
```

Change to:

```csharp
        var (faceIndex, face) = await BowserSvc.RollAsync(group.GroupId, _eventId, _userName, _selectedScriptId);
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 5: Manual verification**

Navigate to `/bowser-event` (or the route configured for `BowserEvent.razor`), confirm scripts load and a die roll still works end-to-end.

- [ ] **Step 6: Commit**

```bash
git add CampClotNot/Pages/BowserEvent.razor
git commit -m "feat: resolve active event on BowserEvent page"
```

---

### Task 5: Convert `Admin/Activities.razor` and `Admin/ScheduleItemTypes.razor`

**Files:**
- Modify: `CampClotNot/Pages/Admin/Activities.razor`
- Modify: `CampClotNot/Pages/Admin/ScheduleItemTypes.razor`

- [ ] **Step 1: `Activities.razor` — inject the service**

Add to the `@inject` block (currently lines 3-5, after `@inject MiniGameService MiniGameSvc`):

```razor
@inject MiniGameService MiniGameSvc
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 2: `Activities.razor` — `Reload()`**

Find (currently lines 174-176):

```csharp
    private async Task Reload()
    {
        _activities = await MiniGameSvc.GetActivitiesAsync(SeedService.Id.EventCcn2026);
```

Change to:

```csharp
    private async Task Reload()
    {
        var eventId = await ActiveEventSvc.GetActiveEventIdAsync();
        _activities = await MiniGameSvc.GetActivitiesAsync(eventId);
```

- [ ] **Step 3: `Activities.razor` — `SaveAsync()`**

Find (currently lines 196-199):

```csharp
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_fName)) return;
        await MiniGameSvc.UpsertActivityAsync(_editingId, SeedService.Id.EventCcn2026, _fName.Trim(), _fDescription.Trim(), _fLocationId);
```

Change to:

```csharp
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_fName)) return;
        var eventId = await ActiveEventSvc.GetActiveEventIdAsync();
        await MiniGameSvc.UpsertActivityAsync(_editingId, eventId, _fName.Trim(), _fDescription.Trim(), _fLocationId);
```

- [ ] **Step 4: `ScheduleItemTypes.razor` — inject the service**

Add to the `@inject` block (currently lines 4-6, after `@inject ScheduleItemTypeService ScheduleItemTypeSvc`):

```razor
@inject ScheduleItemTypeService ScheduleItemTypeSvc
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 5: `ScheduleItemTypes.razor` — fallback resolution**

Find (currently lines 222-225):

```csharp
        _events = await db.Events.OrderByDescending(e => e.EffDate).ToListAsync();
        _selectedEventId = _events.FirstOrDefault(e => e.IsActive)?.EventId
                        ?? _events.FirstOrDefault()?.EventId
                        ?? SeedService.Id.EventCcn2026;
```

Change to:

```csharp
        _events = await db.Events.OrderByDescending(e => e.EffDate).ToListAsync();
        _selectedEventId = _events.FirstOrDefault(e => e.IsActive)?.EventId
                        ?? _events.FirstOrDefault()?.EventId
                        ?? await ActiveEventSvc.GetActiveEventIdAsync();
```

This line already prefers whichever event has `IsActive == true` from the already-loaded `_events` list; only the last-resort fallback (used solely if `_events` is completely empty) changes.

- [ ] **Step 6: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 7: Manual verification**

Visit `/admin/activities` — list loads, create/edit an activity still works. Visit `/admin/schedule-item-types` — page loads with CCN 2026 pre-selected.

- [ ] **Step 8: Commit**

```bash
git add "CampClotNot/Pages/Admin/Activities.razor" "CampClotNot/Pages/Admin/ScheduleItemTypes.razor"
git commit -m "feat: resolve active event on Activities and ScheduleItemTypes admin pages"
```

---

### Task 6: Convert `Admin/Schedule.razor`

**Files:**
- Modify: `CampClotNot/Pages/Admin/Schedule.razor`

- [ ] **Step 1: Inject the service**

Add an injection line near the other injected services at the top of the file.

```razor
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 2: `OnInitializedAsync`**

Find (currently lines 412-434, condensed to the relevant lines):

```csharp
    protected override async Task OnInitializedAsync()
    {
        var authState = await Auth.GetAuthenticationStateAsync();
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);

        using var dbEvent = DbFactory.CreateDbContext();
        var campEvent = await dbEvent.Events.FindAsync(SeedService.Id.EventCcn2026);
        if (campEvent is not null)
        {
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        var authState = await Auth.GetAuthenticationStateAsync();
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);

        _eventId = await ActiveEventSvc.GetActiveEventIdAsync();
        var campEvent = await ActiveEventSvc.GetActiveEventAsync();
        if (campEvent is not null)
        {
```

Add the backing field near the other private fields at the top of `@code`:

```csharp
    private Guid _eventId;
```

Find the rest of the same method (currently lines 426, 430, 434):

```csharp
        _scheduleItemTypes = await ScheduleItemTypeSvc.GetForEventAsync(SeedService.Id.EventCcn2026);
        _fTypeId = _scheduleItemTypes.FirstOrDefault()?.ScheduleItemTypeId ?? Guid.Empty;

        _locations  = await LocationSvc.GetAllAsync();
        _activities = await MiniGameSvc.GetActivitiesAsync(SeedService.Id.EventCcn2026);

        using var dbGroups = DbFactory.CreateDbContext();
        _groups = await dbGroups.Groups
            .Where(g => g.EventId == SeedService.Id.EventCcn2026)
```

Change to:

```csharp
        _scheduleItemTypes = await ScheduleItemTypeSvc.GetForEventAsync(_eventId);
        _fTypeId = _scheduleItemTypes.FirstOrDefault()?.ScheduleItemTypeId ?? Guid.Empty;

        _locations  = await LocationSvc.GetAllAsync();
        _activities = await MiniGameSvc.GetActivitiesAsync(_eventId);

        using var dbGroups = DbFactory.CreateDbContext();
        _groups = await dbGroups.Groups
            .Where(g => g.EventId == _eventId)
```

- [ ] **Step 3: `Reload()`**

Find (currently lines 443-445):

```csharp
    private async Task Reload()
    {
        _events  = await ScheduleSvc.GetByEventAsync(SeedService.Id.EventCcn2026);
```

Change to:

```csharp
    private async Task Reload()
    {
        _events  = await ScheduleSvc.GetByEventAsync(_eventId);
```

- [ ] **Step 4: `SaveAsync()`**

Find (currently lines 523, 541-544):

```csharp
        var isPresentation = SelectedTypeSystemName == "Presentation";
        var dto = new ScheduleItemDto(
            _editingId,
            SeedService.Id.EventCcn2026,
```

Change to:

```csharp
        var isPresentation = SelectedTypeSystemName == "Presentation";
        var dto = new ScheduleItemDto(
            _editingId,
            _eventId,
```

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 6: Manual verification**

Visit `/admin/schedule` — list loads, min/max date constraints still reflect CCN 2026's dates, creating and editing a schedule item still works.

- [ ] **Step 7: Commit**

```bash
git add "CampClotNot/Pages/Admin/Schedule.razor"
git commit -m "feat: resolve active event on Admin Schedule page"
```

---

### Task 7: Convert `Admin/Staff.razor` and `Admin/Sponsors.razor`

**Files:**
- Modify: `CampClotNot/Pages/Admin/Staff.razor`
- Modify: `CampClotNot/Pages/Admin/Sponsors.razor`

- [ ] **Step 1: `Staff.razor` — inject the service**

Add to the `@inject` block (currently lines 3-4, after `@inject StaffDirectoryService StaffSvc`):

```razor
@inject StaffDirectoryService StaffSvc
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 2: `Staff.razor` — `Reload()`**

Find (currently lines 230-232):

```csharp
    private async Task Reload()
    {
        _members = await StaffSvc.GetAllAsync(SeedService.Id.EventCcn2026);
```

Change to:

```csharp
    private async Task Reload()
    {
        _members = await StaffSvc.GetAllAsync(await ActiveEventSvc.GetActiveEventIdAsync());
```

- [ ] **Step 3: `Staff.razor` — the new-member constructor**

Find (currently lines 271-274):

```csharp
        var member = new StaffMember
        {
            StaffMemberId      = _editingId == Guid.Empty ? Guid.Empty : _editingId,
            CampEventId        = SeedService.Id.EventCcn2026,
```

Change to:

```csharp
        var eventId = await ActiveEventSvc.GetActiveEventIdAsync();
        var member = new StaffMember
        {
            StaffMemberId      = _editingId == Guid.Empty ? Guid.Empty : _editingId,
            CampEventId        = eventId,
```

Confirm the enclosing method is declared `async Task` (it must already be, since it awaits elsewhere) so the added `await` is valid.

- [ ] **Step 4: `Sponsors.razor` — inject the service**

Add to the `@inject` block (currently lines 3-4, after `@inject SponsorService SponsorSvc`):

```razor
@inject SponsorService SponsorSvc
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 5: `Sponsors.razor` — `Reload()`**

Find (currently lines 228-230):

```csharp
    private async Task Reload()
    {
        _sponsors = await SponsorSvc.GetAllForEventAsync(Services.SeedService.Id.EventCcn2026);
```

Change to:

```csharp
    private async Task Reload()
    {
        _sponsors = await SponsorSvc.GetAllForEventAsync(await ActiveEventSvc.GetActiveEventIdAsync());
```

- [ ] **Step 6: `Sponsors.razor` — the upsert constructor**

Find (currently lines 289-292):

```csharp
        await SponsorSvc.UpsertAsync(new Sponsor
        {
            SponsorId        = _editingId,
            EventId          = Services.SeedService.Id.EventCcn2026,
```

Change to:

```csharp
        var eventId = await ActiveEventSvc.GetActiveEventIdAsync();
        await SponsorSvc.UpsertAsync(new Sponsor
        {
            SponsorId        = _editingId,
            EventId          = eventId,
```

- [ ] **Step 7: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 8: Manual verification**

Visit `/admin/staff` — list loads, add/edit a staff member still works. Visit `/admin/sponsors` — list loads, add/edit a sponsor still works.

- [ ] **Step 9: Commit**

```bash
git add "CampClotNot/Pages/Admin/Staff.razor" "CampClotNot/Pages/Admin/Sponsors.razor"
git commit -m "feat: resolve active event on Admin Staff and Sponsors pages"
```

---

### Task 8: Convert `Hub/Schedule.razor`

**Files:**
- Modify: `CampClotNot/Pages/Hub/Schedule.razor`

- [ ] **Step 1: Inject the service**

Add an injection line near the other injected services at the top of the file.

```razor
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 2: `OnInitializedAsync`**

Find (currently lines 664-680, condensed to the relevant lines):

```csharp
    protected override async Task OnInitializedAsync()
    {
        var authState = await Auth.GetAuthenticationStateAsync();
        _canEdit        = authState.User.IsInRole("Admin");
        _isAdminOrStaff = authState.User.IsInRole("Admin") || authState.User.IsInRole("Staff");
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);

        // Run all independent reference-data queries in parallel
        using var dbEvent  = DbFactory.CreateDbContext();
        using var dbGroups = DbFactory.CreateDbContext();
        var campEventTask  = dbEvent.Events.FindAsync(SeedService.Id.EventCcn2026).AsTask();
        var sitTask        = ScheduleItemTypeSvc.GetForEventAsync(SeedService.Id.EventCcn2026);
        var locsTask       = LocationSvc.GetAllAsync();
        var actsTask       = MiniGameSvc.GetActivitiesAsync(SeedService.Id.EventCcn2026);
        var groupsTask     = dbGroups.Groups
            .Where(g => g.EventId == SeedService.Id.EventCcn2026)
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        var authState = await Auth.GetAuthenticationStateAsync();
        _canEdit        = authState.User.IsInRole("Admin");
        _isAdminOrStaff = authState.User.IsInRole("Admin") || authState.User.IsInRole("Staff");
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);

        _eventId = await ActiveEventSvc.GetActiveEventIdAsync();

        // Run all independent reference-data queries in parallel
        using var dbGroups = DbFactory.CreateDbContext();
        var campEventTask  = ActiveEventSvc.GetActiveEventAsync();
        var sitTask        = ScheduleItemTypeSvc.GetForEventAsync(_eventId);
        var locsTask       = LocationSvc.GetAllAsync();
        var actsTask       = MiniGameSvc.GetActivitiesAsync(_eventId);
        var groupsTask     = dbGroups.Groups
            .Where(g => g.EventId == _eventId)
```

Note `dbEvent` is removed entirely (it was only used for the `campEventTask` line, which now goes through `ActiveEventSvc` — already cached, no separate `DbContext` needed for it). `campEventTask` is now `Task<Event?>` instead of `Task<Event?>` via `FindAsync` — same resulting type, so anything awaiting it later in the method (`campEventTask.Result` per the design spec's earlier finding, or `await campEventTask`) needs no further change.

Add the backing field near the other private fields at the top of `@code`:

```csharp
    private Guid _eventId;
```

- [ ] **Step 3: `LoadEvents()`**

Find (currently lines 715-717):

```csharp
    private async Task LoadEvents()
    {
        var events = await ScheduleSvc.GetByEventAsync(SeedService.Id.EventCcn2026);
```

Change to:

```csharp
    private async Task LoadEvents()
    {
        var events = await ScheduleSvc.GetByEventAsync(_eventId);
```

- [ ] **Step 4: `SaveEvent()`**

Find (currently lines 806-821):

```csharp
    private async Task SaveEvent()
    {
        if (string.IsNullOrWhiteSpace(_fTitle) || _fDate is null || string.IsNullOrWhiteSpace(_fStartStr)) return;
        if (!DateTime.TryParse(_fStartStr, out var startDt)) return;

        var assignments = _fAssignments
            .Where(a => a.ActivityId.HasValue || a.LocationId.HasValue || !string.IsNullOrEmpty(a.Note))
            .Select(a => new GroupAssignmentDto(a.GroupId, a.ActivityId, a.LocationId, a.Note))
            .ToList();

        var isPresentation = SelectedTypeSystemName == "Presentation";
        TimeOnly? end = !string.IsNullOrWhiteSpace(_fEndStr) && DateTime.TryParse(_fEndStr, out var endDt)
            ? TimeOnly.FromDateTime(endDt) : null;
        var dto = new ScheduleItemDto(
            _editingId,
            SeedService.Id.EventCcn2026,
```

Change to:

```csharp
    private async Task SaveEvent()
    {
        if (string.IsNullOrWhiteSpace(_fTitle) || _fDate is null || string.IsNullOrWhiteSpace(_fStartStr)) return;
        if (!DateTime.TryParse(_fStartStr, out var startDt)) return;

        var assignments = _fAssignments
            .Where(a => a.ActivityId.HasValue || a.LocationId.HasValue || !string.IsNullOrEmpty(a.Note))
            .Select(a => new GroupAssignmentDto(a.GroupId, a.ActivityId, a.LocationId, a.Note))
            .ToList();

        var isPresentation = SelectedTypeSystemName == "Presentation";
        TimeOnly? end = !string.IsNullOrWhiteSpace(_fEndStr) && DateTime.TryParse(_fEndStr, out var endDt)
            ? TimeOnly.FromDateTime(endDt) : null;
        var dto = new ScheduleItemDto(
            _editingId,
            _eventId,
```

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.` Pay attention to the `campEventTask` type — if the compiler complains about `AsTask()` no longer being called on something, confirm `ActiveEventSvc.GetActiveEventAsync()` already returns a `Task<Event?>` directly (it does — no `.AsTask()` needed since it's not coming from `FindAsync`'s `ValueTask`).

- [ ] **Step 6: Manual verification**

Visit `/hub/schedule` — day tabs load, today's schedule renders, min/max date constraints still reflect CCN 2026's dates, creating/editing/copy-to-day all still work.

- [ ] **Step 7: Commit**

```bash
git add "CampClotNot/Pages/Hub/Schedule.razor"
git commit -m "feat: resolve active event on Hub Schedule page"
```

---

### Task 9: Convert `Hub/HubSubNav.razor`, `Hub/Staff.razor`, `Hub/Sponsors.razor`, `Hub/Incidents.razor`, `Hub/Info.razor`

**Files:**
- Modify: `CampClotNot/Pages/Hub/HubSubNav.razor`
- Modify: `CampClotNot/Pages/Hub/Staff.razor`
- Modify: `CampClotNot/Pages/Hub/Sponsors.razor`
- Modify: `CampClotNot/Pages/Hub/Incidents.razor`
- Modify: `CampClotNot/Pages/Hub/Info.razor`

- [ ] **Step 1: `HubSubNav.razor` — inject the service**

Add to the `@inject` block (currently lines 1-2):

```razor
@inject NavigationManager Nav
@inject AuthenticationStateProvider Auth
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 2: `HubSubNav.razor` — `SubmitAsync()`**

Find (currently lines 274-293, condensed):

```csharp
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_fPersonsInvolved) || string.IsNullOrWhiteSpace(_fDescription))
        {
            _errorMsg = "Persons Involved and Description are required.";
            return;
        }

        _submitting = true;
        _errorMsg   = "";

        var state  = await Auth.GetAuthenticationStateAsync();
        var user   = state.User;
        var userId = Guid.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : Guid.Empty;
        var name   = user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
        var role   = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Unknown";

        await IncidentSvc.SubmitAsync(new IncidentReport
        {
            EventId                = Services.SeedService.Id.EventCcn2026,
```

Change to:

```csharp
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_fPersonsInvolved) || string.IsNullOrWhiteSpace(_fDescription))
        {
            _errorMsg = "Persons Involved and Description are required.";
            return;
        }

        _submitting = true;
        _errorMsg   = "";

        var state  = await Auth.GetAuthenticationStateAsync();
        var user   = state.User;
        var userId = Guid.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : Guid.Empty;
        var name   = user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
        var role   = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Unknown";
        var eventId = await ActiveEventSvc.GetActiveEventIdAsync();

        await IncidentSvc.SubmitAsync(new IncidentReport
        {
            EventId                = eventId,
```

- [ ] **Step 3: `Hub/Staff.razor` — inject the service**

Add to the `@inject` block (currently lines 3-5, after `@inject StaffDirectoryService StaffSvc`):

```razor
@inject StaffDirectoryService StaffSvc
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 4: `Hub/Staff.razor` — `Reload()`**

Find (currently lines 196-198):

```csharp
    private async Task Reload()
    {
        var eventId = SeedService.Id.EventCcn2026;
```

Change to:

```csharp
    private async Task Reload()
    {
        var eventId = await ActiveEventSvc.GetActiveEventIdAsync();
```

- [ ] **Step 5: `Hub/Staff.razor` — `OpenAdd()`**

Find (currently lines 206-208):

```csharp
    private void OpenAdd()
    {
        _form = new() { CampEventId = SeedService.Id.EventCcn2026, AvatarEmoji = "👤", IsVisible = true };
```

`OpenAdd()` is synchronous today, but resolving the active event needs an `await`. Change the method to async and update its callers:

```csharp
    private async Task OpenAdd()
    {
        _form = new() { CampEventId = await ActiveEventSvc.GetActiveEventIdAsync(), AvatarEmoji = "👤", IsVisible = true };
```

Search the rest of `Hub/Staff.razor` for `OpenAdd(` — any `@onclick="OpenAdd"` binding still works unchanged with an async `Task`-returning method (Blazor supports this natively); a call site like `OpenAdd();` without `await` needs to become `await OpenAdd();` if one exists inside another async method — check for this during Step 6's build/manual pass.

- [ ] **Step 6: `Hub/Staff.razor` — `ImportUser()`**

Find (currently lines 308-310):

```csharp
    private async Task ImportUser(Guid userId)
    {
        await StaffSvc.ImportUserAsync(SeedService.Id.EventCcn2026, userId);
```

Change to:

```csharp
    private async Task ImportUser(Guid userId)
    {
        await StaffSvc.ImportUserAsync(await ActiveEventSvc.GetActiveEventIdAsync(), userId);
```

- [ ] **Step 7: `Hub/Sponsors.razor` — inject the service**

Add to the `@inject` block (currently line 3):

```razor
@inject SponsorService SponsorSvc
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 8: `Hub/Sponsors.razor` — `OnInitializedAsync`**

Find (currently lines 95-97):

```csharp
    protected override async Task OnInitializedAsync()
    {
        _sponsors = await SponsorSvc.GetAllForEventAsync(Services.SeedService.Id.EventCcn2026);
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        _sponsors = await SponsorSvc.GetAllForEventAsync(await ActiveEventSvc.GetActiveEventIdAsync());
```

- [ ] **Step 9: `Hub/Incidents.razor` — inject the service**

Add to the `@inject` block (currently lines 3-5, after `@inject IncidentReportService IncidentSvc`):

```razor
@inject IncidentReportService IncidentSvc
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 10: `Hub/Incidents.razor` — `Reload()`**

Find (currently lines 110-112):

```csharp
    private async Task Reload()
    {
        _reports = await IncidentSvc.GetAllForEventAsync(Services.SeedService.Id.EventCcn2026);
```

Change to:

```csharp
    private async Task Reload()
    {
        _reports = await IncidentSvc.GetAllForEventAsync(await ActiveEventSvc.GetActiveEventIdAsync());
```

- [ ] **Step 11: `Hub/Info.razor` — inject the service**

Add an injection line near the other injected services at the top of the file:

```razor
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 12: `Hub/Info.razor` — the 5 call sites**

There are 5 occurrences (lines 304, 313, 349, 350, 374), each `await DocSvc.GetForEventAsync(SeedService.Id.EventCcn2026)` or `await DocSvc.AddAsync(SeedService.Id.EventCcn2026, ...)`, spread across 3 different methods. Resolve once per method and reuse. For the method containing lines 301-304:

```csharp
        _pages = await InfoSvc.GetAllAsync();
        UpdateActivePage();
        if (IsDocumentsPage)
            _documents = await DocSvc.GetForEventAsync(SeedService.Id.EventCcn2026);
```

Change to:

```csharp
        _pages = await InfoSvc.GetAllAsync();
        UpdateActivePage();
        if (IsDocumentsPage)
            _documents = await DocSvc.GetForEventAsync(await ActiveEventSvc.GetActiveEventIdAsync());
```

For the method containing lines 310-313 (same pattern, different method):

```csharp
        if (!_pages.Any()) return;
        UpdateActivePage();
        if (IsDocumentsPage)
            _documents = await DocSvc.GetForEventAsync(SeedService.Id.EventCcn2026);
```

Change to:

```csharp
        if (!_pages.Any()) return;
        UpdateActivePage();
        if (IsDocumentsPage)
            _documents = await DocSvc.GetForEventAsync(await ActiveEventSvc.GetActiveEventIdAsync());
```

For the method containing lines 346-350 (two occurrences, resolve once and reuse):

```csharp
        {
            if (string.IsNullOrWhiteSpace(_fDocTitle) || _fDocData is null) return;
            var docRoles = ComputeDocVisibleRoles();
            await DocSvc.AddAsync(SeedService.Id.EventCcn2026, _fDocTitle, _fDocFileName, _fDocData, "application/pdf", docRoles, _userId);
            _documents = await DocSvc.GetForEventAsync(SeedService.Id.EventCcn2026);
```

Change to:

```csharp
        {
            if (string.IsNullOrWhiteSpace(_fDocTitle) || _fDocData is null) return;
            var docRoles = ComputeDocVisibleRoles();
            var eventId = await ActiveEventSvc.GetActiveEventIdAsync();
            await DocSvc.AddAsync(eventId, _fDocTitle, _fDocFileName, _fDocData, "application/pdf", docRoles, _userId);
            _documents = await DocSvc.GetForEventAsync(eventId);
```

For the method containing line 374:

```csharp
        if (!await JS.InvokeAsync<bool>("confirm", "Delete this document?")) return;
        await DocSvc.DeleteAsync(documentId);
        _documents = await DocSvc.GetForEventAsync(SeedService.Id.EventCcn2026);
```

Change to:

```csharp
        if (!await JS.InvokeAsync<bool>("confirm", "Delete this document?")) return;
        await DocSvc.DeleteAsync(documentId);
        _documents = await DocSvc.GetForEventAsync(await ActiveEventSvc.GetActiveEventIdAsync());
```

- [ ] **Step 13: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.` If `Hub/Staff.razor`'s `OpenAdd` change surfaces a caller that isn't awaited, fix that call site to `await OpenAdd()` (its enclosing method must also be `async Task`, not `void`).

- [ ] **Step 14: Manual verification**

- Open any Hub page, submit an incident report via the FAB — confirm it saves.
- Visit `/hub/staff` — list loads, "Add Staff" and "Import from Users" still work.
- Visit `/hub/sponsors` — tile grid loads.
- Visit `/hub/incidents` — list loads (Admin/MedicalStaff only).
- Visit `/hub/info` on a page with document uploads — list loads, upload/delete still work.

- [ ] **Step 15: Commit**

```bash
git add "CampClotNot/Pages/Hub/HubSubNav.razor" "CampClotNot/Pages/Hub/Staff.razor" "CampClotNot/Pages/Hub/Sponsors.razor" "CampClotNot/Pages/Hub/Incidents.razor" "CampClotNot/Pages/Hub/Info.razor"
git commit -m "feat: resolve active event on remaining Hub pages"
```

---

### Task 10: Convert `Dashboard.razor`

**Files:**
- Modify: `CampClotNot/Pages/Dashboard.razor`

- [ ] **Step 1: Inject the service**

Add to the `@inject` block (currently lines 4-7, after `@inject SponsorService SponsorSvc`):

```razor
@inject SponsorService SponsorSvc
@inject ActiveEventService ActiveEventSvc
```

- [ ] **Step 2: `OnInitializedAsync`**

Find (currently lines 221-245):

```csharp
    protected override async Task OnInitializedAsync()
    {
        _sponsors = await SponsorSvc.GetAllForEventAsync(SeedService.Id.EventCcn2026);

        var today = CampTime.Today;
        using var db = DbFactory.CreateDbContext();
        var campEvent = await db.Events.FindAsync(SeedService.Id.EventCcn2026);

        if (campEvent is not null)
        {
            if (today < campEvent.EffDate)
            {
                _scheduleDay     = campEvent.EffDate;
                _showingFirstDay = true;
            }
            else
            {
                _scheduleDay = today <= campEvent.ExpDate ? today : campEvent.ExpDate;
            }
        }
        else
        {
            _scheduleDay = today;
        }
        _todayEvents = await ScheduleSvc.GetForDayAsync(SeedService.Id.EventCcn2026, _scheduleDay);
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        var eventId = await ActiveEventSvc.GetActiveEventIdAsync();
        _sponsors = await SponsorSvc.GetAllForEventAsync(eventId);

        var today = CampTime.Today;
        var campEvent = await ActiveEventSvc.GetActiveEventAsync();

        if (campEvent is not null)
        {
            if (today < campEvent.EffDate)
            {
                _scheduleDay     = campEvent.EffDate;
                _showingFirstDay = true;
            }
            else
            {
                _scheduleDay = today <= campEvent.ExpDate ? today : campEvent.ExpDate;
            }
        }
        else
        {
            _scheduleDay = today;
        }
        _todayEvents = await ScheduleSvc.GetForDayAsync(eventId, _scheduleDay);
```

The `using var db = DbFactory.CreateDbContext();` line is removed entirely — it was only used for the `FindAsync` call, which `ActiveEventSvc.GetActiveEventAsync()` now replaces. Leave the `@inject IDbContextFactory<AppDbContext> DbFactory` line in place in case other methods in this file still use `DbFactory`.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Manual verification**

Visit `/dashboard` — sponsors widget, today's schedule, and latest announcement all still render correctly.

- [ ] **Step 5: Commit**

```bash
git add CampClotNot/Pages/Dashboard.razor
git commit -m "feat: resolve active event on Dashboard page"
```

---

### Task 11: End-to-end verification and proof the wiring works

**Files:** none (verification only)

- [ ] **Step 1: Confirm no hardcoded references remain outside `SeedService`**

Run: `grep -rn "Id.EventCcn2026" CampClotNot/Pages/`
Expected: no output (zero matches). If anything remains, it was missed by an earlier task — go back and convert it following the same pattern.

- [ ] **Step 2: Full regression pass on CCN 2026**

With the app running and CCN 2026 still the only event (still Active), click through every page touched by Tasks 3-10 one more time in a single session: `/dashboard`, `/board`, `/board/display`, `/minigames`, `/minigames/display`, `/admin/games`, `/bowser-event`, `/admin/activities`, `/admin/schedule-item-types`, `/admin/schedule`, `/admin/staff`, `/admin/sponsors`, `/hub/schedule`, `/hub/staff`, `/hub/sponsors`, `/hub/incidents`, `/hub/info`. Everything must look and behave exactly as it did before this plan — this is the proof that resolving the active event dynamically is behaviorally equivalent to the old hardcoded constant, as long as only one event exists and it's active.

- [ ] **Step 3: Prove the toggle actually works**

On `/admin/events`, create a throwaway test event (e.g. "Wiring Test", any date range, any event type) and mark it Active. Save. Revisit 2-3 of the converted pages (e.g. `/hub/schedule`, `/dashboard`) and confirm they now show empty/different data scoped to the new event (no schedule items, no staff, etc. — since nothing has been created for it yet) rather than CCN 2026's data. This is the proof that the Active toggle now genuinely controls what the app shows.

- [ ] **Step 4: Restore CCN 2026 as the active event**

Back on `/admin/events`, edit CCN 2026 and mark it Active again (this automatically deactivates the test event per the existing `SaveAsync` invariant). Delete the "Wiring Test" event if the admin UI supports deletion; if not, leave it inactive — it does no harm sitting inactive in the events list, and can be deleted manually via `DELETE FROM "Events" WHERE "Name" = 'Wiring Test'` if needed later.

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "chore: v1.1.0 sub-project 1 complete — active-event wiring verified end-to-end" --allow-empty
```

(Use `--allow-empty` if Step 4 left no file changes to stage — this commit exists to mark the milestone in history.)
