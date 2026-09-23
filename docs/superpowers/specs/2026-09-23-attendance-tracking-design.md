# Attendance Tracking

**Date:** 2026-09-23
**Status:** Approved, pending implementation plan
**Driver:** Camp Harvest 2026 (mid-October) and future chapter events need a record of who actually
showed up to which session. This is sub-project 3 of a 4-part roadmap (guest identity → guest push
notifications → attendance tracking → admin event configurability). Tracked in issue #308.

## Context

Two things already exist that look like attendance but aren't:

- `GuestEventVisit` (sub-project 1, #304) records `FirstJoinedAt`/`LastSeenAt` per guest per event.
  It means "this guest joined the event in the app," which happens once when they scan the QR code.
  It says nothing about which sessions they attended.
- Staff are `User` rows. They aren't event-scoped (`StaffMember` is the event-scoped directory card,
  optionally linked to a `User` via `LinkedUserId`). There is no per-event or per-session record for
  staff.

Guests and staff sign in under the same cookie scheme. A guest principal carries a
`GuestClaimTypes.GuestAttendeeId` claim plus a `GuestClaimTypes.EventId` claim. A staff principal
carries `ClaimTypes.NameIdentifier` (the `UserId`) plus a role claim. `GuestAccessService` already
exposes `GetGuestAttendeeId(ClaimsPrincipal)` and `GetGuestEventId(ClaimsPrincipal)`.

## Roadmap (for traceability)

1. Named guest identity — done (#304)
2. Guest push notifications — done (#306 / PR #307)
3. **Attendance tracking** (this doc)
4. Admin event configurability (theme UI, event duplication)

## Design Decisions (settled with Tyler before writing)

| Question (from #308) | Decision |
|---|---|
| Passive or active? | **Active.** Attendees self check in with an "I'm here" button, and an Admin can check anyone in from a roster. Joining the event (`GuestEventVisit`) does not count as attendance. |
| Per event, per day, or per session? | **Per schedule item.** Admins mark items as tracked with a new opt-in `TrackAttendance` flag. Meals, travel, and free time stay untracked unless an admin flags them. |
| Self check-in window | From **30 minutes before `StartTime`** until **`EndTime`**, or `StartTime + 2h` when there's no `EndTime`, on the item's `CampDay`, in camp time (`CampTime.Now`). Admins can check people in at any time. |
| Who self checks in? | **Anyone signed in**: guests and staff users (Admin, Staff, MedicalStaff, Volunteer). |
| Who checks others in? | **Admin only.** |
| Who is on the roster? | Guests who joined the event (`GuestEventVisit`), all active staff `User` rows, and anyone already checked in to the item. |
| Walk-ins | An Admin can add a guest by first/last name from the roster. This reuses `GetOrCreateGuestAsync` (same normalization and matching) and `RecordVisitAsync`, then checks them in. No phone needed. |
| Undo | An Admin can undo any check-in (hard delete of the row). Guests and staff can't undo their own check-ins; a mis-tap goes to an Admin. |
| Roster output | Per-item headcount (total, guests, staff), a per-day summary of counts for every tracked item, and CSV export per item and for the whole event. |
| Roster location | New page `/admin/attendance`, Admin-only. `/admin/guests` is unchanged. |

## Data Model

### `ScheduleItem` gains one column

```csharp
public bool TrackAttendance { get; set; }   // default false
```

It's added to `ScheduleItemDto` as a trailing optional parameter (`bool TrackAttendance = false`) and
written in both branches of `ScheduleService.UpsertAsync`. Both schedule forms that build the DTO
(`/admin/schedule` and the Admin-only edit form on `/hub/schedule`) get a "Track attendance" checkbox,
so editing an item from either page can't silently clear the flag.

### New entity `ScheduleItemAttendance`

`Data/Entities/ScheduleItemAttendance.cs`:

```csharp
public class ScheduleItemAttendance
{
    public Guid ScheduleItemAttendanceId { get; set; }
    public Guid ScheduleItemId { get; set; }
    public ScheduleItem ScheduleItem { get; set; } = null!;
    public Guid? GuestAttendeeId { get; set; }
    public GuestAttendee? GuestAttendee { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CheckedInAt { get; set; }          // CampTime.Now (camp wall clock)
    public AttendanceMethod Method { get; set; }       // Self | Admin
    public Guid? CheckedInByUserId { get; set; }       // Admin who checked them in; null for Self
    public User? CheckedInByUser { get; set; }
}

public enum AttendanceMethod { Self = 0, Admin = 1 }   // Data/Enums.cs, stored as int
```

- **Exactly one attendee column is set.** Each row belongs to either a guest (`GuestAttendeeId`) or a
  staff user (`UserId`). This is enforced with a DB check constraint
  `("GuestAttendeeId" IS NULL) <> ("UserId" IS NULL)`. The push-subscription table skipped this kind of
  constraint because the auth model already prevented the bad state. Here an Admin code path writes
  rows on someone else's behalf, so the constraint is worth having.
- **One check-in per person per item.** There are two unique indexes: `(ScheduleItemId, GuestAttendeeId)`
  and `(ScheduleItemId, UserId)`. Postgres treats NULLs as distinct under a unique index, so each index
  only constrains its own kind of row. Inserts are idempotent and follow the existing
  `RecordVisitAsync` race pattern: catch `DbUpdateException`, re-query in a fresh context, and treat an
  existing row as success.
- **Explicit FKs for all four relationships** in `OnModelCreating` (CLAUDE.md pitfall #13). `User` and
  `CheckedInByUser` point at the same table, so EF can't infer them by convention anyway.
- **Delete behaviour:**
  - `ScheduleItem` → attendance: **Cascade.** Deleting a schedule item deletes its check-ins.
  - `GuestAttendee` → attendance: **Cascade.** Deleting a guest on `/admin/guests` removes their
    attendance history too. #304's spec flagged that this sub-project would need to decide, and a
    deleted person's check-ins have no one to attribute them to.
  - `User` → attendance (both FKs): **Restrict** (EF's default for these). Users are deactivated, never
    hard-deleted, anywhere in the app today.
- **Why `CampTime.Now` and not `DateTime.UtcNow`:** the check-in window compares against `CampDay` and
  `StartTime`, which are camp wall-clock values, and the roster and CSV show times to Vicki in camp time.
  `ScheduleItem.UpdatedAt` already uses `CampTime.Now` for the same reason.

The roadmap sketch in CLAUDE.md said `EventAttendance`. Since the unit is a schedule item, the table is
named for what it holds. The event is always reachable via `ScheduleItem.CampEventId`.

## `AttendanceService`

New scoped service `Services/AttendanceService.cs`, using the primary-constructor DI style and the
synchronous `factory.CreateDbContext()` (CLAUDE.md pitfall #14).

```csharp
public enum CheckInResult { CheckedIn, AlreadyCheckedIn, NotTracked, OutsideWindow, NotAllowed }

// Pure helper, used by both the Hub button and the server-side check.
public static bool IsSelfCheckInOpen(ScheduleItem item, DateTime campNow);

// Hub: which of these items has the current principal already checked in to?
Task<HashSet<Guid>> GetMyCheckedInItemIdsAsync(ClaimsPrincipal user, Guid eventId);

// Hub: "I'm here". Resolves guest vs staff from the principal and re-validates everything server-side.
Task<CheckInResult> SelfCheckInAsync(ClaimsPrincipal user, Guid scheduleItemId);

// Admin roster
Task<List<ScheduleItem>> GetTrackedItemsAsync(Guid eventId);                  // all tracked items, day + time order
Task<Dictionary<Guid, int>> GetCountsAsync(Guid eventId);                     // ScheduleItemId → headcount
Task<List<RosterEntry>> GetRosterAsync(Guid scheduleItemId);                  // guests + staff, with check-in state
Task<CheckInResult> AdminCheckInAsync(Guid scheduleItemId, Guid? guestAttendeeId, Guid? userId, Guid adminUserId);
Task UndoCheckInAsync(Guid scheduleItemAttendanceId);
Task<CheckInResult> CheckInWalkInAsync(Guid scheduleItemId, string firstName, string lastName, Guid adminUserId);

// Export
Task<string> BuildItemCsvAsync(Guid scheduleItemId);
Task<string> BuildEventCsvAsync(Guid eventId);
```

`RosterEntry` is a record with `Name`, `Kind` (Guest/Staff), `RoleName?`, `GuestAttendeeId?`, `UserId?`,
and the check-in (`ScheduleItemAttendanceId?`, `CheckedInAt?`, `Method?`, `CheckedInByName?`).

### Self check-in rules (`SelfCheckInAsync`)

1. Resolve identity. If `GetGuestAttendeeId(user)` is set, it's a guest. Otherwise use
   `NameIdentifier` → `UserId`. If neither is present (a legacy guest cookie from before #304), return
   `NotAllowed`.
2. Load the item. If it's missing or `TrackAttendance == false`, return `NotTracked`.
3. Guests only: `item.CampEventId` must equal `GetGuestEventId(user)`. This stops a guest from checking
   in to another event's items. Staff aren't event-scoped, so there's no equivalent check for them.
4. `IsSelfCheckInOpen(item, CampTime.Now)` must be true, otherwise `OutsideWindow`.
5. Insert with `Method = Self`, `CheckedInByUserId = null`. A duplicate is `AlreadyCheckedIn`.

`AdminCheckInAsync` skips the window check and the event match on purpose: an Admin can back-fill
yesterday's session from a paper sign-in sheet. It still requires `TrackAttendance`.

### Roster composition (`GetRosterAsync`)

The roster is the union, de-duplicated by attendee, of:
- guests with a `GuestEventVisit` for the item's event,
- all `User` rows with `IsActive == true` (with `UserRole.Name` for the badge),
- anyone with a check-in row on the item, even if they'd otherwise be excluded (for example, a
  since-deactivated user).

Rows are sorted by last name, then first name. The service returns everything and the page filters
it. At chapter scale that's a few hundred rows at most.

## Hub: "I'm here" (`/hub/schedule`)

For each item with `TrackAttendance == true`, in the mobile row, the mobile detail modal, and the
desktop table's Event cell:

- **Already checked in:** a green "✓ Checked in" chip. It isn't clickable.
- **Window open, not checked in:** an "I'm here" `ccn-btn` button with `@onclick:stopPropagation` so it
  doesn't also open the mobile detail modal. On click it calls `SelfCheckInAsync` and turns into the
  chip on success. Otherwise it shows a short inline message ("Check-in is closed for this item").
- **Window not open:** nothing shown. This keeps untracked-looking days uncluttered.

The page loads `GetMyCheckedInItemIdsAsync` once in `OnInitializedAsync` alongside the existing
parallel loads. The window is evaluated at render time. If someone leaves the page open from before
the window starts, the button appears on their next navigation or refresh, and the server-side check
is what actually enforces the window.

## Admin Roster (`/admin/attendance`)

`@attribute [Authorize(Roles = "Admin")]`. Scoped to the active event (`ActiveEventService`), matching
`/admin/schedule`. Neo-brutalist panel styling, same as the other admin pages.

- **Day tabs** for the event's dates. Under the selected day, a **summary panel** lists that day's
  tracked items (time, title, headcount pill). Clicking one selects it. If no items are tracked, the
  panel says so and links to `/admin/schedule`.
- **Roster panel** for the selected item:
  - A header with the title, time, headcount (total, guests, staff), a **Refresh** button (self
    check-ins from phones don't push to this page live), and a **Download CSV** link.
  - A search box plus a filter: All / Checked in / Not checked in.
  - A table with Name, a Type badge (Guest, or the staff role name), and Status. Status is either
    "Checked in 9:42 AM · Self" (or "· by Tyler") with an **Undo** button, or a **Check In** button.
    Undo asks for a JS `confirm` first, same as the existing deletes.
- **Walk-in FAB:** a `ccn-fab-mobile` "+ Walk-in" button opens a modal using the `fadeIn`/`popIn`
  pattern (CLAUDE.md pitfalls #11 and #12) with First Name and Last Name fields. Saving calls
  `CheckInWalkInAsync`, which runs `GetOrCreateGuestAsync` → `RecordVisitAsync` → admin check-in. The
  walk-in then appears on `/admin/guests` like any other guest who joined, and on the rosters of every
  other tracked item for the event.
- A **"Download all (CSV)"** link in the page header exports the whole event.

The nav gets an "Attendance" link in the Admin dropdown and the mobile admin sheet, under the
**People** section next to Guests.

## CSV Export

These are two Admin-only minimal-API endpoints in `Program.cs`, because a file download needs a real
HTTP response, the same reason `/admin/events/{id}/guest-qr` is an endpoint:

- `GET /admin/attendance/item/{scheduleItemId}.csv`: one row per **roster entry** (checked in or not),
  so the export doubles as a sign-in sheet.
  Columns: `Last Name, First Name, Type, Checked In, Checked In At, Method, Checked In By`.
- `GET /admin/attendance/event/{eventId}.csv`: one row per **check-in** across all tracked items.
  Columns: `Date, Start, Item, Last Name, First Name, Type, Checked In At, Method, Checked In By`.

Both are `RequireAuthorization(policy => policy.RequireRole("Admin"))` and return
`text/csv; charset=utf-8` with a UTF-8 BOM so Excel on Windows opens names with accents correctly.
Fields are quoted per RFC 4180. Any field that begins with `=`, `+`, `-`, or `@` gets a leading `'` so a
guest-typed name can't turn into a spreadsheet formula.

## Explicitly Out of Scope

- Session signups or capacity limits (`MaxCapacity` already exists on `ScheduleItem` but isn't used
  here). That's the separate v2.2.0 "session signups" item.
- Check-out times or duration tracking. "Undo" deletes a mistaken check-in and doesn't record
  departure.
- Live-updating the roster over SignalR. There's a Refresh button instead.
- QR-per-session check-in, geofencing, or any proof-of-presence beyond the time window.
- PDF export and the end-of-event reporting bundle, which is a separate v2.2.0 item.
- Letting non-Admin roles (Staff, Volunteer) check others in.
- Self-undo for attendees.
- Event-scoping staff users. The roster lists all active users, since `User` has no event
  association today.

## Testing

No automated test suite exists for this project (established project-wide convention).
Verification is `dotnet build` with no new warnings, plus a manual walkthrough:

- Flag an item as tracked on `/admin/schedule`, then edit it from `/hub/schedule` and confirm the flag
  survives.
- As a guest, confirm "I'm here" appears only inside the window. Tap it, confirm the chip, reload, and
  confirm it persists. Tapping twice (two tabs) creates one row.
- As a Staff user, self check in to the same item. Both appear on the roster.
- Confirm a guest from a different event can't check in to this event's items (server returns
  `NotAllowed`).
- As Admin on `/admin/attendance`: check someone in outside the window, undo it, add a walk-in (who
  then appears on `/admin/guests`), download both CSVs, and open them in Excel or Sheets.
- Delete a guest on `/admin/guests` who has check-ins. The delete succeeds and their rows are gone.
