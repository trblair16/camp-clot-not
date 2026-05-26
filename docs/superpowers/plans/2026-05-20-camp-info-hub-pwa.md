# Camp Info Hub + PWA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Camp Info Hub (Schedule, Announcements, Staff Directory, Info Pages) and PWA installability for v0.5.0 of the Camp Clot Not Blazor Server app.

**Architecture:** Page-per-route Hub under `/hub/*` with a shared `HubSubNav` component; four concrete service classes following the `MiniGameService` pattern (`IDbContextFactory<AppDbContext>` injected, each method creates and disposes its own context); PWA via static manifest + service worker added to `_Layout.cshtml`.

**Tech Stack:** Blazor Server .NET 8, EF Core + PostgreSQL (Npgsql), MudBlazor, Markdig (new), vanilla JS interop for PWA/connection indicator.

---

## File Map

**Create:**
- `CampClotNot/Data/Entities/InfoPage.cs`
- `CampClotNot/Data/Entities/StaffMember.cs`
- `CampClotNot/Data/Entities/Announcement.cs`
- `CampClotNot/Data/Entities/ScheduleEvent.cs`
- `CampClotNot/Data/Entities/ScheduleEventGroup.cs`
- `CampClotNot/Services/InfoPageService.cs`
- `CampClotNot/Services/StaffDirectoryService.cs`
- `CampClotNot/Services/AnnouncementService.cs`
- `CampClotNot/Services/ScheduleService.cs`
- `CampClotNot/Pages/Hub/Hub.razor`
- `CampClotNot/Pages/Hub/HubSubNav.razor`
- `CampClotNot/Pages/Hub/Schedule.razor`
- `CampClotNot/Pages/Hub/Announcements.razor`
- `CampClotNot/Pages/Hub/Staff.razor`
- `CampClotNot/Pages/Hub/Info.razor`
- `CampClotNot/Shared/ConnectionIndicator.razor`
- `CampClotNot/Shared/InstallPromptBanner.razor`
- `CampClotNot/wwwroot/js/connection-indicator.js`
- `CampClotNot/wwwroot/js/install-prompt.js`
- `CampClotNot/wwwroot/manifest.json`
- `CampClotNot/wwwroot/service-worker.js`
- `CampClotNot/wwwroot/offline.html`
- `CampClotNot/wwwroot/icons/icon-192.png` (placeholder)
- `CampClotNot/wwwroot/icons/icon-512.png` (placeholder)
- `CampClotNot/wwwroot/icons/icon-512-maskable.png` (placeholder)

**Modify:**
- `CampClotNot/Data/AppDbContext.cs`
- `CampClotNot/Services/SeedService.cs`
- `CampClotNot/Program.cs`
- `CampClotNot/Pages/_Layout.cshtml`
- `CampClotNot/Shared/AppNav.razor`
- `.gitignore` (already modified, just needs committing)

---

## Task 1: Branch Setup + SQL Cleanup

**Files:** `.gitignore`, `docs/superpowers/specs/2026-05-19-camp-info-hub-pwa-design.md`

- [ ] **Verify you are on the correct branch**

```powershell
git branch --show-current   # must be feature/N-camp-info-hub
git log --oneline -3        # confirm it branched from dev, not feature/96
```

- [ ] **Commit the pending files that are already in the working directory**

```powershell
git add .gitignore `
  docs/superpowers/specs/2026-05-19-camp-info-hub-pwa-design.md `
  docs/superpowers/handoffs/2026-05-20-v050-implementation-handoff.md `
  docs/superpowers/plans/2026-05-20-camp-info-hub-pwa.md
git commit -m "docs: v0.5.0 design spec, handoff, and implementation plan"
```

- [ ] **Run the DB cleanup SQL against hbda_dev**

Connect to PostgreSQL (`psql hbda_dev` or pgAdmin) and run:

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

Expected: `DELETE 0` or `DELETE 1` for each — no errors.

---

## Task 2: Group Seed Cleanup

**Files:** `CampClotNot/Services/SeedService.cs`

The previous agent added Group5/Group6 IDs and a stale cleanup loop that runs on every startup. Remove both.

- [ ] **Remove the dead Group5/Group6 constants from `SeedService.Id`**

In the `public static class Id` block, delete these two lines:
```csharp
public static readonly Guid Group5 = new("0000000a-000a-000a-000a-000000000005");
public static readonly Guid Group6 = new("0000000a-000a-000a-000a-000000000006");
```

- [ ] **Remove the stale cleanup loop from `SeedGroupsAsync`**

Find and delete the block that looks like this (it removes stale groups 5 and 6):

```csharp
// Remove placeholder groups 5 & 6 and their dependent rows if they exist
foreach (var staleId in new[] { Id.Group5, Id.Group6 })
{
    var pos = await db.GroupBoardPositions.FindAsync(staleId);
    if (pos is not null) db.GroupBoardPositions.Remove(pos);
    // ...
    var stale = await db.Groups.FindAsync(staleId);
    if (stale is not null) db.Groups.Remove(stale);
}
```

- [ ] **Build to verify**

```powershell
cd CampClotNot; dotnet build
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Commit**

```powershell
git add CampClotNot/Services/SeedService.cs
git commit -m "chore: remove dead Group5/Group6 seed constants and stale cleanup loop"
```

---

## Task 3: Entity Classes

**Files:** 5 new files in `CampClotNot/Data/Entities/`

> **Naming note:** `ScheduleEvent`'s PK is `ScheduleEventId` (not `EventId`) to avoid confusion with the `Event` entity's FK pattern used throughout the codebase. `ScheduleEvent` also has a `CampEventId` FK to `Event` to scope events per camp, consistent with how `Group`, `ScriptedMiniGame`, etc. are all scoped to an Event.

- [ ] **Create `CampClotNot/Data/Entities/InfoPage.cs`**

```csharp
namespace CampClotNot.Data.Entities;

public class InfoPage
{
    public Guid PageId { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string IconEmoji { get; set; } = "📄";
    public int SortOrder { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedBy { get; set; } = null!;
}
```

- [ ] **Create `CampClotNot/Data/Entities/StaffMember.cs`**

```csharp
namespace CampClotNot.Data.Entities;

public class StaffMember
{
    public Guid StaffMemberId { get; set; }
    public string DisplayName { get; set; } = "";
    public string RoleTitle { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string AvatarEmoji { get; set; } = "👤";
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid? LinkedUserId { get; set; }
    public User? LinkedUser { get; set; }
}
```

- [ ] **Create `CampClotNot/Data/Entities/Announcement.cs`**

```csharp
namespace CampClotNot.Data.Entities;

public enum AnnouncementPriority { Normal, Urgent }

public class Announcement
{
    public Guid AnnouncementId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;
    public bool IsPinned { get; set; }
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsArchived { get; set; }
}
```

- [ ] **Create `CampClotNot/Data/Entities/ScheduleEvent.cs`**

```csharp
namespace CampClotNot.Data.Entities;

public enum ScheduleEventType { Activity, Meal, Travel, Free, Mandatory }

public class ScheduleEvent
{
    public Guid ScheduleEventId { get; set; }
    public Guid CampEventId { get; set; }       // FK to Event — scopes to CCN 2026
    public Event CampEvent { get; set; } = null!;
    public DateOnly CampDay { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? LocationDisplayName { get; set; }
    public ScheduleEventType EventType { get; set; }
    public bool AppliesToAllGroups { get; set; } = true;
    public int? MaxCapacity { get; set; }       // stored silently; future registration feature
    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
    public List<ScheduleEventGroup> EventGroups { get; set; } = [];
}
```

- [ ] **Create `CampClotNot/Data/Entities/ScheduleEventGroup.cs`**

```csharp
namespace CampClotNot.Data.Entities;

public class ScheduleEventGroup
{
    public Guid ScheduleEventId { get; set; }
    public ScheduleEvent ScheduleEvent { get; set; } = null!;
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
}
```

- [ ] **Build to verify**

```powershell
cd CampClotNot; dotnet build
```

- [ ] **Commit**

```powershell
git add CampClotNot/Data/Entities/
git commit -m "feat: add Hub entity classes (InfoPage, StaffMember, Announcement, ScheduleEvent)"
```

---

## Task 4: AppDbContext Updates

**Files:** `CampClotNot/Data/AppDbContext.cs`

- [ ] **Add the 5 new DbSets** in a new section comment:

```csharp
// Hub (Camp Info)
public DbSet<InfoPage> InfoPages => Set<InfoPage>();
public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
public DbSet<Announcement> Announcements => Set<Announcement>();
public DbSet<ScheduleEvent> ScheduleEvents => Set<ScheduleEvent>();
public DbSet<ScheduleEventGroup> ScheduleEventGroups => Set<ScheduleEventGroup>();
```

- [ ] **Add to `OnModelCreating`** after the existing composite key configs:

```csharp
// ScheduleEvent: non-conventional PK
modelBuilder.Entity<ScheduleEvent>().HasKey(e => e.ScheduleEventId);

// ScheduleEventGroup: composite PK
modelBuilder.Entity<ScheduleEventGroup>()
    .HasKey(eg => new { eg.ScheduleEventId, eg.GroupId });

// InfoPage: unique index on Slug
modelBuilder.Entity<InfoPage>()
    .HasIndex(p => p.Slug)
    .IsUnique();
```

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/Data/AppDbContext.cs
git commit -m "feat: add Hub DbSets and model config to AppDbContext"
```

---

## Task 5: EF Core Migrations

**Files:** `CampClotNot/Migrations/` (4 new migration files)

Run all commands from the `CampClotNot/` directory.

- [ ] **Migration 1 — InfoPage**

```powershell
cd CampClotNot; dotnet ef migrations add AddInfoPage
```

- [ ] **Migration 2 — StaffMember**

```powershell
dotnet ef migrations add AddStaffMember
```

- [ ] **Migration 3 — Announcement**

```powershell
dotnet ef migrations add AddAnnouncement
```

- [ ] **Migration 4 — ScheduleEvent** (covers both ScheduleEvents and ScheduleEventGroups tables)

```powershell
dotnet ef migrations add AddScheduleEvent
```

- [ ] **Apply all migrations**

```powershell
dotnet ef database update
```
Expected output ends with: `Done.`

- [ ] **Verify tables exist in hbda_dev**

```sql
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public'
AND table_name IN ('info_pages','staff_members','announcements','schedule_events','schedule_event_groups');
```
All 5 rows should be returned.

- [ ] **Commit**

```powershell
git add CampClotNot/Migrations/
git commit -m "feat: EF Core migrations for InfoPage, StaffMember, Announcement, ScheduleEvent"
```

---

## Task 6: SeedService — InfoPage Seeds

**Files:** `CampClotNot/Services/SeedService.cs`

- [ ] **Add InfoPage stable IDs to `SeedService.Id`** (after the BoardSpaces block):

```csharp
// InfoPages — CCN 2026 handbook pages (slug set fixed in v0.5.0)
public static readonly Guid InfoPageRules            = new("0000000e-000e-000e-000e-000000000001");
public static readonly Guid InfoPageFaq              = new("0000000e-000e-000e-000e-000000000002");
public static readonly Guid InfoPageMedical          = new("0000000e-000e-000e-000e-000000000003");
public static readonly Guid InfoPageScheduleOverview = new("0000000e-000e-000e-000e-000000000004");
public static readonly Guid InfoPagePacking          = new("0000000e-000e-000e-000e-000000000005");
```

- [ ] **Add `SeedInfoPagesAsync` method** at the bottom of `SeedService`:

```csharp
private async Task SeedInfoPagesAsync(AppDbContext db)
{
    // Find the seeded admin user ID to use as UpdatedByUserId
    var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == _config["Seed:AdminEmail"]);
    var adminId = adminUser?.UserId ?? Guid.Empty;

    var pages = new[]
    {
        new { Id = Id.InfoPageRules,            Slug = "rules",             Title = "Camp Rules",        Icon = "📋", Order = 1 },
        new { Id = Id.InfoPageFaq,              Slug = "faq",               Title = "FAQ",               Icon = "❓", Order = 2 },
        new { Id = Id.InfoPageMedical,          Slug = "medical",           Title = "Medical Info",      Icon = "🏥", Order = 3 },
        new { Id = Id.InfoPageScheduleOverview, Slug = "schedule-overview", Title = "Schedule Overview", Icon = "📅", Order = 4 },
        new { Id = Id.InfoPagePacking,          Slug = "packing",           Title = "Packing List",      Icon = "🎒", Order = 5 },
    };

    foreach (var def in pages)
    {
        if (await db.InfoPages.FindAsync(def.Id) is null)
        {
            db.InfoPages.Add(new InfoPage
            {
                PageId          = def.Id,
                Slug            = def.Slug,
                Title           = def.Title,
                IconEmoji       = def.Icon,
                SortOrder       = def.Order,
                Body            = $"# {def.Title}\n\nContent coming soon — an Admin will update this before camp.",
                UpdatedAt       = DateTime.UtcNow,
                UpdatedByUserId = adminId,
            });
        }
    }
    await db.SaveChangesAsync();
    logger.LogInformation("Seeded/verified CCN 2026 info pages.");
}
```

> **Note:** `_config` refers to the `IConfiguration config` already injected into `SeedService`. If the field name differs, check the existing constructor and use the correct field name.

- [ ] **Call `SeedInfoPagesAsync` from `SeedAsync`** alongside the other seed calls:

```csharp
await SeedInfoPagesAsync(db);
```

- [ ] **Run the app briefly to confirm seeding**

```powershell
cd CampClotNot; dotnet run
```
Watch the console for: `Seeded/verified CCN 2026 info pages.` Then `Ctrl+C`.

- [ ] **Commit**

```powershell
git add CampClotNot/Services/SeedService.cs
git commit -m "feat: seed InfoPage stable IDs and placeholder content"
```

---

## Task 7: Service Classes + Program.cs

**Files:** 4 new service files + `CampClotNot/Program.cs`

- [ ] **Create `CampClotNot/Services/InfoPageService.cs`**

```csharp
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class InfoPageService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<InfoPage>> GetAllAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.InfoPages.OrderBy(p => p.SortOrder).ToListAsync();
    }

    public async Task<InfoPage?> GetBySlugAsync(string slug)
    {
        using var db = factory.CreateDbContext();
        return await db.InfoPages.FirstOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task UpdateBodyAsync(Guid pageId, string body, Guid updatedByUserId)
    {
        using var db = factory.CreateDbContext();
        var page = await db.InfoPages.FindAsync(pageId);
        if (page is null) return;
        page.Body = body;
        page.UpdatedAt = DateTime.UtcNow;
        page.UpdatedByUserId = updatedByUserId;
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Create `CampClotNot/Services/StaffDirectoryService.cs`**

```csharp
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class StaffDirectoryService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<StaffMember>> GetVisibleAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.StaffMembers
            .Where(s => s.IsVisible)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task<List<StaffMember>> GetAllAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.StaffMembers.OrderBy(s => s.SortOrder).ToListAsync();
    }

    public async Task UpsertAsync(StaffMember member)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.StaffMembers.FindAsync(member.StaffMemberId);
        if (existing is null)
        {
            if (member.StaffMemberId == Guid.Empty)
                member.StaffMemberId = Guid.NewGuid();
            db.StaffMembers.Add(member);
        }
        else
        {
            existing.DisplayName  = member.DisplayName;
            existing.RoleTitle    = member.RoleTitle;
            existing.Phone        = member.Phone;
            existing.Email        = member.Email;
            existing.AvatarEmoji  = member.AvatarEmoji;
            existing.IsVisible    = member.IsVisible;
            existing.SortOrder    = member.SortOrder;
            existing.LinkedUserId = member.LinkedUserId;
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid staffMemberId)
    {
        using var db = factory.CreateDbContext();
        var member = await db.StaffMembers.FindAsync(staffMemberId);
        if (member is null) return;
        db.StaffMembers.Remove(member);
        await db.SaveChangesAsync();
    }

    public async Task ReorderAsync(List<Guid> orderedIds)
    {
        using var db = factory.CreateDbContext();
        var members = await db.StaffMembers
            .Where(s => orderedIds.Contains(s.StaffMemberId))
            .ToListAsync();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var m = members.FirstOrDefault(x => x.StaffMemberId == orderedIds[i]);
            if (m is not null) m.SortOrder = i;
        }
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Create `CampClotNot/Services/AnnouncementService.cs`**

```csharp
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class AnnouncementService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<Announcement>> GetFeedAsync()
    {
        using var db = factory.CreateDbContext();
        var now = DateTime.UtcNow;

        // Auto-archive expired announcements on read
        var expired = await db.Announcements
            .Where(a => !a.IsArchived && a.ExpiresAt != null && a.ExpiresAt <= now)
            .ToListAsync();
        foreach (var a in expired) a.IsArchived = true;
        if (expired.Count > 0) await db.SaveChangesAsync();

        return await db.Announcements
            .Where(a => !a.IsArchived)
            .Include(a => a.Author)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    // Stub for future Dashboard banner — not called from Dashboard in v0.5.0
    public async Task<Announcement?> GetLatestPinnedAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.Announcements
            .Where(a => !a.IsArchived && a.IsPinned)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Announcement> PostAsync(string title, string body, AnnouncementPriority priority, Guid authorId)
    {
        using var db = factory.CreateDbContext();
        var announcement = new Announcement
        {
            AnnouncementId = Guid.NewGuid(),
            Title          = title,
            Body           = body,
            Priority       = priority,
            IsPinned       = false,
            AuthorId       = authorId,
            CreatedAt      = DateTime.UtcNow,
            IsArchived     = false
        };
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync();
        return announcement;
    }

    public async Task PinAsync(Guid id, bool isPinned)
    {
        using var db = factory.CreateDbContext();
        var a = await db.Announcements.FindAsync(id);
        if (a is null) return;
        a.IsPinned = isPinned;
        await db.SaveChangesAsync();
    }

    public async Task ArchiveAsync(Guid id)
    {
        using var db = factory.CreateDbContext();
        var a = await db.Announcements.FindAsync(id);
        if (a is null) return;
        a.IsArchived = true;
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Create `CampClotNot/Services/ScheduleService.cs`**

```csharp
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public record ScheduleEventDto(
    Guid ScheduleEventId,
    Guid CampEventId,
    DateOnly CampDay,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    string Title,
    string? Description,
    string? LocationDisplayName,
    ScheduleEventType EventType,
    bool AppliesToAllGroups,
    int? MaxCapacity,
    List<Guid> GroupIds
);

public class ScheduleService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<ScheduleEvent>> GetByEventAsync(Guid campEventId)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleEvents
            .Where(e => e.CampEventId == campEventId)
            .Include(e => e.EventGroups).ThenInclude(eg => eg.Group)
            .OrderBy(e => e.CampDay).ThenBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<List<ScheduleEvent>> GetForDayAsync(Guid campEventId, DateOnly day)
    {
        using var db = factory.CreateDbContext();
        return await db.ScheduleEvents
            .Where(e => e.CampEventId == campEventId && e.CampDay == day)
            .Include(e => e.EventGroups).ThenInclude(eg => eg.Group)
            .OrderBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<ScheduleEvent> UpsertAsync(ScheduleEventDto dto, Guid userId)
    {
        using var db = factory.CreateDbContext();
        var existing = await db.ScheduleEvents
            .Include(e => e.EventGroups)
            .FirstOrDefaultAsync(e => e.ScheduleEventId == dto.ScheduleEventId);

        if (existing is null)
        {
            var ev = new ScheduleEvent
            {
                ScheduleEventId     = dto.ScheduleEventId == Guid.Empty ? Guid.NewGuid() : dto.ScheduleEventId,
                CampEventId         = dto.CampEventId,
                CampDay             = dto.CampDay,
                StartTime           = dto.StartTime,
                EndTime             = dto.EndTime,
                Title               = dto.Title,
                Description         = dto.Description,
                LocationDisplayName = dto.LocationDisplayName,
                EventType           = dto.EventType,
                AppliesToAllGroups  = dto.AppliesToAllGroups,
                MaxCapacity         = dto.MaxCapacity,
                CreatedBy           = userId,
                UpdatedAt           = DateTime.UtcNow
            };
            if (!dto.AppliesToAllGroups)
                ev.EventGroups = dto.GroupIds
                    .Select(gid => new ScheduleEventGroup { ScheduleEventId = ev.ScheduleEventId, GroupId = gid })
                    .ToList();
            db.ScheduleEvents.Add(ev);
            await db.SaveChangesAsync();
            return ev;
        }
        else
        {
            existing.CampDay             = dto.CampDay;
            existing.StartTime           = dto.StartTime;
            existing.EndTime             = dto.EndTime;
            existing.Title               = dto.Title;
            existing.Description         = dto.Description;
            existing.LocationDisplayName = dto.LocationDisplayName;
            existing.EventType           = dto.EventType;
            existing.AppliesToAllGroups  = dto.AppliesToAllGroups;
            existing.MaxCapacity         = dto.MaxCapacity;
            existing.UpdatedAt           = DateTime.UtcNow;

            db.ScheduleEventGroups.RemoveRange(existing.EventGroups);
            existing.EventGroups = dto.AppliesToAllGroups
                ? []
                : dto.GroupIds
                    .Select(gid => new ScheduleEventGroup { ScheduleEventId = existing.ScheduleEventId, GroupId = gid })
                    .ToList();

            await db.SaveChangesAsync();
            return existing;
        }
    }

    public async Task DeleteAsync(Guid scheduleEventId)
    {
        using var db = factory.CreateDbContext();
        var ev = await db.ScheduleEvents
            .Include(e => e.EventGroups)
            .FirstOrDefaultAsync(e => e.ScheduleEventId == scheduleEventId);
        if (ev is null) return;
        db.ScheduleEventGroups.RemoveRange(ev.EventGroups);
        db.ScheduleEvents.Remove(ev);
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Register all four services in `Program.cs`** after `AddScoped<MiniGameService>()`:

```csharp
builder.Services.AddScoped<InfoPageService>();
builder.Services.AddScoped<StaffDirectoryService>();
builder.Services.AddScoped<AnnouncementService>();
builder.Services.AddScoped<ScheduleService>();
```

- [ ] **Add `AccessDeniedPath = "/"` to cookie auth** in the `.AddCookie(opt => { ... })` block:

```csharp
opt.AccessDeniedPath = "/";
```

This ensures Display users hitting any `/hub/*` route are redirected to Dashboard rather than an error page.

- [ ] **Add Markdig NuGet**

```powershell
cd CampClotNot; dotnet add package Markdig
```

- [ ] **Build + commit**

```powershell
dotnet build
git add CampClotNot/Services/ CampClotNot/Program.cs CampClotNot/CampClotNot.csproj
git commit -m "feat: Hub service classes (InfoPage, StaffDirectory, Announcement, Schedule) + Markdig"
```

---

## Task 8: HubSubNav Component

**Files:** `CampClotNot/Pages/Hub/HubSubNav.razor`

- [ ] **Create `CampClotNot/Pages/Hub/HubSubNav.razor`**

```razor
@inject NavigationManager Nav

<style>
    .hub-subnav {
        display: flex;
        border-bottom: 3px solid var(--black);
        margin-bottom: 20px;
        background: var(--panel-bg);
    }
    .hub-tab {
        flex: 1;
        padding: 12px 8px;
        text-align: center;
        font-family: 'Fredoka One', cursive;
        font-size: 13px;
        color: var(--text-light);
        text-decoration: none;
        border-bottom: 4px solid transparent;
        transition: color .1s, border-color .1s;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 2px;
    }
    .hub-tab .tab-icon { font-size: 18px; line-height: 1; }
    .hub-tab.active {
        color: var(--black);
        border-bottom-color: var(--black);
        background: var(--bg-base);
    }
    @@media (min-width: 480px) {
        .hub-tab { font-size: 14px; flex-direction: row; justify-content: center; gap: 6px; }
    }
</style>

<nav class="hub-subnav">
    <a href="/hub/schedule"      class="hub-tab @(IsActive("/hub/schedule")      ? "active" : "")">
        <span class="tab-icon">📅</span><span>Schedule</span>
    </a>
    <a href="/hub/announcements" class="hub-tab @(IsActive("/hub/announcements") ? "active" : "")">
        <span class="tab-icon">📣</span><span>Announcements</span>
    </a>
    <a href="/hub/staff"         class="hub-tab @(IsActive("/hub/staff")         ? "active" : "")">
        <span class="tab-icon">👥</span><span>Staff</span>
    </a>
    <a href="/hub/info"          class="hub-tab @(IsActive("/hub/info")          ? "active" : "")">
        <span class="tab-icon">📋</span><span>Info</span>
    </a>
</nav>

@code {
    private bool IsActive(string href) =>
        ("/" + Nav.ToBaseRelativePath(Nav.Uri))
            .StartsWith(href, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/Pages/Hub/HubSubNav.razor
git commit -m "feat: HubSubNav shared sub-navigation component"
```

---

## Task 9: Hub Redirect + Schedule Page

**Files:** `CampClotNot/Pages/Hub/Hub.razor`, `CampClotNot/Pages/Hub/Schedule.razor`

- [ ] **Create `CampClotNot/Pages/Hub/Hub.razor`**

```razor
@page "/hub"
@attribute [Authorize]
@inject NavigationManager Nav

@code {
    protected override void OnInitialized()
    {
        Nav.NavigateTo("/hub/schedule");
    }
}
```

`[Authorize]` (no roles) lets any authenticated user reach this page momentarily before the redirect fires. Display users hitting `/hub/schedule` (which uses `[Authorize(Roles = "Admin,Staff")]`) will be caught by `AccessDeniedPath = "/"`.

- [ ] **Create `CampClotNot/Pages/Hub/Schedule.razor`**

```razor
@page "/hub/schedule"
@attribute [Authorize(Roles = "Admin,Staff")]
@inject ScheduleService ScheduleSvc
@inject AuthenticationStateProvider Auth

<PageTitle>Schedule — Camp Clot Not</PageTitle>

<HubSubNav />

<div style="padding: 0 16px 100px">
    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:16px">
        <h2 style="font-family:'Fredoka One',cursive;font-size:22px;margin:0">📅 Schedule</h2>
        @if (_canEdit)
        {
            <button class="ccn-btn" @onclick="OpenAddForm">+ Add Event</button>
        }
    </div>

    @if (_loading)
    {
        <MudProgressCircular Indeterminate="true" />
    }
    else if (!_eventsByDay.Any())
    {
        <p style="color:var(--text-light);font-family:'Fredoka One',cursive">No events scheduled yet.</p>
    }
    else
    {
        @foreach (var (day, events) in _eventsByDay)
        {
            var isToday = day == DateOnly.FromDateTime(DateTime.Today);
            var expanded = _expandedDays.Contains(day);
            <div style="border:3px solid var(--black);border-radius:10px;margin-bottom:12px;box-shadow:3px 3px 0 var(--black);overflow:hidden">
                <button style="width:100%;padding:12px 16px;background:@(isToday ? "var(--color-primary)" : "var(--panel-bg)");border:none;cursor:pointer;display:flex;justify-content:space-between;align-items:center;font-family:'Fredoka One',cursive;font-size:16px;color:@(isToday ? "white" : "var(--black)")"
                        @onclick="() => ToggleDay(day)">
                    <span>@day.ToString("dddd, MMMM d") @(isToday ? "— TODAY" : "")</span>
                    <span>@(expanded ? "▲" : "▼") (@events.Count)</span>
                </button>
                @if (expanded)
                {
                    <div style="padding:8px 0">
                        @foreach (var ev in events)
                        {
                            <div style="padding:10px 16px;border-bottom:2px solid #E8E0CC;display:flex;gap:12px;align-items:flex-start">
                                <div style="min-width:70px;font-family:'Fredoka One',cursive;font-size:13px;color:var(--text-light)">
                                    @ev.StartTime.ToString("h:mm tt")
                                    @if (ev.EndTime.HasValue)
                                    {
                                        <br />@ev.EndTime.Value.ToString("h:mm tt")
                                    }
                                </div>
                                <div style="flex:1">
                                    <div style="font-family:'Fredoka One',cursive;font-size:15px">@ev.Title</div>
                                    @if (!string.IsNullOrEmpty(ev.LocationDisplayName))
                                    {
                                        <div style="font-size:13px;color:var(--text-light)">📍 @ev.LocationDisplayName</div>
                                    }
                                    <span style="font-size:11px;padding:2px 8px;border-radius:4px;background:@EventTypeBadgeColor(ev.EventType);color:white;font-family:'Fredoka One',cursive">
                                        @ev.EventType
                                    </span>
                                    @if (!ev.AppliesToAllGroups && ev.EventGroups.Any())
                                    {
                                        <span style="font-size:12px;color:var(--text-light);margin-left:6px">
                                            @string.Join(", ", ev.EventGroups.Select(eg => eg.Group.ShortName))
                                        </span>
                                    }
                                </div>
                                @if (_canEdit)
                                {
                                    <div style="display:flex;gap:4px">
                                        <button class="ccn-btn" style="font-size:11px;padding:4px 8px" @onclick="() => OpenEditForm(ev)">Edit</button>
                                        <button class="ccn-btn" style="font-size:11px;padding:4px 8px;background:#D12B2B" @onclick="() => DeleteEvent(ev.ScheduleEventId)">Del</button>
                                    </div>
                                }
                            </div>
                        }
                    </div>
                }
            </div>
        }
    }

    @if (_showForm)
    {
        <div style="position:fixed;inset:0;background:rgba(26,26,26,.5);z-index:100;display:flex;align-items:center;justify-content:center;padding:16px" @onclick="CloseForm">
            <div style="background:var(--panel-bg);border:3px solid var(--black);border-radius:12px;box-shadow:4px 4px 0 var(--black);padding:24px;width:100%;max-width:500px;max-height:90vh;overflow-y:auto" @onclick:stopPropagation>
                <h3 style="font-family:'Fredoka One',cursive;margin:0 0 16px">@(_editingId == Guid.Empty ? "Add Event" : "Edit Event")</h3>
                <MudTextField @bind-Value="_fTitle"    Label="Title" Required="true" Class="mb-3" />
                <MudDatePicker @bind-Date="_fDate"     Label="Day" Required="true" Class="mb-3" />
                <MudTimePicker @bind-Time="_fStart"    Label="Start Time" Required="true" Class="mb-3" />
                <MudTimePicker @bind-Time="_fEnd"      Label="End Time (optional)" Class="mb-3" />
                <MudSelect @bind-Value="_fType"        Label="Event Type" Class="mb-3">
                    @foreach (var t in Enum.GetValues<ScheduleEventType>())
                    {
                        <MudSelectItem Value="@t">@t</MudSelectItem>
                    }
                </MudSelect>
                <MudTextField @bind-Value="_fLocation" Label="Location (optional)" Class="mb-3" />
                <MudTextField @bind-Value="_fDesc"     Label="Description (optional)" Lines="2" Class="mb-3" />
                <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
                    <button class="ccn-btn" style="background:transparent;border-color:var(--black);color:var(--black)" @onclick="CloseForm">Cancel</button>
                    <button class="ccn-btn" @onclick="SaveEvent">Save</button>
                </div>
            </div>
        </div>
    }
</div>

@code {
    private bool _loading = true;
    private bool _canEdit;
    private bool _showForm;
    private Dictionary<DateOnly, List<ScheduleEvent>> _eventsByDay = [];
    private HashSet<DateOnly> _expandedDays = [];
    private Guid _editingId;
    private Guid _userId;
    private string _fTitle = "";
    private string? _fLocation;
    private string? _fDesc;
    private DateTime? _fDate;
    private TimeSpan? _fStart;
    private TimeSpan? _fEnd;
    private ScheduleEventType _fType = ScheduleEventType.Activity;

    protected override async Task OnInitializedAsync()
    {
        var authState = await Auth.GetAuthenticationStateAsync();
        _canEdit = authState.User.IsInRole("Admin") || authState.User.IsInRole("Staff");
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);

        await LoadEvents();
    }

    private async Task LoadEvents()
    {
        var events = await ScheduleSvc.GetByEventAsync(SeedService.Id.EventCcn2026);
        _eventsByDay = events
            .GroupBy(e => e.CampDay)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.StartTime).ToList());

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_eventsByDay.ContainsKey(today))
            _expandedDays.Add(today);
        else if (_eventsByDay.Any())
            _expandedDays.Add(_eventsByDay.Keys.First());

        _loading = false;
    }

    private void ToggleDay(DateOnly day)
    {
        if (!_expandedDays.Remove(day)) _expandedDays.Add(day);
    }

    private void OpenAddForm()
    {
        _editingId = Guid.Empty;
        _fTitle = ""; _fLocation = null; _fDesc = null;
        _fDate = DateTime.Today; _fStart = new TimeSpan(9, 0, 0); _fEnd = null;
        _fType = ScheduleEventType.Activity;
        _showForm = true;
    }

    private void OpenEditForm(ScheduleEvent ev)
    {
        _editingId  = ev.ScheduleEventId;
        _fTitle     = ev.Title;
        _fLocation  = ev.LocationDisplayName;
        _fDesc      = ev.Description;
        _fDate      = ev.CampDay.ToDateTime(TimeOnly.MinValue);
        _fStart     = ev.StartTime.ToTimeSpan();
        _fEnd       = ev.EndTime?.ToTimeSpan();
        _fType      = ev.EventType;
        _showForm   = true;
    }

    private void CloseForm() => _showForm = false;

    private async Task SaveEvent()
    {
        if (string.IsNullOrWhiteSpace(_fTitle) || _fDate is null || _fStart is null) return;

        var dto = new ScheduleEventDto(
            _editingId,
            SeedService.Id.EventCcn2026,
            DateOnly.FromDateTime(_fDate.Value),
            TimeOnly.FromTimeSpan(_fStart.Value),
            _fEnd.HasValue ? TimeOnly.FromTimeSpan(_fEnd.Value) : null,
            _fTitle, _fDesc, _fLocation, _fType, true, null, []
        );
        await ScheduleSvc.UpsertAsync(dto, _userId);
        _showForm = false;
        await LoadEvents();
    }

    private async Task DeleteEvent(Guid id)
    {
        await ScheduleSvc.DeleteAsync(id);
        await LoadEvents();
    }

    private static string EventTypeBadgeColor(ScheduleEventType t) => t switch
    {
        ScheduleEventType.Meal      => "#E67E22",
        ScheduleEventType.Travel    => "#2980B9",
        ScheduleEventType.Free      => "#27AE60",
        ScheduleEventType.Mandatory => "#D12B2B",
        _                           => "#8E44AD"
    };
}
```

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/Pages/Hub/
git commit -m "feat: Hub redirect page and Schedule page"
```

---

## Task 10: Announcements Page

**Files:** `CampClotNot/Pages/Hub/Announcements.razor`

- [ ] **Create `CampClotNot/Pages/Hub/Announcements.razor`**

```razor
@page "/hub/announcements"
@attribute [Authorize(Roles = "Admin,Staff")]
@inject AnnouncementService AnnouncementSvc
@inject AuthenticationStateProvider Auth

<PageTitle>Announcements — Camp Clot Not</PageTitle>

<HubSubNav />

<div style="padding: 0 16px 100px">
    <h2 style="font-family:'Fredoka One',cursive;font-size:22px;margin:0 0 16px">📣 Announcements</h2>

    @if (_loading)
    {
        <MudProgressCircular Indeterminate="true" />
    }
    else
    {
        <div style="border:3px solid var(--black);border-radius:10px;padding:16px;margin-bottom:20px;box-shadow:3px 3px 0 var(--black);background:var(--panel-bg)">
            <h3 style="font-family:'Fredoka One',cursive;font-size:16px;margin:0 0 12px">New Announcement</h3>
            <MudTextField @bind-Value="_newTitle" Label="Title" Required="true" Class="mb-2" />
            <MudTextField @bind-Value="_newBody"  Label="Message" Lines="3" Class="mb-2" />
            @if (_isAdmin)
            {
                <MudSelect @bind-Value="_newPriority" Label="Priority" Class="mb-2">
                    <MudSelectItem Value="@AnnouncementPriority.Normal">Normal</MudSelectItem>
                    <MudSelectItem Value="@AnnouncementPriority.Urgent">Urgent</MudSelectItem>
                </MudSelect>
            }
            <button class="ccn-btn" @onclick="Post" disabled="@string.IsNullOrWhiteSpace(_newTitle)">Post</button>
        </div>

        @foreach (var a in _feed)
        {
            var urgent = a.Priority == AnnouncementPriority.Urgent;
            <div style="border:3px solid var(--black);border-radius:10px;margin-bottom:12px;box-shadow:3px 3px 0 var(--black);overflow:hidden;border-left-width:@(urgent ? "6px" : "3px");border-left-color:@(urgent ? "#D12B2B" : "var(--black)")">
                <div style="padding:14px 16px;background:var(--panel-bg)">
                    <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:8px">
                        <div style="flex:1">
                            @if (urgent)
                            {
                                <span style="font-size:11px;background:#D12B2B;color:white;padding:2px 8px;border-radius:4px;font-family:'Fredoka One',cursive;margin-right:6px">URGENT</span>
                            }
                            @if (a.IsPinned)
                            {
                                <span style="font-size:11px;background:#F5C800;color:#1A1A1A;padding:2px 8px;border-radius:4px;font-family:'Fredoka One',cursive;margin-right:6px">Pinned</span>
                            }
                            <span style="font-family:'Fredoka One',cursive;font-size:16px">@a.Title</span>
                            <p style="margin:8px 0 4px;font-size:14px">@a.Body</p>
                            <div style="font-size:12px;color:var(--text-light)">
                                @a.Author.Email • @a.CreatedAt.ToLocalTime().ToString("MMM d, h:mm tt")
                            </div>
                        </div>
                        @if (_isAdmin)
                        {
                            <div style="display:flex;gap:4px;flex-shrink:0">
                                <button class="ccn-btn" style="font-size:11px;padding:4px 8px" @onclick="() => TogglePin(a)">@(a.IsPinned ? "Unpin" : "Pin")</button>
                                <button class="ccn-btn" style="font-size:11px;padding:4px 8px;background:#555" @onclick="() => Archive(a.AnnouncementId)">Archive</button>
                            </div>
                        }
                    </div>
                </div>
            </div>
        }
        @if (!_feed.Any())
        {
            <p style="color:var(--text-light);font-family:'Fredoka One',cursive">No announcements yet.</p>
        }
    }
</div>

@code {
    private bool _loading = true;
    private bool _isAdmin;
    private Guid _userId;
    private List<Announcement> _feed = [];
    private string _newTitle = "";
    private string _newBody = "";
    private AnnouncementPriority _newPriority = AnnouncementPriority.Normal;

    protected override async Task OnInitializedAsync()
    {
        var authState = await Auth.GetAuthenticationStateAsync();
        _isAdmin = authState.User.IsInRole("Admin");
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);
        _feed = await AnnouncementSvc.GetFeedAsync();
        _loading = false;
    }

    private async Task Post()
    {
        if (string.IsNullOrWhiteSpace(_newTitle)) return;
        var priority = _isAdmin ? _newPriority : AnnouncementPriority.Normal;
        await AnnouncementSvc.PostAsync(_newTitle, _newBody, priority, _userId);
        _newTitle = ""; _newBody = ""; _newPriority = AnnouncementPriority.Normal;
        _feed = await AnnouncementSvc.GetFeedAsync();
    }

    private async Task TogglePin(Announcement a)
    {
        await AnnouncementSvc.PinAsync(a.AnnouncementId, !a.IsPinned);
        _feed = await AnnouncementSvc.GetFeedAsync();
    }

    private async Task Archive(Guid id)
    {
        await AnnouncementSvc.ArchiveAsync(id);
        _feed = await AnnouncementSvc.GetFeedAsync();
    }
}
```

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/Pages/Hub/Announcements.razor
git commit -m "feat: Announcements Hub page"
```

---

## Task 11: Staff Directory Page

**Files:** `CampClotNot/Pages/Hub/Staff.razor`

- [ ] **Create `CampClotNot/Pages/Hub/Staff.razor`**

```razor
@page "/hub/staff"
@attribute [Authorize(Roles = "Admin,Staff")]
@inject StaffDirectoryService StaffSvc
@inject AuthenticationStateProvider Auth

<PageTitle>Staff — Camp Clot Not</PageTitle>

<HubSubNav />

<div style="padding: 0 16px 100px">
    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:16px">
        <h2 style="font-family:'Fredoka One',cursive;font-size:22px;margin:0">👥 Staff Directory</h2>
        @if (_isAdmin)
        {
            <button class="ccn-btn" @onclick="OpenAdd">+ Add Staff</button>
        }
    </div>

    @if (_loading)
    {
        <MudProgressCircular Indeterminate="true" />
    }
    else if (!_members.Any())
    {
        <p style="color:var(--text-light);font-family:'Fredoka One',cursive">No staff listed yet.</p>
    }
    else
    {
        <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:12px">
            @foreach (var m in _members)
            {
                <div style="border:3px solid var(--black);border-radius:10px;padding:16px;box-shadow:3px 3px 0 var(--black);background:var(--panel-bg);text-align:center">
                    <div style="font-size:36px;margin-bottom:8px">@m.AvatarEmoji</div>
                    <div style="font-family:'Fredoka One',cursive;font-size:15px">@m.DisplayName</div>
                    <div style="font-size:12px;color:var(--text-light);margin-bottom:8px">@m.RoleTitle</div>
                    @if (!string.IsNullOrEmpty(m.Phone))
                    {
                        <a href="tel:@m.Phone" style="display:block;font-size:13px;color:var(--color-primary);text-decoration:none;margin-bottom:4px">@m.Phone</a>
                    }
                    @if (!string.IsNullOrEmpty(m.Email))
                    {
                        <a href="mailto:@m.Email" style="display:block;font-size:13px;color:var(--color-primary);text-decoration:none">@m.Email</a>
                    }
                    @if (_isAdmin)
                    {
                        <div style="display:flex;gap:4px;margin-top:10px;justify-content:center">
                            <button class="ccn-btn" style="font-size:11px;padding:4px 8px" @onclick="() => OpenEdit(m)">Edit</button>
                            <button class="ccn-btn" style="font-size:11px;padding:4px 8px;background:#D12B2B" @onclick="() => Delete(m.StaffMemberId)">Del</button>
                        </div>
                    }
                </div>
            }
        </div>
    }

    @if (_showForm && _isAdmin)
    {
        <div style="position:fixed;inset:0;background:rgba(26,26,26,.5);z-index:100;display:flex;align-items:center;justify-content:center;padding:16px" @onclick="Close">
            <div style="background:var(--panel-bg);border:3px solid var(--black);border-radius:12px;box-shadow:4px 4px 0 var(--black);padding:24px;width:100%;max-width:400px" @onclick:stopPropagation>
                <h3 style="font-family:'Fredoka One',cursive;margin:0 0 16px">@(_form.StaffMemberId == Guid.Empty ? "Add Staff Member" : "Edit Staff Member")</h3>
                <MudTextField @bind-Value="_form.DisplayName" Label="Name" Required="true" Class="mb-2" />
                <MudTextField @bind-Value="_form.RoleTitle"   Label="Role/Title" Class="mb-2" />
                <MudTextField @bind-Value="_form.AvatarEmoji" Label="Avatar Emoji" Class="mb-2" />
                <MudTextField @bind-Value="_form.Phone"       Label="Phone (optional)" Class="mb-2" />
                <MudTextField @bind-Value="_form.Email"       Label="Email (optional)" Class="mb-2" />
                <MudSwitch @bind-Value="_form.IsVisible" Label="Visible in directory" Color="Color.Primary" Class="mb-2" />
                <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
                    <button class="ccn-btn" style="background:transparent;border-color:var(--black);color:var(--black)" @onclick="Close">Cancel</button>
                    <button class="ccn-btn" @onclick="Save">Save</button>
                </div>
            </div>
        </div>
    }
</div>

@code {
    private bool _loading = true;
    private bool _isAdmin;
    private bool _showForm;
    private List<StaffMember> _members = [];
    private StaffMember _form = new() { AvatarEmoji = "👤", IsVisible = true };

    protected override async Task OnInitializedAsync()
    {
        var authState = await Auth.GetAuthenticationStateAsync();
        _isAdmin = authState.User.IsInRole("Admin");
        _members = _isAdmin ? await StaffSvc.GetAllAsync() : await StaffSvc.GetVisibleAsync();
        _loading = false;
    }

    private void OpenAdd() { _form = new() { AvatarEmoji = "👤", IsVisible = true }; _showForm = true; }

    private void OpenEdit(StaffMember m)
    {
        _form = new()
        {
            StaffMemberId = m.StaffMemberId, DisplayName = m.DisplayName, RoleTitle = m.RoleTitle,
            AvatarEmoji = m.AvatarEmoji, Phone = m.Phone, Email = m.Email,
            IsVisible = m.IsVisible, SortOrder = m.SortOrder, LinkedUserId = m.LinkedUserId
        };
        _showForm = true;
    }

    private void Close() => _showForm = false;

    private async Task Save()
    {
        await StaffSvc.UpsertAsync(_form);
        _showForm = false;
        await OnInitializedAsync();
    }

    private async Task Delete(Guid id)
    {
        await StaffSvc.DeleteAsync(id);
        await OnInitializedAsync();
    }
}
```

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/Pages/Hub/Staff.razor
git commit -m "feat: Staff Directory Hub page"
```

---

## Task 12: Info Pages

**Files:** `CampClotNot/Pages/Hub/Info.razor`

- [ ] **Create `CampClotNot/Pages/Hub/Info.razor`**

```razor
@page "/hub/info"
@attribute [Authorize(Roles = "Admin,Staff")]
@inject InfoPageService InfoSvc
@inject AuthenticationStateProvider Auth
@inject NavigationManager Nav

<PageTitle>Info — Camp Clot Not</PageTitle>

<HubSubNav />

<div style="padding: 0 16px 100px">
    @if (_loading)
    {
        <MudProgressCircular Indeterminate="true" />
    }
    else
    {
        <div style="display:flex;gap:16px;align-items:flex-start;flex-wrap:wrap">
            <div style="min-width:160px;border:3px solid var(--black);border-radius:10px;overflow:hidden;box-shadow:3px 3px 0 var(--black);background:var(--panel-bg)">
                @foreach (var p in _pages)
                {
                    <a href="/hub/info?slug=@p.Slug"
                       style="display:block;padding:12px 16px;font-family:'Fredoka One',cursive;font-size:14px;text-decoration:none;border-bottom:2px solid #E8E0CC;color:@(p.Slug == _activeSlug ? "white" : "var(--black)");background:@(p.Slug == _activeSlug ? "var(--black)" : "transparent")">
                        @p.IconEmoji @p.Title
                    </a>
                }
            </div>

            <div style="flex:1;min-width:260px">
                @if (_activePage is not null)
                {
                    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px">
                        <h2 style="font-family:'Fredoka One',cursive;font-size:20px;margin:0">@_activePage.IconEmoji @_activePage.Title</h2>
                        @if (_isAdmin && !_editing)
                        {
                            <button class="ccn-btn" style="font-size:12px;padding:6px 12px" @onclick="StartEdit">Edit</button>
                        }
                    </div>

                    @if (_editing && _isAdmin)
                    {
                        <textarea @bind="_editBody" rows="16"
                                  style="width:100%;font-family:monospace;font-size:13px;padding:12px;border:3px solid var(--black);border-radius:8px;background:var(--bg-base);resize:vertical;box-sizing:border-box"></textarea>
                        <div style="display:flex;gap:8px;margin-top:8px">
                            <button class="ccn-btn" style="background:transparent;border-color:var(--black);color:var(--black)" @onclick="() => _editing = false">Cancel</button>
                            <button class="ccn-btn" @onclick="SaveEdit">Save</button>
                        </div>
                    }
                    else
                    {
                        <div style="border:3px solid var(--black);border-radius:10px;padding:20px;background:var(--panel-bg);box-shadow:3px 3px 0 var(--black)">
                            @((MarkupString)Markdig.Markdown.ToHtml(_activePage.Body ?? ""))
                        </div>
                        <p style="font-size:12px;color:var(--text-light);margin-top:8px">
                            Last updated @_activePage.UpdatedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt")
                        </p>
                    }
                }
            </div>
        </div>
    }
</div>

@code {
    private bool _loading = true;
    private bool _isAdmin;
    private bool _editing;
    private Guid _userId;
    private List<InfoPage> _pages = [];
    private InfoPage? _activePage;
    private string _activeSlug = "";
    private string _editBody = "";

    protected override async Task OnInitializedAsync()
    {
        var authState = await Auth.GetAuthenticationStateAsync();
        _isAdmin = authState.User.IsInRole("Admin");
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);

        _pages = await InfoSvc.GetAllAsync();
        var qs = System.Web.HttpUtility.ParseQueryString(new Uri(Nav.Uri).Query);
        _activeSlug = qs["slug"] ?? _pages.FirstOrDefault()?.Slug ?? "";
        _activePage = _pages.FirstOrDefault(p => p.Slug == _activeSlug);
        _loading = false;
    }

    private void StartEdit() { _editBody = _activePage?.Body ?? ""; _editing = true; }

    private async Task SaveEdit()
    {
        if (_activePage is null) return;
        await InfoSvc.UpdateBodyAsync(_activePage.PageId, _editBody, _userId);
        _activePage.Body = _editBody;
        _editing = false;
    }
}
```

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/Pages/Hub/Info.razor
git commit -m "feat: Info Pages Hub page with Markdig markdown rendering"
```

---

## Task 13: AppNav Hub Tab

**Files:** `CampClotNot/Shared/AppNav.razor`

- [ ] **Add Hub link in the desktop header** — inside the `<div style="display:flex;gap:6px...">` block, after the Activities dropdown:

```razor
<AuthorizeView Roles="Admin,Staff">
    <a href="/hub" class="nav-link" style="@ActiveStyle("/hub", "#2980B9")">📋 Hub</a>
</AuthorizeView>
```

- [ ] **Add Hub tab to the mobile bottom bar** — between the Activities tab and the Admin tab:

```razor
<AuthorizeView Roles="Admin,Staff" Context="hubMobileCtx">
    <a href="/hub/schedule" class="nav-tab @(IsActive("/hub") ? "active" : "")">
        <span class="tab-icon">📋</span>
        <span>Hub</span>
    </a>
</AuthorizeView>
```

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/Shared/AppNav.razor
git commit -m "feat: Hub tab in desktop header and mobile bottom bar"
```

---

## Task 14: Manual Test — Hub

- [ ] **Start the app**

```powershell
cd CampClotNot; dotnet run
```

- [ ] **Test as Admin (`tyler@hbda.local` / `DevAdmin1!`)**
  - Hub tab appears in desktop header and mobile bottom bar
  - `/hub` redirects to `/hub/schedule`
  - Schedule: empty state → Add Event form → event persists after reload → edit/delete work → today auto-expands
  - Announcements: post Normal → post Urgent → pin → archive removes item
  - Staff: add card → tel: and mailto: links appear → edit/delete work
  - Info: 5 sidebar pages appear → content renders as markdown → Edit → Save updates body

- [ ] **Test as Staff** (create a Staff user at `/admin/users`)
  - Hub tab appears; can add schedule events; can post Normal; cannot post Urgent; Staff and Info are read-only; no pin/archive buttons on announcements

- [ ] **Test as Display** (create a Display user at `/admin/users`)
  - Hub tab does NOT appear
  - Direct navigate to `http://localhost:XXXX/hub` → redirects to `/`
  - Direct navigate to `http://localhost:XXXX/hub/schedule` → redirects to `/`

- [ ] **Stop the app and commit any fixes found during testing**

---

## Task 15: PWA Manifest + Layout

**Files:** `CampClotNot/wwwroot/manifest.json`, `CampClotNot/Pages/_Layout.cshtml`

- [ ] **Create `CampClotNot/wwwroot/manifest.json`**

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
    {
      "src": "/icons/icon-512-maskable.png",
      "sizes": "512x512",
      "type": "image/png",
      "purpose": "maskable"
    }
  ]
}
```

- [ ] **Add PWA tags to `CampClotNot/Pages/_Layout.cshtml`** in `<head>`, after existing link tags:

```html
<link rel="manifest" href="/manifest.json" />
<meta name="theme-color" content="#3d0066" />
<meta name="apple-mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
<meta name="apple-mobile-web-app-title" content="Clot Not" />
<link rel="apple-touch-icon" href="/icons/icon-192.png" />
```

- [ ] **Add service worker registration** in `_Layout.cshtml` before `</body>`:

```html
<script>
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/service-worker.js')
            .catch(function(err) { console.warn('SW registration failed:', err); });
    }
</script>
```

- [ ] **Create icon placeholders** — Tyler replaces these with real icons generated from the CCN logo via PWABuilder before camp:

```powershell
New-Item -ItemType Directory -Force CampClotNot/wwwroot/icons
Copy-Item CampClotNot/wwwroot/img/ccn-logo-nav.png CampClotNot/wwwroot/icons/icon-192.png
Copy-Item CampClotNot/wwwroot/img/ccn-logo-nav.png CampClotNot/wwwroot/icons/icon-512.png
Copy-Item CampClotNot/wwwroot/img/ccn-logo-nav.png CampClotNot/wwwroot/icons/icon-512-maskable.png
```

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/wwwroot/manifest.json CampClotNot/Pages/_Layout.cshtml CampClotNot/wwwroot/icons/
git commit -m "feat: PWA manifest, Apple meta tags, SW registration, icon placeholders"
```

---

## Task 16: Service Worker + Offline Page

**Files:** `CampClotNot/wwwroot/service-worker.js`, `CampClotNot/wwwroot/offline.html`

- [ ] **Create `CampClotNot/wwwroot/service-worker.js`**

```javascript
const CACHE_NAME = 'ccn-shell-v1';
const SHELL_ASSETS = [
    '/offline.html',
    '/app.css',
    '/_content/MudBlazor/MudBlazor.min.css',
    '/_content/MudBlazor/MudBlazor.min.js',
    '/icons/icon-192.png',
    '/icons/icon-512.png'
];
const SKIP_PREFIXES = ['/livehub', '/account/', '/api/'];

self.addEventListener('install', function(event) {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(function(cache) {
                return cache.addAll(SHELL_ASSETS);
            })
            .then(function() { return self.skipWaiting(); })
    );
});

self.addEventListener('activate', function(event) {
    event.waitUntil(
        caches.keys().then(function(keys) {
            return Promise.all(
                keys.filter(function(k) { return k !== CACHE_NAME; })
                    .map(function(k) { return caches.delete(k); })
            );
        }).then(function() { return self.clients.claim(); })
    );
});

self.addEventListener('fetch', function(event) {
    var url = new URL(event.request.url);
    var skip = SKIP_PREFIXES.some(function(p) { return url.pathname.startsWith(p); });
    if (skip || event.request.method !== 'GET') return;

    event.respondWith(
        caches.match(event.request).then(function(cached) {
            return cached || fetch(event.request).catch(function() {
                return caches.match('/offline.html');
            });
        })
    );
});
```

- [ ] **Create `CampClotNot/wwwroot/offline.html`**

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Offline — Camp Clot Not</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: system-ui, sans-serif;
            background: #F2ECD8;
            color: #1A1A1A;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 24px;
            text-align: center;
        }
        .card {
            background: white;
            border: 3px solid #1A1A1A;
            border-radius: 12px;
            box-shadow: 4px 4px 0 #1A1A1A;
            padding: 32px 24px;
            max-width: 360px;
            width: 100%;
        }
        h1 { font-size: 22px; margin-bottom: 8px; }
        p { font-size: 15px; color: #555; margin-bottom: 20px; }
        button {
            background: #1A1A1A;
            color: white;
            border: none;
            border-radius: 8px;
            padding: 12px 24px;
            font-size: 15px;
            cursor: pointer;
        }
        #scores { margin-top: 16px; font-size: 13px; color: #555; }
        #scores p { margin-bottom: 4px; }
    </style>
</head>
<body>
    <div class="card">
        <div style="font-size:48px;margin-bottom:16px">📵</div>
        <h1>You're Offline</h1>
        <p>Camp Clot Not requires a live connection. Check your signal and try again.</p>
        <button onclick="window.location.reload()">Try Again</button>
        <div id="scores"></div>
    </div>
    <script>
        (function() {
            try {
                var raw = localStorage.getItem('ccn_last_scores');
                if (!raw) return;
                var scores = JSON.parse(raw);
                if (!Array.isArray(scores) || scores.length === 0) return;
                var container = document.getElementById('scores');
                var heading = document.createElement('p');
                heading.style.fontWeight = 'bold';
                heading.style.marginTop = '12px';
                heading.textContent = 'Last known scores:';
                container.appendChild(heading);
                scores.forEach(function(s) {
                    var row = document.createElement('p');
                    // Use textContent throughout — no untrusted markup
                    row.textContent = s.name + ': ' + s.coins + ' coins, ' + s.stars + ' stars';
                    container.appendChild(row);
                });
            } catch (e) {}
        })();
    </script>
</body>
</html>
```

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/wwwroot/service-worker.js CampClotNot/wwwroot/offline.html
git commit -m "feat: service worker (cache-first shell) and offline fallback page"
```

---

## Task 17: Connection Quality Indicator

**Files:** `CampClotNot/wwwroot/js/connection-indicator.js`, `CampClotNot/Shared/ConnectionIndicator.razor`, `CampClotNot/Pages/_Layout.cshtml`, `CampClotNot/Shared/AppNav.razor`

- [ ] **Create `CampClotNot/wwwroot/js/connection-indicator.js`**

```javascript
window.connectionIndicator = {
    init: function(dotnetRef) {
        function update() {
            dotnetRef.invokeMethodAsync('SetOnline', navigator.onLine);
        }
        window.addEventListener('online', update);
        window.addEventListener('offline', update);
        document.addEventListener('blazor-reconnecting', function() {
            dotnetRef.invokeMethodAsync('SetOnline', false);
        });
        document.addEventListener('blazor-connected', function() {
            dotnetRef.invokeMethodAsync('SetOnline', true);
        });
    }
};
```

- [ ] **Add script tag** to `_Layout.cshtml` before `</body>` (after existing scripts):

```html
<script src="js/connection-indicator.js"></script>
```

- [ ] **Create `CampClotNot/Shared/ConnectionIndicator.razor`**

```razor
@inject IJSRuntime JS
@implements IAsyncDisposable

<span title="@_label"
      style="display:inline-block;width:10px;height:10px;border-radius:50%;background:@_color;border:2px solid rgba(0,0,0,.25);flex-shrink:0;align-self:center;margin-left:6px"></span>

@code {
    private string _label = "Connected";
    private string _color = "#27AE60";
    private DotNetObjectReference<ConnectionIndicator>? _ref;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _ref = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("connectionIndicator.init", _ref);
    }

    [JSInvokable]
    public void SetOnline(bool online)
    {
        _color = online ? "#27AE60" : "#D12B2B";
        _label = online ? "Connected" : "Offline";
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        await Task.CompletedTask;
    }
}
```

- [ ] **Add `<ConnectionIndicator />` to the mobile header in `AppNav.razor`**

In the `nav-mobile-header` div, wrap the logo and add the indicator:

```razor
<div style="display:flex;align-items:center;gap:6px">
    <div style="display:flex;flex-direction:column;align-items:flex-start;gap:2px">
        <img src="/img/ccn-logo-nav.png" alt="Camp Clot Not" style="height:36px;width:auto;object-fit:contain" />
        <span style="font-family:'Fredoka One',cursive;font-size:10px;color:var(--color-primary);letter-spacing:2px;text-transform:uppercase;line-height:1;margin-left:8px">SUPER PARTY '26</span>
    </div>
    <ConnectionIndicator />
</div>
```

Do the same in the desktop header's logo div.

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/wwwroot/js/connection-indicator.js CampClotNot/Shared/ConnectionIndicator.razor CampClotNot/Shared/AppNav.razor CampClotNot/Pages/_Layout.cshtml
git commit -m "feat: connection quality indicator dot in nav header"
```

---

## Task 18: Install Prompt Banner

**Files:** `CampClotNot/wwwroot/js/install-prompt.js`, `CampClotNot/Shared/InstallPromptBanner.razor`

- [ ] **Create `CampClotNot/wwwroot/js/install-prompt.js`**

```javascript
window.installPrompt = {
    _deferred: null,
    init: function(dotnetRef) {
        var dismissed = localStorage.getItem('ccn_install_dismissed') === '1';
        if (dismissed) return;

        window.addEventListener('beforeinstallprompt', function(e) {
            e.preventDefault();
            window.installPrompt._deferred = e;
            dotnetRef.invokeMethodAsync('ShowAndroidBanner');
        });

        var isIos = /iphone|ipad|ipod/i.test(navigator.userAgent);
        var isStandalone = window.navigator.standalone === true;
        if (isIos && !isStandalone) {
            dotnetRef.invokeMethodAsync('ShowIosBanner');
        }
    },
    trigger: function() {
        if (window.installPrompt._deferred) {
            window.installPrompt._deferred.prompt();
            window.installPrompt._deferred = null;
        }
    },
    dismiss: function() {
        localStorage.setItem('ccn_install_dismissed', '1');
    },
    isDismissed: function() {
        return localStorage.getItem('ccn_install_dismissed') === '1';
    }
};
```

- [ ] **Add script tag** to `_Layout.cshtml` before `</body>`:

```html
<script src="js/install-prompt.js"></script>
```

- [ ] **Create `CampClotNot/Shared/InstallPromptBanner.razor`**

```razor
@inject IJSRuntime JS
@implements IAsyncDisposable

@if (_show)
{
    <div style="position:fixed;bottom:70px;left:0;right:0;z-index:200;padding:0 12px;pointer-events:none">
        <div style="background:#1A1A1A;color:white;border-radius:10px;padding:14px 16px;display:flex;align-items:center;gap:12px;box-shadow:0 4px 12px rgba(0,0,0,.3);pointer-events:auto">
            <span style="font-size:22px;flex-shrink:0">📲</span>
            <span style="flex:1;font-size:14px">@_message</span>
            @if (_isAndroid)
            {
                <button onclick="window.installPrompt.trigger()"
                        style="background:var(--color-primary);color:white;border:none;border-radius:6px;padding:8px 14px;font-size:13px;cursor:pointer;white-space:nowrap">
                    Install
                </button>
            }
            <button @onclick="Dismiss"
                    style="background:transparent;border:none;color:#aaa;font-size:22px;cursor:pointer;padding:0 2px;line-height:1;flex-shrink:0">
                ×
            </button>
        </div>
    </div>
}

@code {
    private bool _show;
    private bool _isAndroid;
    private string _message = "";
    private DotNetObjectReference<InstallPromptBanner>? _ref;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var dismissed = await JS.InvokeAsync<bool>("installPrompt.isDismissed");
        if (dismissed) return;
        _ref = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("installPrompt.init", _ref);
    }

    [JSInvokable]
    public void ShowAndroidBanner()
    {
        _isAndroid = true;
        _message = "Install this app for quick access during camp.";
        _show = true;
        StateHasChanged();
    }

    [JSInvokable]
    public void ShowIosBanner()
    {
        _isAndroid = false;
        _message = "Add to your home screen: tap Share then Add to Home Screen.";
        _show = true;
        StateHasChanged();
    }

    private async Task Dismiss()
    {
        await JS.InvokeVoidAsync("installPrompt.dismiss");
        _show = false;
    }

    public async ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        await Task.CompletedTask;
    }
}
```

- [ ] **Add `<InstallPromptBanner />` to `MainLayout.razor`** at the bottom of the render tree, after `@Body`

- [ ] **Build + commit**

```powershell
cd CampClotNot; dotnet build
git add CampClotNot/wwwroot/js/install-prompt.js CampClotNot/Shared/InstallPromptBanner.razor CampClotNot/Shared/MainLayout.razor CampClotNot/Pages/_Layout.cshtml
git commit -m "feat: PWA install prompt banner (Android native prompt + iOS Safari instructions)"
```

---

## Task 19: Final Test + Open PR

- [ ] **Full smoke test**

```powershell
cd CampClotNot; dotnet run
```

Check:
- `dotnet build` 0 errors 0 warnings
- `/manifest.json` loads in browser (valid JSON, icons array present)
- Chrome DevTools → Application → Service Workers: SW registered and active
- Connection indicator dot visible in nav header
- All four Hub sections functional as Admin
- Display role redirects from `/hub` to `/`
- `/offline.html` loads at `http://localhost:XXXX/offline.html`

- [ ] **Open the PR**

```powershell
& "$env:LOCALAPPDATA\Programs\gh\gh.exe" pr create `
  --title "feat: v0.5.0 — Camp Info Hub + PWA" `
  --base dev `
  --body "## Summary
- Adds Camp Info Hub with Schedule, Announcements, Staff Directory, Info Pages (§8.1-§8.4)
- Four concrete service classes following MiniGameService pattern
- HubSubNav shared component + page-per-route Hub architecture under /hub/*
- AppNav Hub tab (desktop header + mobile bottom bar), Admin+Staff only
- AccessDeniedPath set to / so Display role redirects cleanly
- PWA: manifest, service worker, offline.html, connection indicator dot, install prompt banner
- Group seed cleanup (removed stale Group5/Group6 constants and startup loop)
- Markdig NuGet for Info Pages markdown rendering

## Deferred (documented, not in this PR)
- Dashboard pinned announcement banner (GetLatestPinnedAsync stub exists in AnnouncementService)
- Response caching (post-go-live performance pass)
- Guest attendee access / event code login (v1.x — ScheduleEvent.MaxCapacity field exists)
- Session registration for breakout sign-ups (v1.x — GuestAttendee entity sketched in spec)
- /admin/hub schedule and announcement designer (post-go-live)
- Real PWA icons — Tyler generates from CCN logo via PWABuilder before camp

## Test plan
- [ ] Smoke test all four Hub pages as Admin, Staff, and Display roles
- [ ] Verify Display role redirects to / on any /hub/* route
- [ ] Verify PWA manifest served at /manifest.json
- [ ] Verify service worker registers in Chrome DevTools Application tab
- [ ] dotnet build: 0 errors, 0 warnings

Generated with Claude Code"
```

---

## Self-Review

- [x] All 5 entities defined — `ScheduleEventId` PK (not `EventId`) avoids collision with Event FK pattern
- [x] `ScheduleEvent.CampEventId` FK scopes events consistently with Group/ScriptedMiniGame pattern
- [x] `AnnouncementService.GetLatestPinnedAsync()` stub exists, not called from Dashboard
- [x] `Dashboard.razor` untouched throughout this plan
- [x] `AccessDeniedPath = "/"` added to Program.cs for clean Display role redirect
- [x] All service methods use `IDbContextFactory` — no shared context across async boundaries
- [x] `Hub.razor` uses `Nav.NavigateTo` without `forceLoad:true` — safe during prerendering
- [x] PWA tags go in `_Layout.cshtml` (`<head>` lives there), not `_Host.cshtml`
- [x] `offline.html` score display uses DOM `textContent` only — no untrusted markup
- [x] Icon files are placeholders with note to replace before camp
- [x] No interfaces — concrete classes consistent with existing services
- [x] No caching — deferred per design decision
- [x] `ScheduleEventDto` record defined in `ScheduleService.cs` — used consistently in Schedule.razor
- [x] `SeedService.Id.EventCcn2026` referenced in Schedule.razor — already exists in SeedService
