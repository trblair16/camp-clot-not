# Attendance Tracking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record who attended which schedule item. Guests and staff tap "I'm here" on tracked items during a time window, and an Admin can check anyone in (including walk-ins by name), undo mistakes, see headcounts, and export CSV. The spec is `docs/superpowers/specs/2026-09-23-attendance-tracking-design.md` (issue #308).

**Architecture:** `ScheduleItem` gets an opt-in `TrackAttendance` flag. A new `ScheduleItemAttendance` table holds one row per person per tracked item, owned by either a `GuestAttendeeId` or a staff `UserId` (a DB check constraint enforces exactly one), and records how the check-in happened (`Self`/`Admin`) and by whom. A new `AttendanceService` owns the check-in window, identity resolution from the `ClaimsPrincipal`, roster assembly, and CSV building. `/hub/schedule` gets an "I'm here" button. A new Admin-only `/admin/attendance` page shows the day's tracked items, the roster, and a walk-in modal, and two Admin-only minimal-API endpoints serve the CSVs.

**Tech Stack:** Blazor Server (.NET 8), EF Core/Npgsql, ASP.NET Core cookie auth, minimal APIs.

## Global Constraints

- **Check-in window:** self check-in is allowed from `StartTime - 30 min` until `EndTime`, or `StartTime + 2h` when there's no `EndTime`, measured in camp wall-clock time (`CampTime.Now`). If `EndTime < StartTime`, the item crosses midnight and the end is the next day. Admin check-in ignores the window.
- **Idempotent inserts.** Two unique indexes, `(ScheduleItemId, GuestAttendeeId)` and `(ScheduleItemId, UserId)`, plus the existing catch-`DbUpdateException`-and-re-query race pattern from `GuestAccessService.RecordVisitAsync`. A duplicate check-in is a success (`AlreadyCheckedIn`), never an error.
- **Guests can only check in to their own event's items**: `item.CampEventId == GuestAccessService.GetGuestEventId(user)`.
- **Timestamps use `CampTime.Now`** (camp wall clock), matching `ScheduleItem.UpdatedAt`, because they're compared against `CampDay`/`StartTime` and shown to Vicki.
- No automated test suite exists for this project. Verification is `dotnet build` with no new warnings, plus the manual walkthrough in the PR description (final task).
- Never use PowerShell for source file text edits (CLAUDE.md pitfall #1).
- Always use synchronous `factory.CreateDbContext()` / `DbFactory.CreateDbContext()`, never the async variant (pitfall #14).
- Primary-constructor DI style for the new service (see `GuestAccessService`).
- Every FK gets an explicit `HasOne().WithMany().HasForeignKey()` (pitfall #13). This matters especially here, because `ScheduleItemAttendance` has **two** navigations to `User`.
- Modals use the `fadeIn`/`popIn` pattern (pitfall #12). FABs use `ccn-fab-mobile` (pitfall #11). Razor `@onclick` lambdas containing `$"..."` use single-quoted attributes (pitfall #15).
- Scope is limited to the spec. No session signups/capacity, no check-out times, no SignalR live roster, no PDF export, no non-Admin checkers.
- Work happens on branch `feature/308-attendance-tracking` (off latest `dev`). The PR targets `dev`.

### Environment (cloud sessions)

```bash
apt-get update && apt-get install -y dotnet-sdk-8.0
dotnet tool install -g dotnet-ef --version '8.*'
export PATH="$PATH:$HOME/.dotnet/tools"
```

Migrations are applied automatically at startup (`SeedService.SeedAsync()` → `MigrateAsync()`), so there's no manual prod step. Creating one needs a design-time connection string but no live DB:

```bash
cd CampClotNot
ConnectionStrings__DefaultConnection="Host=localhost;Database=dummy;Username=x;Password=x" \
Vapid__PublicKey=BPlaceholderPlaceholderPlaceholder Vapid__PrivateKey=x \
dotnet ef migrations add <Name>
```

---

### Task 1: Data model: `TrackAttendance` flag, `ScheduleItemAttendance` entity, migration

**Files:**
- Modify: `CampClotNot/Data/Enums.cs`
- Modify: `CampClotNot/Data/Entities/ScheduleEvent.cs` (holds the `ScheduleItem` class)
- Create: `CampClotNot/Data/Entities/ScheduleItemAttendance.cs`
- Modify: `CampClotNot/Data/AppDbContext.cs`
- Create: EF migration `AddAttendanceTracking`

**Interfaces:**
- Produces: `ScheduleItem.TrackAttendance`, `ScheduleItemAttendance`, `AttendanceMethod`, `AppDbContext.ScheduleItemAttendances`. These are consumed by Tasks 2–6.

- [ ] **Step 1: Add the enum**

In `CampClotNot/Data/Enums.cs`, after `IncidentReportType`:

```csharp
public enum AttendanceMethod { Self = 0, Admin = 1 }
```

- [ ] **Step 2: Add the flag to `ScheduleItem`**

In `CampClotNot/Data/Entities/ScheduleEvent.cs`, after `public int? MaxCapacity { get; set; }`:

```csharp
    public bool TrackAttendance { get; set; }
```

- [ ] **Step 3: Create the entity**

`CampClotNot/Data/Entities/ScheduleItemAttendance.cs`:

```csharp
namespace CampClotNot.Data.Entities;

public class ScheduleItemAttendance
{
    public Guid ScheduleItemAttendanceId { get; set; }
    public Guid ScheduleItemId { get; set; }
    public ScheduleItem ScheduleItem { get; set; } = null!;
    public Guid? GuestAttendeeId { get; set; }
    public GuestAttendee? GuestAttendee { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CheckedInAt { get; set; }
    public AttendanceMethod Method { get; set; }
    public Guid? CheckedInByUserId { get; set; }
    public User? CheckedInByUser { get; set; }
}
```

- [ ] **Step 4: Register the DbSet and configure the model**

In `AppDbContext.cs`, add under the `// Guest identity` DbSets:

```csharp
    // Attendance
    public DbSet<ScheduleItemAttendance> ScheduleItemAttendances => Set<ScheduleItemAttendance>();
```

At the end of `OnModelCreating`:

```csharp
        // ScheduleItemAttendance: one row per attendee per tracked item. Owned by exactly one of
        // GuestAttendeeId / UserId (check constraint). Two FKs point at Users, so every
        // relationship is configured explicitly to avoid shadow FK properties.
        modelBuilder.Entity<ScheduleItemAttendance>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_ScheduleItemAttendances_OneAttendee",
                "(\"GuestAttendeeId\" IS NULL) <> (\"UserId\" IS NULL)"));
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasIndex(a => new { a.ScheduleItemId, a.GuestAttendeeId })
            .IsUnique();
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasIndex(a => new { a.ScheduleItemId, a.UserId })
            .IsUnique();
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasOne(a => a.ScheduleItem)
            .WithMany()
            .HasForeignKey(a => a.ScheduleItemId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasOne(a => a.GuestAttendee)
            .WithMany()
            .HasForeignKey(a => a.GuestAttendeeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasOne(a => a.CheckedInByUser)
            .WithMany()
            .HasForeignKey(a => a.CheckedInByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 5: Generate the migration** (see Environment above)

```bash
dotnet ef migrations add AddAttendanceTracking
```

Expected result: adds `TrackAttendance boolean NOT NULL DEFAULT FALSE` to `ScheduleItems`, and creates `ScheduleItemAttendances` with the check constraint, two unique indexes, the index on `CheckedInByUserId`, and four FKs (two cascade, two restrict). Read the generated file and confirm there's **no** `ScheduleItemId1`/`UserId1`-style shadow column.

- [ ] **Step 6: Build and commit**

```bash
dotnet build
git add CampClotNot/Data CampClotNot/Migrations
git commit -m "feat: add ScheduleItemAttendance table and TrackAttendance flag"
```

---

### Task 2: Carry `TrackAttendance` through the schedule DTO and both schedule forms

**Files:**
- Modify: `CampClotNot/Services/ScheduleService.cs`
- Modify: `CampClotNot/Pages/Admin/Schedule.razor`
- Modify: `CampClotNot/Pages/Hub/Schedule.razor` (Admin-only edit form)

**Interfaces:**
- Produces: `ScheduleItemDto(..., bool TrackAttendance = false)`.

- [ ] **Step 1: DTO + upsert**

Append `bool TrackAttendance = false` as the last parameter of `ScheduleItemDto`. In `UpsertAsync`, set `TrackAttendance = dto.TrackAttendance` in the insert initializer and `existing.TrackAttendance = dto.TrackAttendance;` in the update branch.

- [ ] **Step 2: `/admin/schedule` form**

Add a `private bool _fTrackAttendance;` field. Set it in `StartEdit` and `StartCopy` from `ev.TrackAttendance`, reset it in `CancelEdit`, and pass it as `TrackAttendance: _fTrackAttendance` to the DTO. Render a checkbox row below "Activity (optional)", matching the `/admin/events` checkbox markup:

```razor
<label style="display:flex;align-items:center;gap:8px;margin-bottom:14px;cursor:pointer;font-size:13px;font-weight:700">
    <input type="checkbox" checked="@_fTrackAttendance" @onchange="() => _fTrackAttendance = !_fTrackAttendance" />
    Track attendance <span style="font-weight:600;color:var(--text-light)">— shows an "I'm here" button</span>
</label>
```

In the admin schedule table, show a small "📋 Attendance" chip next to the title for tracked items, so admins can see which items are flagged.

- [ ] **Step 3: `/hub/schedule` Admin edit form**

This form also calls `UpsertAsync`. Without the field, editing a tracked item here would clear the flag. Add the same `_fTrackAttendance` field and checkbox. Set it in `OpenEditForm`/`OpenCopyForm`, reset it in `OpenAddForm`, and pass it in `SaveEvent`.

- [ ] **Step 4: Build and commit**

```bash
git commit -am "feat: TrackAttendance checkbox on schedule forms"
```

---

### Task 3: `AttendanceService`

**Files:**
- Create: `CampClotNot/Services/AttendanceService.cs`
- Modify: `CampClotNot/Program.cs` (DI: `builder.Services.AddScoped<AttendanceService>();`)

**Interfaces:**
- Consumes: `GuestAccessService.GetGuestAttendeeId`, `GetGuestEventId`, `GetOrCreateGuestAsync`, `RecordVisitAsync`.
- Produces (used by Tasks 4–6):
  - `enum CheckInResult { CheckedIn, AlreadyCheckedIn, NotTracked, OutsideWindow, NotAllowed }`
  - `record RosterEntry(FirstName, LastName, IsGuest, RoleName, GuestAttendeeId, UserId, AttendanceId, CheckedInAt, Method, CheckedInByName)` with `IsCheckedIn`
  - `static bool IsSelfCheckInOpen(ScheduleItem, DateTime campNow)`
  - `Task<HashSet<Guid>> GetMyCheckedInItemIdsAsync(ClaimsPrincipal, Guid eventId)`
  - `Task<CheckInResult> SelfCheckInAsync(ClaimsPrincipal, Guid scheduleItemId)`
  - `Task<List<ScheduleItem>> GetTrackedItemsAsync(Guid eventId)`
  - `Task<Dictionary<Guid,int>> GetCountsAsync(Guid eventId)`
  - `Task<List<RosterEntry>> GetRosterAsync(Guid scheduleItemId)`
  - `Task<CheckInResult> AdminCheckInAsync(Guid scheduleItemId, Guid? guestAttendeeId, Guid? userId, Guid adminUserId)`
  - `Task<CheckInResult> CheckInWalkInAsync(Guid scheduleItemId, string firstName, string lastName, Guid adminUserId)`
  - `Task UndoCheckInAsync(Guid scheduleItemAttendanceId)`
  - `Task<(string FileName, byte[] Content)?> BuildItemCsvAsync(Guid scheduleItemId)`
  - `Task<(string FileName, byte[] Content)?> BuildEventCsvAsync(Guid eventId)`

- [ ] **Step 1: Window + identity helpers**

```csharp
public static readonly TimeSpan OpensBefore     = TimeSpan.FromMinutes(30);
public static readonly TimeSpan DefaultDuration = TimeSpan.FromHours(2);

public static bool IsSelfCheckInOpen(ScheduleItem item, DateTime campNow)
{
    if (!item.TrackAttendance) return false;
    var start = item.CampDay.ToDateTime(item.StartTime);
    var end = item.EndTime is { } e
        ? item.CampDay.ToDateTime(e).AddDays(e < item.StartTime ? 1 : 0)
        : start + DefaultDuration;
    return campNow >= start - OpensBefore && campNow <= end;
}

// Guest cookies carry GuestAttendeeId; staff cookies carry NameIdentifier. A legacy guest cookie
// (pre-#304) has neither and can't check in.
private (Guid? GuestId, Guid? UserId) ResolveAttendee(ClaimsPrincipal user) { ... }
```

- [ ] **Step 2: Insert helper with the race pattern**

`InsertAsync(itemId, guestId, userId, method, byUserId)` checks for an existing row (`AlreadyCheckedIn`), adds with `CheckedInAt = CampTime.Now`, and saves. On `DbUpdateException` it re-queries in a fresh context: if the row now exists, it returns `AlreadyCheckedIn`, otherwise it rethrows. The comment should match the one in `RecordVisitAsync`.

- [ ] **Step 3: Self / Admin / walk-in / undo**

Follow the rules in the spec's "Self check-in rules" section. `AdminCheckInAsync` throws `ArgumentException` unless exactly one of `guestAttendeeId`/`userId` is set, requires `TrackAttendance`, and skips the window. `CheckInWalkInAsync` loads the item (`NotTracked` if not tracked), then calls `GetOrCreateGuestAsync` → `RecordVisitAsync(guest, item.CampEventId)` → `InsertAsync(..., Admin, adminUserId)`. `UndoCheckInAsync` hard-deletes the row if it exists.

- [ ] **Step 4: Roster + counts**

`GetRosterAsync` loads the item, its check-ins (including `GuestAttendee`, `User.UserRole`, `CheckedInByUser`), guests with a `GuestEventVisit` for the item's event, and active users (including `UserRole`). It unions them by id, adding anyone who is checked in but otherwise excluded, and sorts by last name then first name, case-insensitive. `GetCountsAsync` does a `GroupBy(ScheduleItemId).Count()` over check-ins whose item belongs to the event.

- [ ] **Step 5: CSV builders**

A shared `Csv(params string?[] fields)` helper quotes every field (RFC 4180, doubling `"`), and prefixes `'` when a field starts with `=`, `+`, `-`, `@`, tab, or CR, to prevent spreadsheet formula injection. The output is UTF-8 **with BOM** (`Encoding.UTF8.GetPreamble()`) so Excel reads accented names correctly.
- Item CSV: one row per roster entry. Columns: `Last Name, First Name, Type, Checked In, Checked In At, Method, Checked In By`. Filename: `attendance-{yyyy-MM-dd}-{slug(title)}.csv`.
- Event CSV: one row per check-in across tracked items, ordered by day, start time, item title, and last name. Columns: `Date, Start, Item, Last Name, First Name, Type, Checked In At, Method, Checked In By`. Filename: `attendance-{slug(event name)}.csv`.

- [ ] **Step 6: Register, build, commit**

```bash
git commit -am "feat: AttendanceService — check-in window, self/admin/walk-in check-in, roster, CSV"
```

---

### Task 4: "I'm here" on `/hub/schedule`

**Files:**
- Modify: `CampClotNot/Pages/Hub/Schedule.razor`

- [ ] **Step 1: Load state.** Inject `AttendanceService AttendanceSvc`. In `OnInitializedAsync`, after `_eventId` is resolved, set `_myCheckIns = await AttendanceSvc.GetMyCheckedInItemIdsAsync(authState.User, _eventId);`, and keep `authState.User` in a `_user` field for the click handler.

- [ ] **Step 2: One render fragment, three placements.** Add a `RenderFragment CheckInControl(ScheduleItem ev)` in `@code` that renders nothing for untracked items, a green "✓ Checked in" chip when `_myCheckIns.Contains(id)`, an `I'm here` `ccn-btn` when `AttendanceService.IsSelfCheckInOpen(ev, CampTime.Now)`, and otherwise nothing. The button wrapper carries `@onclick:stopPropagation` so tapping it on mobile doesn't open the detail modal. Place `@CheckInControl(ev)` in the mobile row (under the meta line), the desktop table's Event cell, and the mobile detail modal (`md`).

- [ ] **Step 3: Click handler.** `SelfCheckIn(Guid id)` calls `SelfCheckInAsync(_user, id)`. On `CheckedIn`/`AlreadyCheckedIn` it adds the id to `_myCheckIns`. On `OutsideWindow` it stores a per-item message "Check-in is closed for this item". On `NotAllowed` or `NotTracked` it shows "Check-in isn't available". A `_checkingIn` set prevents double taps.

- [ ] **Step 4: Build, commit**

```bash
git commit -am "feat: I'm here self check-in on /hub/schedule"
```

---

### Task 5: `/admin/attendance` roster page + nav

**Files:**
- Create: `CampClotNot/Pages/Admin/Attendance.razor`
- Modify: `CampClotNot/Shared/AppNav.razor`

- [ ] **Step 1: Page skeleton.** `@page "/admin/attendance"`, `@attribute [Authorize(Roles = "Admin")]`. On init it loads the active event (`ActiveEventService`), the admin `UserId` (`NameIdentifier`), the tracked items, and the counts. The default day follows the same three-case rule as `/hub/schedule` (before the event → first day, during → today, after → last day). The default item is the first tracked item on that day whose window is open, otherwise the day's first tracked item.

- [ ] **Step 2: Layout** (same header and panel styling as `/admin/guests`):
  - Header: the title "Attendance" and a `Download all (CSV)` link to `/admin/attendance/event/{eventId}/csv` (with a `download` attribute so Blazor's router doesn't intercept it).
  - Day tabs across the event dates, each showing a count of tracked items.
  - Left panel: the tracked items for the day as clickable rows (time, title, headcount pill). The selected row is highlighted. If there are no tracked items, a message links to `/admin/schedule`.
  - Right panel: the roster for the selected item. It has a header (title, time, "N checked in · G guests · S staff"), a Refresh button, a `Download CSV` link, a search input, a three-way filter (All / Checked in / Not checked in), and a table (Name, Type badge, Status/Action). Check-in and Undo call the service, then reload the roster and counts. Undo asks for a JS `confirm` first.

- [ ] **Step 3: Walk-in FAB + modal.** When an item is selected, show a `ccn-btn ccn-fab-mobile` "+ Walk-in" button. The modal uses the `fadeIn` backdrop and a `ccn-panel` with `popIn` (pitfall #12), and has First/Last Name fields and an inline error for empty names. Saving calls `CheckInWalkInAsync`, then closes the modal, reloads, and shows a snackbar.

- [ ] **Step 4: Nav.** In `AppNav.razor`, add `<a href="/admin/attendance">` with `people.svg`, labeled "Attendance", right after the Guests link in both the desktop Admin dropdown and the mobile admin sheet.

- [ ] **Step 5: Build, commit**

```bash
git commit -am "feat: /admin/attendance roster with walk-in, undo, and headcounts"
```

---

### Task 6: CSV endpoints

**Files:**
- Modify: `CampClotNot/Program.cs`

- [ ] **Step 1:** Add these next to the `/admin/events/{id}/guest-qr` endpoint:

```csharp
    app.MapGet("/admin/attendance/item/{id:guid}/csv", async (Guid id, AttendanceService svc) =>
    {
        var csv = await svc.BuildItemCsvAsync(id);
        return csv is { } f ? Results.File(f.Content, "text/csv; charset=utf-8", f.FileName) : Results.NotFound();
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));

    app.MapGet("/admin/attendance/event/{id:guid}/csv", async (Guid id, AttendanceService svc) =>
    {
        var csv = await svc.BuildEventCsvAsync(id);
        return csv is { } f ? Results.File(f.Content, "text/csv; charset=utf-8", f.FileName) : Results.NotFound();
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));
```

- [ ] **Step 2: Build (no new warnings), commit**

```bash
git commit -am "feat: Admin CSV export endpoints for attendance"
```

---

### Task 7: Verification + PR

- [ ] **Step 1:** Run `dotnet build` and compare the warning count against `dev`. There should be no new warnings.
- [ ] **Step 2:** Re-read the whole diff against the spec and the CLAUDE.md pitfalls.
- [ ] **Step 3:** Push `feature/308-attendance-tracking` and open a PR into `dev` titled "Attendance tracking (#308)". The body links the spec, closes #308, includes the manual checklist below, and lists anything noticed but left out of scope.

**Manual checklist (for the PR description):**
1. `/admin/schedule`: tick "Track attendance" on an item for today whose start time is within the next 30 minutes, then save. The table shows the 📋 chip. Edit it from `/hub/schedule` and confirm the box is still ticked after saving.
2. Guest (private window, `/join`): "I'm here" shows on the tracked item only. Tap it and confirm the "✓ Checked in" chip. Reload and confirm it persists. Tapping on mobile doesn't open the detail modal.
3. Change the item's start time to 3 hours from now. The button disappears for the guest.
4. Staff user: self check in to a tracked, open item.
5. `/admin/attendance`: the headcount shows both people (guest + staff, marked "Self"). Check in another user and confirm it's marked "by <you>". Undo it. Add a walk-in, "Pat Walker", who appears as checked in and also on `/admin/guests`.
6. Download the item CSV and the "all" CSV. Open them in Excel/Sheets and check the columns, the accents in a name like "José", and that a name like `=1+1` shows as text.
7. Delete the walk-in guest on `/admin/guests`. The delete succeeds and the headcount drops.
8. Delete a tracked schedule item that has check-ins. The delete succeeds.
