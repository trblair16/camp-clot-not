# v1.1.0 Sub-project 2: Capability Gating Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let Vicki toggle 5 capabilities on/off per event from `/admin/events`, and make disabled capabilities genuinely unreachable (nav item hidden AND the underlying page blocked, not just hidden from nav) for the active event.

**Architecture:** A new `CapabilityService` (mirroring the existing `ScheduleItemTypeService` pattern exactly) provides a cached `IsEnabledAsync(eventId, Feature)` check. Every gated page calls it once in `OnInitializedAsync` and renders a "not available for this event" block instead of its normal content when disabled. `AppNav.razor` uses the same service to hide nav entries. `Admin/Events.razor` gets 5 checkboxes wired to the same underlying `EventCapability` join table.

**Tech Stack:** Blazor Server (.NET 8), EF Core, `IMemoryCache`.

## Global Constraints

- Follow `ScheduleItemTypeService`'s exact shape for `CapabilityService` — primary-constructor DI, `IMemoryCache`-backed reads, `cache.Remove(key)` on write. `EventCapability` has a **surrogate `EventCapabilityId` PK**, not a composite key like `EventScheduleItemType` — so lookups use `FirstOrDefaultAsync(e => e.EventId == x && e.CapabilityId == y)`, not `FindAsync(x, y)`.
- Capability scope, per product decision: `BoardGame` gates `/board` + `/board/display`. `MiniGameSpinner` gates `/minigames` + `/minigames/display`. `BowserEvent` (new) gates `/bowser` + `/bowser/display`. `CoinShop` gates `/transactions` (the coin/star ledger — the closest real feature to "coin shop"; no shop UI exists to build here). `Awards` (new) gates `/leaderboard/reveal` + `/leaderboard/reveal/display` (the "Awards Ceremony" leaderboard-reveal feature — closest real feature to "awards"; `AwardType`/`CamperAward` have no UI and are out of scope for this plan). `Announcements` and `Itinerary` (pre-existing `Feature` values) are **not** touched by this plan — out of scope.
- `SeedCapabilitiesAsync` and `SeedEventCapabilitiesAsync` currently use a blanket `if (await db.Capabilities.AnyAsync()) return;` guard — this means adding 2 new capability rows would silently no-op on any DB that already has the original 5 seeded (i.e. every existing dev/prod DB). Both methods must switch to the per-ID upsert pattern already used by `SeedActivityTypeCategoriesAsync` (check-then-add per ID, not blanket-skip).
- Disabled-page guard UI: a single reusable block, styled consistently with the rest of the app (Fredoka One heading, cream/neo-brutalist palette):
  ```razor
  <div style="text-align:center;padding:80px 20px;color:var(--text-light)">
      <p style="font-family:'Fredoka One',cursive;font-size:18px;margin:0 0 8px;color:var(--text-dark)">Not available for this event</p>
      <p style="font-size:14px">This feature isn't enabled for the current event.</p>
  </div>
  ```
- No automated test suite — verification is `dotnet build` succeeding plus manual/live-app checks.
- Never use PowerShell for source file text edits in this repo — always use the Edit tool.
- Always use synchronous `DbFactory.CreateDbContext()`, never `CreateDbContextAsync()`.

---

### Task 1: Extend `Feature` enum, fix seed guard pattern, seed 2 new capabilities

**Files:**
- Modify: `CampClotNot/Data/Enums.cs`
- Modify: `CampClotNot/Services/SeedService.cs`

- [ ] **Step 1: Add the two new `Feature` values**

In `CampClotNot/Data/Enums.cs`, find line 11:

```csharp
public enum Feature { BoardGame, CoinShop, MiniGameSpinner, Announcements, Itinerary }
```

Change to:

```csharp
public enum Feature { BoardGame, CoinShop, MiniGameSpinner, Announcements, Itinerary, BowserEvent, Awards }
```

- [ ] **Step 2: Add the two new capability GUID constants**

In `CampClotNot/Services/SeedService.cs`, find lines 35-40:

```csharp
        // Capabilities
        public static readonly Guid CapBoardGame       = new("00000005-0005-0005-0005-000000000001");
        public static readonly Guid CapCoinShop        = new("00000005-0005-0005-0005-000000000002");
        public static readonly Guid CapMiniGameSpinner = new("00000005-0005-0005-0005-000000000003");
        public static readonly Guid CapAnnouncements   = new("00000005-0005-0005-0005-000000000004");
        public static readonly Guid CapItinerary       = new("00000005-0005-0005-0005-000000000005");
```

Change to:

```csharp
        // Capabilities
        public static readonly Guid CapBoardGame       = new("00000005-0005-0005-0005-000000000001");
        public static readonly Guid CapCoinShop        = new("00000005-0005-0005-0005-000000000002");
        public static readonly Guid CapMiniGameSpinner = new("00000005-0005-0005-0005-000000000003");
        public static readonly Guid CapAnnouncements   = new("00000005-0005-0005-0005-000000000004");
        public static readonly Guid CapItinerary       = new("00000005-0005-0005-0005-000000000005");
        public static readonly Guid CapBowserEvent     = new("00000005-0005-0005-0005-000000000006");
        public static readonly Guid CapAwards          = new("00000005-0005-0005-0005-000000000007");
```

- [ ] **Step 3: Switch `SeedCapabilitiesAsync` to the per-ID upsert pattern**

Find `SeedCapabilitiesAsync` (lines 251-263):

```csharp
    private async Task SeedCapabilitiesAsync(AppDbContext db)
    {
        if (await db.Capabilities.AnyAsync()) return;
        db.Capabilities.AddRange(
            new Capability { CapabilityId = Id.CapBoardGame,       Name = "Board Game",        Description = "Mario Party-style board game with block hits", SystemName = nameof(Feature.BoardGame) },
            new Capability { CapabilityId = Id.CapCoinShop,        Name = "Coin Shop",         Description = "Shop where groups spend coins for rewards",     SystemName = nameof(Feature.CoinShop) },
            new Capability { CapabilityId = Id.CapMiniGameSpinner, Name = "Mini-Game Spinner", Description = "Pre-scripted evening mini-game selector",        SystemName = nameof(Feature.MiniGameSpinner) },
            new Capability { CapabilityId = Id.CapAnnouncements,   Name = "Announcements",     Description = "Real-time schedule announcements",              SystemName = nameof(Feature.Announcements) },
            new Capability { CapabilityId = Id.CapItinerary,       Name = "Itinerary",         Description = "Camp itinerary and schedule",                    SystemName = nameof(Feature.Itinerary) }
        );
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded Capabilities.");
    }
```

Change to:

```csharp
    private async Task SeedCapabilitiesAsync(AppDbContext db)
    {
        var defs = new[]
        {
            new { Id = Id.CapBoardGame,       Name = "Board Game",        Description = "Mario Party-style board game with block hits",  SystemName = nameof(Feature.BoardGame) },
            new { Id = Id.CapCoinShop,        Name = "Coin Shop",         Description = "Shop where groups spend coins for rewards",      SystemName = nameof(Feature.CoinShop) },
            new { Id = Id.CapMiniGameSpinner, Name = "Mini-Game Spinner", Description = "Pre-scripted evening mini-game selector",        SystemName = nameof(Feature.MiniGameSpinner) },
            new { Id = Id.CapAnnouncements,   Name = "Announcements",     Description = "Real-time schedule announcements",               SystemName = nameof(Feature.Announcements) },
            new { Id = Id.CapItinerary,       Name = "Itinerary",         Description = "Camp itinerary and schedule",                     SystemName = nameof(Feature.Itinerary) },
            new { Id = Id.CapBowserEvent,     Name = "Bowser Event",      Description = "Random die-roll event with projector display",   SystemName = nameof(Feature.BowserEvent) },
            new { Id = Id.CapAwards,          Name = "Awards",            Description = "Awards ceremony leaderboard reveal",              SystemName = nameof(Feature.Awards) }
        };

        foreach (var d in defs)
        {
            if (await db.Capabilities.AnyAsync(c => c.CapabilityId == d.Id)) continue;
            db.Capabilities.Add(new Capability { CapabilityId = d.Id, Name = d.Name, Description = d.Description, SystemName = d.SystemName });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded Capabilities.");
    }
```

- [ ] **Step 4: Switch `SeedEventCapabilitiesAsync` to the per-ID upsert pattern**

Find `SeedEventCapabilitiesAsync` (lines 338-350):

```csharp
    private async Task SeedEventCapabilitiesAsync(AppDbContext db)
    {
        if (await db.EventCapabilities.AnyAsync()) return;
        var capIds = new[] { Id.CapBoardGame, Id.CapCoinShop, Id.CapMiniGameSpinner, Id.CapAnnouncements, Id.CapItinerary };
        db.EventCapabilities.AddRange(capIds.Select(capId => new EventCapability
        {
            EventCapabilityId = Guid.NewGuid(),
            EventId           = Id.EventCcn2026,
            CapabilityId      = capId
        }));
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded EventCapabilities for CCN 2026.");
    }
```

Change to:

```csharp
    private async Task SeedEventCapabilitiesAsync(AppDbContext db)
    {
        var capIds = new[] { Id.CapBoardGame, Id.CapCoinShop, Id.CapMiniGameSpinner, Id.CapAnnouncements, Id.CapItinerary, Id.CapBowserEvent, Id.CapAwards };
        foreach (var capId in capIds)
        {
            if (await db.EventCapabilities.AnyAsync(e => e.EventId == Id.EventCcn2026 && e.CapabilityId == capId)) continue;
            db.EventCapabilities.Add(new EventCapability
            {
                EventCapabilityId = Guid.NewGuid(),
                EventId           = Id.EventCcn2026,
                CapabilityId      = capId
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded EventCapabilities for CCN 2026.");
    }
```

This keeps CCN 2026 with all 7 capabilities enabled (unchanged behavior — the 2 new ones default on for the existing event), matching the design spec's "CCN 2026 unaffected" requirement.

- [ ] **Step 5: Build to verify it compiles**

Run (from `CampClotNot/`): `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 6: Manual verification**

Run the app locally, confirm startup logs still show `"Seeded Capabilities."` and `"Seeded EventCapabilities for CCN 2026."` with no errors (the per-ID upsert runs against an already-seeded local DB, so this also proves the upsert-not-blanket-skip fix works against existing data, not just a fresh DB).

- [ ] **Step 7: Commit**

```bash
git add CampClotNot/Data/Enums.cs CampClotNot/Services/SeedService.cs
git commit -m "feat: add BowserEvent and Awards capabilities, fix seed upsert pattern"
```

---

### Task 2: Create `CapabilityService`

**Files:**
- Create: `CampClotNot/Services/CapabilityService.cs`

**Interfaces:**
- Consumes: `Feature` enum (Task 1), `Capability`/`EventCapability` entities (pre-existing).
- Produces: `CapabilityService.GetEnabledCapabilitiesAsync(Guid eventId) : Task<List<Capability>>`, `CapabilityService.IsEnabledAsync(Guid eventId, Feature feature) : Task<bool>`, `CapabilityService.SetEventCapabilityAsync(Guid eventId, Guid capabilityId, bool enabled) : Task`, `CapabilityService.InvalidateCache(Guid eventId) : void`. Every later task in this plan consumes `IsEnabledAsync` (page/nav gating) and Task 3 additionally consumes the other three members.

- [ ] **Step 1: Write the service**

```csharp
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class CapabilityService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private static string EventKey(Guid eventId) => $"cap.ev.{eventId}";

    public async Task<List<Capability>> GetEnabledCapabilitiesAsync(Guid eventId)
    {
        return await cache.GetOrCreateAsync(EventKey(eventId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            using var db = factory.CreateDbContext();
            return await db.EventCapabilities
                .Where(e => e.EventId == eventId)
                .Include(e => e.Capability)
                .Select(e => e.Capability)
                .ToListAsync();
        }) ?? [];
    }

    public async Task<bool> IsEnabledAsync(Guid eventId, Feature feature)
    {
        var enabled = await GetEnabledCapabilitiesAsync(eventId);
        return enabled.Any(c => c.SystemName == nameof(feature) || c.SystemName == feature.ToString());
    }

    public async Task SetEventCapabilityAsync(Guid eventId, Guid capabilityId, bool enabled)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.EventCapabilities
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.CapabilityId == capabilityId);
        if (enabled && existing is null)
        {
            db.EventCapabilities.Add(new EventCapability
            {
                EventCapabilityId = Guid.NewGuid(),
                EventId           = eventId,
                CapabilityId      = capabilityId
            });
        }
        else if (!enabled && existing is not null)
        {
            db.EventCapabilities.Remove(existing);
        }
        await db.SaveChangesAsync();
        cache.Remove(EventKey(eventId));
    }

    public void InvalidateCache(Guid eventId) => cache.Remove(EventKey(eventId));
}
```

Note on `IsEnabledAsync`: `feature.ToString()` and `nameof(feature)` produce the same string for an enum value passed as a parameter (both yield the member name, e.g. `"BoardGame"`) — this looks redundant but `nameof` on a parameter name isn't meaningful for runtime value comparison, so simplify: just use `c.SystemName == feature.ToString()`. Use this corrected version:

```csharp
    public async Task<bool> IsEnabledAsync(Guid eventId, Feature feature)
    {
        var enabled = await GetEnabledCapabilitiesAsync(eventId);
        return enabled.Any(c => c.SystemName == feature.ToString());
    }
```

- [ ] **Step 2: Register in `Program.cs`**

Find (currently, after Task 1 of sub-project 1 already added `ActiveEventService` — the exact line number shifted, search for it):

```csharp
    builder.Services.AddScoped<ActiveEventService>();
```

Add immediately after:

```csharp
    builder.Services.AddScoped<ActiveEventService>();
    builder.Services.AddScoped<CapabilityService>();
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CampClotNot/Services/CapabilityService.cs CampClotNot/Program.cs
git commit -m "feat: add CapabilityService for per-event feature gating"
```

---

### Task 3: Capability checkboxes on `/admin/events`

**Files:**
- Modify: `CampClotNot/Pages/Admin/Events.razor`

**Interfaces:**
- Consumes: `CapabilityService` (Task 2) — `GetEnabledCapabilitiesAsync`, `SetEventCapabilityAsync`, `InvalidateCache`.

- [ ] **Step 1: Inject the service**

Add to the top injection block (alongside the existing `@inject ActiveEventService ActiveEventSvc`):

```razor
@inject CapabilityService CapSvc
```

- [ ] **Step 2: Add capability state fields**

In the `@code` block, add alongside the existing private fields (after `private bool _fIsActive;`):

```csharp
    private List<Capability> _allCapabilities = new();
    private HashSet<Guid> _fEnabledCapIds = new();
```

- [ ] **Step 3: Load all capabilities in `Reload()`**

Find:

```csharp
    private async Task Reload()
    {
        using var db = DbFactory.CreateDbContext();
        _events = await db.Events.Include(e => e.EventType).OrderByDescending(e => e.EffDate).ToListAsync();
        _eventTypes = await db.EventTypes.ToListAsync();
        _themes = await db.Themes.ToListAsync();
        if (_fEventTypeId == Guid.Empty && _eventTypes.Any())
            _fEventTypeId = _eventTypes.First().EventTypeId;
    }
```

Change to:

```csharp
    private async Task Reload()
    {
        using var db = DbFactory.CreateDbContext();
        _events = await db.Events.Include(e => e.EventType).OrderByDescending(e => e.EffDate).ToListAsync();
        _eventTypes = await db.EventTypes.ToListAsync();
        _themes = await db.Themes.ToListAsync();
        _allCapabilities = await db.Capabilities.OrderBy(c => c.Name).ToListAsync();
        if (_fEventTypeId == Guid.Empty && _eventTypes.Any())
            _fEventTypeId = _eventTypes.First().EventTypeId;
    }
```

- [ ] **Step 4: Populate enabled capabilities in `StartEdit`**

Find:

```csharp
    private void StartEdit(Event ev)
    {
        _editingId = ev.EventId;
        _fName = ev.Name;
        _fStartDate = ev.EffDate;
        _fEndDate = ev.ExpDate;
        _fEventTypeId = ev.EventTypeId;
        _fIsActive = ev.IsActive;
        _saveError = "";
    }
```

`StartEdit` is currently synchronous, but loading enabled capabilities needs an `await`. Change to:

```csharp
    private async Task StartEdit(Event ev)
    {
        _editingId = ev.EventId;
        _fName = ev.Name;
        _fStartDate = ev.EffDate;
        _fEndDate = ev.ExpDate;
        _fEventTypeId = ev.EventTypeId;
        _fIsActive = ev.IsActive;
        _saveError = "";
        var enabled = await CapSvc.GetEnabledCapabilitiesAsync(ev.EventId);
        _fEnabledCapIds = enabled.Select(c => c.CapabilityId).ToHashSet();
    }
```

Search the rest of `Events.razor` for every call site of `StartEdit(` (there are two — one in the mobile card list, one in the desktop table row, both currently `@onclick="() => StartEdit(ev)"` or similar). Update each to `@onclick="() => StartEdit(ev)"` — no change needed to the call site syntax itself, since `@onclick` natively supports an async `Task`-returning lambda target; just confirm neither call site does something like `StartEdit(ev); _mobileFormOpen = true;` as two statements in a lambda that would now need reordering — if you find that exact pattern (`() => { StartEdit(ev); _mobileFormOpen = true; }`), change it to `async () => { await StartEdit(ev); _mobileFormOpen = true; }`.

- [ ] **Step 5: Default capabilities in `ResetForm`**

Find:

```csharp
    private void ResetForm()
    {
        _editingId = Guid.Empty;
        _fName = "";
        _fStartDate = null;
        _fEndDate = null;
        _fIsActive = false;
        _saveError = "";
        if (_eventTypes.Any()) _fEventTypeId = _eventTypes.First().EventTypeId;
    }
```

Change to (new events default to all capabilities enabled, matching how CCN 2026 was seeded — Vicki can uncheck what she doesn't want rather than starting from nothing):

```csharp
    private void ResetForm()
    {
        _editingId = Guid.Empty;
        _fName = "";
        _fStartDate = null;
        _fEndDate = null;
        _fIsActive = false;
        _saveError = "";
        _fEnabledCapIds = _allCapabilities.Select(c => c.CapabilityId).ToHashSet();
        if (_eventTypes.Any()) _fEventTypeId = _eventTypes.First().EventTypeId;
    }
```

- [ ] **Step 6: Add a toggle handler**

Add a new method near `AddEventTypeAsync`:

```csharp
    private void ToggleCapability(Guid capabilityId)
    {
        if (!_fEnabledCapIds.Add(capabilityId))
            _fEnabledCapIds.Remove(capabilityId);
    }
```

This only toggles the in-memory `_fEnabledCapIds` set (used by the checkbox UI) — the actual DB write happens in `SaveAsync` (Step 8), consistent with every other form field on this page (nothing else saves until the Save button is clicked either).

- [ ] **Step 7: Add the checkbox UI**

Find the existing `<MudSwitch T="bool" @bind-Value="_fIsActive" ...>` block (in the form panel, before the save-error/button row). Add a new section immediately after it:

```razor
            <div style="margin-bottom:16px">
                <div style="font-size:10px;font-weight:900;letter-spacing:1.5px;text-transform:uppercase;color:var(--text-light);margin-bottom:8px">Capabilities</div>
                @foreach (var cap in _allCapabilities)
                {
                    <div style="display:flex;align-items:center;gap:8px;margin-bottom:4px">
                        <input type="checkbox" checked="@_fEnabledCapIds.Contains(cap.CapabilityId)" @onchange="() => ToggleCapability(cap.CapabilityId)" />
                        <span style="font-size:13px;font-family:'Nunito',sans-serif;font-weight:600">@cap.Name</span>
                    </div>
                }
            </div>
```

- [ ] **Step 8: Sync capabilities in `SaveAsync`**

Find the end of `SaveAsync`, specifically the section right before `await db.SaveChangesAsync();`:

```csharp
        if (_editingId == Guid.Empty)
        {
            var themeId = _themes.Any() ? _themes.First().ThemeId : Guid.Empty;
            db.Events.Add(new Event
            {
                EventId = Guid.NewGuid(),
                Name = _fName.Trim(),
                EffDate = _fStartDate.Value,
                ExpDate = _fEndDate.Value,
                EventTypeId = _fEventTypeId,
                ThemeId = themeId,
                IsActive = _fIsActive
            });
        }
        else
        {
            var existing = await db.Events.FindAsync(_editingId);
            if (existing is not null)
            {
                existing.Name = _fName.Trim();
                existing.EffDate = _fStartDate.Value;
                existing.ExpDate = _fEndDate.Value;
                existing.EventTypeId = _fEventTypeId;
                existing.IsActive = _fIsActive;
            }
        }

        await db.SaveChangesAsync();
        ActiveEventSvc.InvalidateCache();
        ResetForm();
        _mobileFormOpen = false;
        await Reload();
    }
```

Change to (capturing the target event's ID in both branches, then syncing `EventCapability` rows after the main save):

```csharp
        Guid targetEventId;
        if (_editingId == Guid.Empty)
        {
            targetEventId = Guid.NewGuid();
            var themeId = _themes.Any() ? _themes.First().ThemeId : Guid.Empty;
            db.Events.Add(new Event
            {
                EventId = targetEventId,
                Name = _fName.Trim(),
                EffDate = _fStartDate.Value,
                ExpDate = _fEndDate.Value,
                EventTypeId = _fEventTypeId,
                ThemeId = themeId,
                IsActive = _fIsActive
            });
        }
        else
        {
            targetEventId = _editingId;
            var existing = await db.Events.FindAsync(_editingId);
            if (existing is not null)
            {
                existing.Name = _fName.Trim();
                existing.EffDate = _fStartDate.Value;
                existing.ExpDate = _fEndDate.Value;
                existing.EventTypeId = _fEventTypeId;
                existing.IsActive = _fIsActive;
            }
        }

        await db.SaveChangesAsync();

        foreach (var cap in _allCapabilities)
            await CapSvc.SetEventCapabilityAsync(targetEventId, cap.CapabilityId, _fEnabledCapIds.Contains(cap.CapabilityId));

        ActiveEventSvc.InvalidateCache();
        ResetForm();
        _mobileFormOpen = false;
        await Reload();
    }
```

Note: `CapSvc.SetEventCapabilityAsync` opens its own `DbContext` per call (matching the service's design in Task 2) rather than reusing the `db` variable from `SaveAsync` — this is intentional and consistent with how `ActiveEventSvc.InvalidateCache()` is already called as a separate step after `db.SaveChangesAsync()` on this same page. `CapSvc.SetEventCapabilityAsync` already calls its own `cache.Remove(EventKey(eventId))` internally, so no separate `CapSvc.InvalidateCache()` call is needed here.

- [ ] **Step 9: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 10: Manual verification**

Visit `/admin/events`, edit CCN 2026 — confirm all 7 capability checkboxes show checked. Uncheck one, save, re-open the edit form — confirm it stays unchecked. Re-check it before finishing (don't leave CCN 2026 with a capability disabled).

- [ ] **Step 11: Commit**

```bash
git add "CampClotNot/Pages/Admin/Events.razor"
git commit -m "feat: add capability checkboxes to Admin Events page"
```

---

### Task 4: Gate nav items in `AppNav.razor`

**Files:**
- Modify: `CampClotNot/Shared/AppNav.razor`

**Interfaces:**
- Consumes: `CapabilityService.IsEnabledAsync` (Task 2), `ActiveEventService.GetActiveEventIdAsync` (sub-project 1, already merged).

- [ ] **Step 1: Inject services and add lifecycle loading**

`AppNav.razor` currently has no `OnInitializedAsync` and no data-loading `@code` — it's purely presentational, driven by `AuthorizeView` role checks. Add the two injections to the top of the file (alongside `@inject NavigationManager Nav` and `@inject ThemeService ThemeSvc`):

```razor
@inject ActiveEventService ActiveEventSvc
@inject CapabilityService CapSvc
```

- [ ] **Step 2: Add capability state and loading**

In the `@code` block, add near the other private fields:

```csharp
    private bool _boardGameEnabled = true;
    private bool _miniGameEnabled = true;
    private bool _bowserEnabled = true;
    private bool _coinShopEnabled = true;
    private bool _awardsEnabled = true;

    protected override async Task OnInitializedAsync()
    {
        var eventId = await ActiveEventSvc.GetActiveEventIdAsync();
        _boardGameEnabled = await CapSvc.IsEnabledAsync(eventId, Feature.BoardGame);
        _miniGameEnabled  = await CapSvc.IsEnabledAsync(eventId, Feature.MiniGameSpinner);
        _bowserEnabled    = await CapSvc.IsEnabledAsync(eventId, Feature.BowserEvent);
        _coinShopEnabled  = await CapSvc.IsEnabledAsync(eventId, Feature.CoinShop);
        _awardsEnabled    = await CapSvc.IsEnabledAsync(eventId, Feature.Awards);
    }
```

The fields default to `true` so that if this component somehow renders before `OnInitializedAsync` completes (Blazor Server prerendering), nav items don't flash hidden-then-shown — they start visible and only hide once the real capability state loads, matching the "fail open" posture of showing rather than hiding on an unresolved state during the brief prerender window.

- [ ] **Step 3: Gate the desktop Game dropdown**

Find (in the desktop `AuthorizeView Roles="Admin,Staff"`/`Roles="Admin"` blocks inside the Game dropdown):

```razor
                <AuthorizeView Roles="Admin,Staff">
                    <a href="/board" ...>Board Game</a>
                </AuthorizeView>
                <AuthorizeView Roles="Admin">
                    <a href="/minigames" ...>Mini-Game</a>
                    <a href="/bowser" ...>Bowser Event</a>
                    <a href="/leaderboard/reveal" target="_blank" ...>Awards Ceremony</a>
                    <button @onclick="...">Projector</button>
                </AuthorizeView>
```

(The exact attribute/styling content between the tags is unchanged — only wrap each link in an additional capability check. Read the actual current file for the precise markup before editing; the shape above is the structural skeleton confirmed by investigation, not the literal current styling.)

Wrap each capability-gated link with an `@if`, keeping the existing `AuthorizeView` role wrapping outside it:

```razor
                <AuthorizeView Roles="Admin,Staff">
                    @if (_boardGameEnabled)
                    {
                        <a href="/board" ...>Board Game</a>
                    }
                </AuthorizeView>
                <AuthorizeView Roles="Admin">
                    @if (_miniGameEnabled)
                    {
                        <a href="/minigames" ...>Mini-Game</a>
                    }
                    @if (_bowserEnabled)
                    {
                        <a href="/bowser" ...>Bowser Event</a>
                    }
                    @if (_awardsEnabled)
                    {
                        <a href="/leaderboard/reveal" target="_blank" ...>Awards Ceremony</a>
                    }
                    <button @onclick="...">Projector</button>
                </AuthorizeView>
```

Leave the "Projector" button and the `/leaderboard` (plain leaderboard, not reveal) link unwrapped — the plain leaderboard/standings view isn't one of the 5 gated capabilities.

- [ ] **Step 4: Gate the Admin dropdown's Transactions link**

Find the `<a href="/transactions">Transactions</a>` line in the Admin dropdown (both desktop version and the mobile Admin sheet version — two separate occurrences). Wrap each in:

```razor
@if (_coinShopEnabled)
{
    <a href="/transactions">Transactions</a>
}
```

(Preserve the exact existing attributes/styling on the `<a>` tag — only add the wrapping `@if`.)

- [ ] **Step 5: Gate the mobile Game bottom sheet**

Repeat the same pattern as Step 3 for the mobile Game sheet section (Board Game, Mini-Game, Bowser Event, Awards Ceremony links) — same `_boardGameEnabled`/`_miniGameEnabled`/`_bowserEnabled`/`_awardsEnabled` flags, same wrapping pattern, applied to the mobile markup's copies of these links.

- [ ] **Step 6: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 7: Manual verification**

With CCN 2026 having all capabilities enabled (default state), confirm the nav looks unchanged — all 5 gated links still appear in both desktop and mobile. (Full "does hiding actually work" verification happens in Task 11 once pages are also gated.)

- [ ] **Step 8: Commit**

```bash
git add "CampClotNot/Shared/AppNav.razor"
git commit -m "feat: gate nav items by per-event capability"
```

---

### Task 5: Gate Board Game pages

**Files:**
- Modify: `CampClotNot/Pages/Board.razor`
- Modify: `CampClotNot/Pages/BoardDisplay.razor`

**Interfaces:**
- Consumes: `CapabilityService.IsEnabledAsync` (Task 2). Both files already inject `ActiveEventService` (sub-project 1) and have an `EventId` field populated in `OnInitializedAsync`.

- [ ] **Step 1: `Board.razor`**

Add injection: `@inject CapabilityService CapSvc`

In `OnInitializedAsync`, immediately after the existing `EventId = await ActiveEventSvc.GetActiveEventIdAsync();` line, add:

```csharp
        _capabilityEnabled = await CapSvc.IsEnabledAsync(EventId, Feature.BoardGame);
        if (!_capabilityEnabled) { _loading = false; return; }
```

Add the backing field near the other private fields: `private bool _capabilityEnabled = true;`

Read the file's top-level render markup (the content after `@page`/`@inject` directives, typically starting with the first real content `<div>`). Find where the existing `@if (_loading) { ... } else { ... (the real board UI) ... }` structure is (Board.razor already has a `_loading` flag per the plan's earlier investigation). Add a new branch: `else if (!_capabilityEnabled) { <the standard disabled-guard block from Global Constraints> } else { ... existing content ... }`. If the file's structure isn't a simple `_loading`/else split (verify by reading the actual file), use the same principle: guard the entire real-content render behind `_capabilityEnabled`, checked after the loading check.

- [ ] **Step 2: `BoardDisplay.razor`**

Same pattern: inject `CapabilityService CapSvc`, add `_capabilityEnabled` field, check it right after `EventId = await ActiveEventSvc.GetActiveEventIdAsync();` in `OnInitializedAsync`, short-circuit remaining initialization if disabled, and gate the render markup the same way as Step 1.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Manual verification**

Deferred to Task 11 (full end-to-end pass, once a real test event with the capability off exists).

- [ ] **Step 5: Commit**

```bash
git add CampClotNot/Pages/Board.razor CampClotNot/Pages/BoardDisplay.razor
git commit -m "feat: gate Board Game pages behind BoardGame capability"
```

---

### Task 6: Gate Mini-Game Spinner pages

**Files:**
- Modify: `CampClotNot/Pages/MiniGames.razor`
- Modify: `CampClotNot/Pages/MiniGamesDisplay.razor`

**Interfaces:**
- Consumes: `CapabilityService.IsEnabledAsync` (Task 2). Both files already inject `ActiveEventService` and have an `EventId` field.

- [ ] **Step 1: `MiniGames.razor`**

Same pattern as Task 5 Step 1, using `Feature.MiniGameSpinner`. `MiniGames.razor`'s `OnInitializedAsync` currently reads:

```csharp
    protected override async Task OnInitializedAsync()
    {
        EventId = await ActiveEventSvc.GetActiveEventIdAsync();
        await LoadAsync();
        _loading = false;
    }
```

Change to:

```csharp
    protected override async Task OnInitializedAsync()
    {
        EventId = await ActiveEventSvc.GetActiveEventIdAsync();
        _capabilityEnabled = await CapSvc.IsEnabledAsync(EventId, Feature.MiniGameSpinner);
        if (!_capabilityEnabled) { _loading = false; return; }
        await LoadAsync();
        _loading = false;
    }
```

Inject `CapabilityService CapSvc`, add `private bool _capabilityEnabled = true;`, and gate the render markup the same way as Task 5.

- [ ] **Step 2: `MiniGamesDisplay.razor`**

Same pattern, using `Feature.MiniGameSpinner`.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CampClotNot/Pages/MiniGames.razor CampClotNot/Pages/MiniGamesDisplay.razor
git commit -m "feat: gate Mini-Game Spinner pages behind MiniGameSpinner capability"
```

---

### Task 7: Gate Bowser Event pages

**Files:**
- Modify: `CampClotNot/Pages/BowserEvent.razor`
- Modify: `CampClotNot/Pages/BowserDisplay.razor`

**Interfaces:**
- Consumes: `CapabilityService.IsEnabledAsync` (Task 2). `BowserEvent.razor` already injects `ActiveEventService` and has an `_eventId` field (sub-project 1, Task 4). `BowserDisplay.razor` has not yet been touched by any prior sub-project-1 task — check whether it has its own hardcoded event reference or active-event resolution; if it doesn't resolve an event at all today, add `ActiveEventService` injection and resolution here as part of this task (it wasn't in sub-project 1's file list, so this may be new wiring).

- [ ] **Step 1: `BowserEvent.razor`**

Inject `CapabilityService CapSvc`. In `OnInitializedAsync`, immediately after `_eventId = await ActiveEventSvc.GetActiveEventIdAsync();`, add:

```csharp
        _capabilityEnabled = await CapSvc.IsEnabledAsync(_eventId, Feature.BowserEvent);
        if (!_capabilityEnabled) { _loading = false; return; }
```

Add `private bool _capabilityEnabled = true;`, gate the render markup per the Task 5 pattern.

- [ ] **Step 2: `BowserDisplay.razor`**

Read the file first to determine its current event-resolution state (per the note in Interfaces above). If it already resolves an event ID, follow the same gating pattern as Step 1. If it doesn't yet resolve an event at all, inject both `ActiveEventService` and `CapabilityService`, resolve the event ID in `OnInitializedAsync`, then gate on `Feature.BowserEvent` the same way.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CampClotNot/Pages/BowserEvent.razor CampClotNot/Pages/BowserDisplay.razor
git commit -m "feat: gate Bowser Event pages behind BowserEvent capability"
```

---

### Task 8: Gate the Coin Shop (Transactions) page

**Files:**
- Modify: `CampClotNot/Pages/Transactions.razor`

**Interfaces:**
- Consumes: `CapabilityService.IsEnabledAsync` (Task 2). This file has not been touched by sub-project 1 — check whether it currently resolves an active event at all (it may not need one if it queries all groups' transactions without an event filter; read the file to confirm). If it doesn't currently reference an event, you'll need `ActiveEventService` too, purely to get the ID to pass to `IsEnabledAsync`.

- [ ] **Step 1: Inject services**

Add `@inject ActiveEventService ActiveEventSvc` (if not already effectively available) and `@inject CapabilityService CapSvc` to the top of `Transactions.razor`, alongside its existing `@inject TransactionService TxSvc` and `@inject GroupService GroupSvc`.

- [ ] **Step 2: Add the gate check**

Read the file's `OnInitializedAsync` (or equivalent lifecycle method) first. Add, as the first lines of that method:

```csharp
        var eventId = await ActiveEventSvc.GetActiveEventIdAsync();
        _capabilityEnabled = await CapSvc.IsEnabledAsync(eventId, Feature.CoinShop);
        if (!_capabilityEnabled) { _loading = false; return; }
```

(Adjust `_loading = false;` to match whatever loading-flag field this file actually uses — read the file to confirm the exact field name.) Add `private bool _capabilityEnabled = true;` near the other private fields.

- [ ] **Step 3: Gate the render markup**

Same pattern as prior tasks — wrap the page's real content behind `_capabilityEnabled`, showing the standard disabled-guard block otherwise.

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add CampClotNot/Pages/Transactions.razor
git commit -m "feat: gate Transactions page behind CoinShop capability"
```

---

### Task 9: Gate the Awards Ceremony pages

**Files:**
- Modify: `CampClotNot/Pages/LeaderboardReveal.razor`
- Modify: `CampClotNot/Pages/LeaderboardRevealDisplay.razor`

**Interfaces:**
- Consumes: `CapabilityService.IsEnabledAsync` (Task 2). Neither file currently resolves an active event — both need `ActiveEventService` injected purely to get the ID for the capability check.

- [ ] **Step 1: `LeaderboardReveal.razor`**

Add `@inject ActiveEventService ActiveEventSvc` and `@inject CapabilityService CapSvc` (alongside existing `@inject GroupService GroupSvc` and `@inject IHubContext<LiveHub> Hub`). Read the file's `OnInitializedAsync` (or add one if it doesn't have one) and add the gate check as the first step, same pattern as Task 8 Step 2, using `Feature.Awards`. Gate the render markup the same way.

- [ ] **Step 2: `LeaderboardRevealDisplay.razor`**

Same pattern as Step 1.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add CampClotNot/Pages/LeaderboardReveal.razor CampClotNot/Pages/LeaderboardRevealDisplay.razor
git commit -m "feat: gate Awards Ceremony pages behind Awards capability"
```

---

### Task 10: Gate Admin/Games.razor tabs

**Files:**
- Modify: `CampClotNot/Pages/Admin/Games.razor`

**Interfaces:**
- Consumes: `CapabilityService.IsEnabledAsync` (Task 2). This file already injects `ActiveEventService` and has an `EventId` field (sub-project 1, Task 3).

- [ ] **Step 1: Inject the service and load capability flags**

Add `@inject CapabilityService CapSvc`. In `OnInitializedAsync`, immediately after `EventId = await ActiveEventSvc.GetActiveEventIdAsync();` (before the `Task.WhenAll(...)` call), add:

```csharp
        _boardGameEnabled = await CapSvc.IsEnabledAsync(EventId, Feature.BoardGame);
        _miniGameEnabled  = await CapSvc.IsEnabledAsync(EventId, Feature.MiniGameSpinner);
        _bowserEnabled    = await CapSvc.IsEnabledAsync(EventId, Feature.BowserEvent);
```

Add the three backing fields near the other private fields: `private bool _boardGameEnabled = true; private bool _miniGameEnabled = true; private bool _bowserEnabled = true;`

- [ ] **Step 2: Conditionally render tabs**

The page has 5 `MudTabPanel` entries: "Board Spaces" (line ~30), "Block Hit Scripts" (line ~92), "Mini-Game Scripts" (line ~159), "Bowser Scripts" (line ~279), "Reset" (line ~358). Wrap each capability-specific tab in an `@if`:

```razor
        @if (_boardGameEnabled)
        {
            <MudTabPanel Text="Board Spaces">
                ... existing content unchanged ...
            </MudTabPanel>
            <MudTabPanel Text="Block Hit Scripts">
                ... existing content unchanged ...
            </MudTabPanel>
        }
        @if (_miniGameEnabled)
        {
            <MudTabPanel Text="Mini-Game Scripts">
                ... existing content unchanged ...
            </MudTabPanel>
        }
        @if (_bowserEnabled)
        {
            <MudTabPanel Text="Bowser Scripts">
                ... existing content unchanged ...
            </MudTabPanel>
        }
        <MudTabPanel Text="Reset">
            ... existing content unchanged ...
        </MudTabPanel>
```

Leave "Reset" unwrapped for now — read its content first; if it has per-feature reset buttons (board reset, mini-game reset, bowser reset), those individual buttons/sections within the Reset tab should each be wrapped in their matching `@if` instead of hiding the whole tab, so an admin can still reset whichever features remain enabled.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add "CampClotNot/Pages/Admin/Games.razor"
git commit -m "feat: gate Admin Games tabs by capability"
```

---

### Task 11: End-to-end verification

**Files:** none (verification only)

- [ ] **Step 1: Confirm build is clean**

Run: `dotnet build`
Expected: `Build succeeded.`, 0 errors, no new warnings beyond the 3 pre-existing ones.

- [ ] **Step 2: Regression pass with CCN 2026 (all capabilities on)**

Run the app, log in, click through every page touched by Tasks 4-10 (`/dashboard` for nav, `/board`, `/board/display`, `/minigames`, `/minigames/display`, `/bowser`, `/bowser/display`, `/transactions`, `/leaderboard/reveal`, `/leaderboard/reveal/display`, `/admin/games`, `/admin/events`). Everything must look and behave exactly as before this sub-project — CCN 2026 has all 7 capabilities enabled by default, so nothing should be hidden or blocked.

- [ ] **Step 3: Prove gating actually works**

On `/admin/events`, edit CCN 2026 (or create a throwaway test event, mark it active) and uncheck "Board Game" and "Bowser Event". Save. Confirm:
- The desktop and mobile nav no longer show "Board Game" or "Bowser Event" links
- Directly navigating to `/board` or `/bowser` shows the "Not available for this event" guard instead of the real page
- `/admin/games` no longer shows the "Board Spaces"/"Block Hit Scripts"/"Bowser Scripts" tabs
- Mini-Game Spinner, Coin Shop (Transactions), and Awards Ceremony are unaffected (still visible/working) — confirming the gate is per-capability, not all-or-nothing

- [ ] **Step 4: Restore CCN 2026's capabilities**

Re-check "Board Game" and "Bowser Event" on CCN 2026 (or whichever event was used for testing), save, and delete any throwaway test event created for this check.

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "chore: v1.1.0 sub-project 2 complete — capability gating verified end-to-end" --allow-empty
```
