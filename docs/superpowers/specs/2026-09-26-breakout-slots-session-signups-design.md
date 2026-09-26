# Breakout Slots, Session Sign-ups, Per-Event Staff, and QR Check-in

**Date:** 2026-09-26
**Status:** Implemented (2026-09-26) on `claude/guest-attendance-extend-djwotf`. See "Implementation Notes"
for deviations.
**Driver:** Camp Harvest 2026 (mid-October) runs parallel breakout sessions. Attendance tracking
(#308 / PR #309) assumes everyone could attend every tracked item, and it lists every active `User`
as staff at every event. Tracked in issue #311, a follow-up to #308, scheduled after #310 (event
duplication).

## Context

- **Attendance (#308).** `ScheduleItem.TrackAttendance` opts an item in. `ScheduleItemAttendance`
  holds one row per person per item, and each row belongs to exactly one of `GuestAttendeeId` or
  `UserId` (enforced by a check constraint). `AttendanceService` owns the check-in window, identity
  resolution, the roster, and CSV export. The roster is guests with a `GuestEventVisit` for the
  event, plus **all active `User` rows**, plus anyone already checked in.
- **Staff aren't event-scoped.** `User.UserRoleId` is global, and so is `User.GroupId`, even though
  `Group` belongs to one event. `GroupId` is only set for Volunteers (`AuthService`), and it drives
  one thing: which per-group override (`ScheduleItemGroup`) a Volunteer sees on `/hub/schedule`.
- **`StaffMember`** is the public-facing directory card (`/hub/staff`, `/admin/staff`). It's
  event-scoped and optionally linked to a `User` via `LinkedUserId`. #310's duplication can copy it.
- **`ScheduleItem.MaxCapacity`** exists and is unused.
- **Schedule consumers:** `/hub/schedule` (guests + all roles), `/admin/schedule`, and the Dashboard's
  "today" widget (`ScheduleService.GetForDayAsync`). All three load every item for the event.

## Decisions

The issue body and Tyler's comment settled these:

| Topic | Decision |
|---|---|
| Structure | A **slot** is a `ScheduleItem`. **Options** are ordinary `ScheduleItem`s with `ParentScheduleItemId` → the slot, so they get locations, presenters, and attendance for free. |
| Participant view | Not signed up: one placeholder row for the slot. Signed up: the chosen option replaces the placeholder. Other options never appear. |
| Self sign-up | A per-slot "Allow self sign-up" setting. When it's off, the placeholder says "Your session will be assigned". |
| Capacity | Per option, using `MaxCapacity`. A full option shows **Full** and can't be picked. Admins can go past capacity, and the roster flags anyone over. No waitlist. |
| One per slot | One option per person per slot, enforced in the database. |
| Locked picks | Once someone picks, only an Admin can move or remove them. |
| Unassigned | The slot's admin view lists expected people who aren't in any option, with per-person "assign to…" and multi-select. |
| Registrations | `ScheduleItemRegistration`, the same shape as `ScheduleItemAttendance`. |
| Attendance on options | "I'm here" appears only for people registered to that option. The roster is the expected list ("8 of 12"). The CSV includes no-shows. |
| Bulk sign-up | Add all guests, add all staff, select by role (and by group only if the event has groups), search-and-add, remove, copy the list from another option. Bulk adds skip people already in another option of the slot and report the count. |
| Per-event staff | "All staff" everywhere means staff **at this event**. |

These were settled with Tyler on 2026-09-26, answering the questions the issue left for the spec:

| Question | Decision |
|---|---|
| `StaffMember` or a new table? | A **new `EventStaff` table.** `StaffMember` stays the public directory card. |
| What does the per-event role control? | **Rosters and filters only** (badges, the "by role" bulk picker, CSV "Type"). Login and permissions keep using the global `User.UserRoleId`. Nothing in auth changes. |
| `User.GroupId` | **Moves to `EventStaff.GroupId`**, then `User.GroupId` is dropped. |
| Admin UI for staff | A new **`/admin/event-staff`** page scoped to the active event. |
| Duplication (#310) | A new **"Staff"** copy option, on by default. It copies user and role but not group, because groups aren't copied. |
| Backfill | Every **active** user becomes staff at **every existing event** with their current role. Their group moves to the event the group belongs to. |

Added the same day, after Tyler confirmed that general sessions and meetings (not just breakouts)
need attendance. #308's per-item "Track attendance" toggle already covers them. What was missing is
a QR code at the door:

| Question | Decision |
|---|---|
| QR check-in | **Yes, one static, printable QR per tracked item.** Scanning it checks you in to that item. |
| Time window | **The same window as the button**: 30 minutes before the start until the end. |
| Button vs QR | **A per-item choice**: "Button + QR" (the default) or "QR only". |
| Breakouts vs attendance | Breakouts add **sign-up before the session** (choose one of several parallel sessions, capacity, expected list, no-shows). Attendance on any item works without them. |

## Part 1: Per-Event Staff

### Entity `EventStaff`

`Data/Entities/EventStaff.cs`:

```csharp
public class EventStaff
{
    public Guid EventStaffId { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid UserRoleId { get; set; }          // role at this event; defaults to the user's global role
    public UserRole UserRole { get; set; } = null!;
    public Guid? GroupId { get; set; }            // must belong to EventId (service-enforced)
    public Group? Group { get; set; }
    public DateTime AddedAt { get; set; }         // CampTime.Now
}
```

- A unique index on `(EventId, UserId)`.
- All FKs are explicit (pitfall #13). `Event` → Cascade. `User` → Restrict (users are never
  hard-deleted). `UserRole` → Restrict. `Group` → **SetNull**, so deleting a group on `/admin/groups`
  just unassigns its volunteers.
- `User.GroupId` and the `User.Group` navigation are removed.

### Migration `AddEventStaff` (with backfill)

1. Create `EventStaff`.
2. Backfill with raw SQL inside the migration:
   ```sql
   INSERT INTO "EventStaff" ("EventStaffId","EventId","UserId","UserRoleId","GroupId","AddedAt")
   SELECT gen_random_uuid(), e."EventId", u."UserId", u."UserRoleId",
          CASE WHEN g."EventId" = e."EventId" THEN u."GroupId" END, now()
   FROM "Users" u CROSS JOIN "Events" e
   LEFT JOIN "Groups" g ON g."GroupId" = u."GroupId"
   WHERE u."IsActive";
   ```
   (Column and table names get checked against the model snapshot when the migration is written.)
3. Drop `Users.GroupId` (its FK and index too).

Migrations run on startup, so there's no manual prod step. `SeedService` also adds an `EventStaff`
row for the seeded admin at CCN 2026 (idempotent) so a fresh database isn't empty.

### `EventStaffService`

A scoped service with a primary constructor and the synchronous `CreateDbContext()` (pitfall #14):

```csharp
Task<List<EventStaff>> GetForEventAsync(Guid eventId);                // includes User, UserRole, Group; active users only
Task<bool> IsStaffAtAsync(Guid userId, Guid eventId);
Task<EventStaff?> GetAsync(Guid userId, Guid eventId);
Task AddAsync(Guid eventId, Guid userId, Guid? userRoleId = null);    // role defaults to User.UserRoleId; idempotent
Task<int> AddAllActiveUsersAsync(Guid eventId);                       // returns number added
Task UpdateAsync(Guid eventStaffId, Guid userRoleId, Guid? groupId);  // validates the group belongs to the event
Task RemoveAsync(Guid eventStaffId);
```

`RemoveAsync` doesn't touch attendance or registration rows. A removed person still shows on rosters
they're checked in or registered on, like a deactivated user does today.

### `/admin/event-staff`

- `[Authorize(Roles = "Admin")]`, scoped to the active event, and placed in the nav under **People**
  as "Event Staff", next to Users/Guests/Attendance.
- A header with "Staff at {event name}", **+ Add** (search the active users not yet at the event),
  and **Add all active users**.
- A table with Name, Email, **Role at this event** (a dropdown), **Group** (a dropdown, shown only when
  the event has groups), and Remove (with a JS `confirm`). The same search and role filter as
  `/admin/users`.

### Consumers that change

| Where | Change |
|---|---|
| `AttendanceService.GetRosterAsync` (#308) | Active users → **active users with an `EventStaff` row for the item's event**. The badge shows the event role. |
| `/hub/schedule` Volunteer group filter | `_userGroupId` is read from `EventStaff` for the event being viewed, not from `Users.GroupId`. |
| `/admin/users` | The group picker and Group column are removed (group is set per event on `/admin/event-staff`). The create-user dialog gets an **"Add to {active event} staff"** checkbox, on by default. |
| `AuthService` create/update | It no longer takes `groupId`. |
| `EventSetupService` (#310) | `EventCopyOptions` gains `EventStaff`, and `CopyCounts` gains `EventStaff`. Copying adds `(UserId, UserRoleId)` for active users, with no group. The `/admin/events` checkbox defaults on. |
| `/hub/staff` "Import from Users" | Lists the active event's staff instead of every user. |

**Unchanged:** self check-in on ordinary tracked items stays open to any signed-in staff user, as in
#308. If they aren't on the event's staff list, they appear on the roster through the existing
"anyone checked in" rule. That avoids locking out an Admin who forgot to add themselves.

## Part 2: Breakout Slots

### `ScheduleItem` gains three columns

```csharp
public bool IsBreakoutSlot { get; set; }             // this item is a slot (placeholder)
public bool AllowSelfSignup { get; set; } = true;    // meaningful only when IsBreakoutSlot
public Guid? ParentScheduleItemId { get; set; }      // set on options → the slot
public ScheduleItem? ParentScheduleItem { get; set; }
public List<ScheduleItem> Options { get; set; } = new();
```

- **One level only.** A slot has no parent, and an option can't be a slot. The service enforces this
  on upsert.
- **Options share the slot's `CampDay`.** Their times default to the slot's but can differ, for
  example a 45-minute session inside a one-hour slot.
- **Deleting a slot deletes its options.** That's a self-referencing FK with Cascade, and the
  registrations and check-ins cascade from the options. The delete `confirm` names how many options
  and sign-ups go with it.
- An option can't move to a different slot once it has registrations. Changing the parent is
  blocked in `UpsertAsync`.
- A slot's own `TrackAttendance` is ignored and hidden in the form. Options are tracked individually.
- `ScheduleItemDto` gains `bool IsBreakoutSlot = false, bool AllowSelfSignup = true,
  Guid? ParentScheduleItemId = null` as trailing optional parameters. Both schedule forms carry them
  so an edit can't clear them (the same lesson as `TrackAttendance`).
- "Copy to new day" on a **slot** copies only the slot, not its options. Copying an **option** keeps
  its parent only when the day matches the parent's. Otherwise the copy becomes a standalone item.

### Entity `ScheduleItemRegistration`

```csharp
public class ScheduleItemRegistration
{
    public Guid ScheduleItemRegistrationId { get; set; }
    public Guid ScheduleItemId { get; set; }          // the option
    public ScheduleItem ScheduleItem { get; set; } = null!;
    public Guid SlotScheduleItemId { get; set; }      // the option's parent, denormalized for the unique index
    public ScheduleItem SlotScheduleItem { get; set; } = null!;
    public Guid? GuestAttendeeId { get; set; }
    public GuestAttendee? GuestAttendee { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public DateTime RegisteredAt { get; set; }        // CampTime.Now
    public AttendanceMethod Method { get; set; }      // Self | Admin (reuses the #308 enum)
    public Guid? RegisteredByUserId { get; set; }
    public User? RegisteredByUser { get; set; }
}
```

- A check constraint `("GuestAttendeeId" IS NULL) <> ("UserId" IS NULL)`, the same as attendance.
- **One per slot:** unique `(SlotScheduleItemId, GuestAttendeeId)` and `(SlotScheduleItemId, UserId)`.
  Postgres treats NULLs as distinct, so each index only constrains its own kind of row. Denormalizing
  the slot id is what makes "one option per slot" a real DB constraint. The service always sets it
  from the option's parent, and the option can't be re-parented once it has registrations.
- FKs: option → Cascade, slot → Cascade, guest → Cascade (same as attendance), both users → Restrict.
  Two navigations to `ScheduleItem` and two to `User` all get explicit configuration (pitfall #13).
  After `migrations add`, check that there's no `ScheduleItemId1`/`UserId1`.
- **Moving** someone updates `ScheduleItemId` in place. That keeps the row, the slot id, and the
  uniqueness intact. `Method`/`RegisteredBy` are set to the moving Admin.
- **Moving or removing someone who's already checked in** to the old option also deletes that
  check-in, because a check-in without a registration is orphaned. The Admin `confirm` says so.

### `RegistrationService`

```csharp
public enum SignUpResult { SignedUp, AlreadyInSlot, Full, SelfSignupClosed, NotAllowed, NotAnOption }

// Hub
Task<Dictionary<Guid, Guid>> GetMyRegistrationsAsync(ClaimsPrincipal user, Guid eventId); // slotId → optionId
Task<SignUpResult> SelfSignUpAsync(ClaimsPrincipal user, Guid optionId);
Task<Dictionary<Guid, int>> GetOptionCountsAsync(Guid eventId);                           // optionId → registered

// Admin
Task<List<SlotSummary>> GetSlotsAsync(Guid eventId);           // slots with options, counts, capacity
Task<List<RosterEntry>> GetOptionRosterAsync(Guid optionId);    // registered people (+ check-in state)
Task<List<RosterEntry>> GetUnassignedAsync(Guid slotId);        // expected people in no option of the slot
Task<BulkResult> AdminAssignAsync(Guid optionId, IEnumerable<AttendeeRef> people, Guid adminUserId); // skips anyone already in the slot
Task MoveAsync(Guid registrationId, Guid toOptionId, Guid adminUserId);
Task RemoveAsync(Guid registrationId);
Task<List<AttendeeRef>> ExpectedPeopleAsync(Guid eventId, BulkFilter filter); // AllGuests | AllStaff | Roles[] | Groups[]
```

`AttendeeRef` is `(Guid? GuestAttendeeId, Guid? UserId)`. `BulkResult` is `(int Added, int Skipped)`.
"Copy list from another option" is `GetOptionRosterAsync(other)` → `AdminAssignAsync`. People already
in another option of the same slot are skipped (so copying between options of the **same** slot adds
nobody, and the UI only offers other slots' options).

**Expected people** means guests with a `GuestEventVisit` for the event, plus active users with an
`EventStaff` row for the event.

#### Self sign-up rules (`SelfSignUpAsync`)

1. Resolve the identity the same way `AttendanceService.ResolveAttendee` does (it moves to a shared
   helper). With no identity, the result is `NotAllowed`.
2. The item must be an option, otherwise `NotAnOption`. Its slot needs `AllowSelfSignup`, otherwise
   `SelfSignupClosed`.
3. A guest's event must match the item's event. A user needs an `EventStaff` row at the item's event
   (this is where per-event staff matters). Otherwise the result is `NotAllowed`.
4. The capacity check and the insert happen in **one transaction that locks the option row**
   (`SELECT … FOR UPDATE` via `FromSql`). That way two people can't both take the last spot. If
   `MaxCapacity` is set and the count is at or above it, the result is `Full`.
5. The insert uses `Method = Self`. A unique-index violation means `AlreadyInSlot`, using the same
   catch-and-re-query pattern as `InsertAsync` in #308.

There's no sign-up deadline. People can sign up any time until an Admin turns self sign-up off.
(See the open questions below.)

### Attendance on options (changes to `AttendanceService`)

- `SelfCheckInAsync`: if the item has a parent, the caller must have a registration for **this**
  option. Otherwise the result is `NotAllowed`. This check runs before the window check.
- `GetRosterAsync`: for an option, the roster is **registrations plus anyone checked in** (instead of
  everyone at the event). `RosterEntry` gains `IsRegistered`, so the page can show "8 of 12" and flag
  check-ins that have no registration.
- Admin check-in on an option roster works as it does today. **Walk-in** on an option creates the
  guest, records the visit, **registers them to the option** (as an Admin, ignoring capacity), then
  checks them in.
- Item CSV: for an option, one row per expected person (no-shows included), with an added
  `Registered` column. The event CSV is unchanged (one row per check-in).
- `/admin/attendance` summary rows for options read "Workshop A: 8 / 12" and are grouped under the
  slot title.

## Part 3: UI

### `/admin/schedule` form

- A new **"Breakout"** select with three choices: *None*, *This is a breakout slot*, and
  *Option in slot…* (which lists the slots on the chosen day).
- **Slot:** shows "Allow self sign-up" (checked by default) and hides "Track attendance".
- **Option:** shows "Capacity" (`MaxCapacity`, blank means unlimited). The day is locked to the
  slot's, and the times are prefilled from it.
- The table shows options indented under their slot with a small "🔀 Breakout" chip on the slot and
  "n / cap" on each option.
- The same fields are added to the Admin-only edit form on `/hub/schedule`.

### `/hub/schedule` (and the Dashboard "today" widget)

A shared visibility filter keeps the two views consistent:
`ScheduleVisibility.Apply(items, isAdmin, myRegistrations)`.

- **Admins:** see the slot with its options nested underneath, each with "n / cap", plus the usual
  edit controls.
- **Everyone else (guests, Staff, MedicalStaff, Volunteer):** see participant rows.
  - **Registered:** the option is shown in the slot's place, with its title, time, location,
    presenter, and "I'm here" when tracked and open.
  - **Not registered, self sign-up on:** "{Slot title} — choose yours". Tapping it opens a modal
    (`fadeIn`/`popIn`, pitfall #12) that lists the options with title, time, location, presenter,
    and "N spots left", or **Full** (disabled), or no count when capacity is unlimited. Picking one
    asks "You can't change this later — ask an Admin if you need to switch." Confirming calls
    `SelfSignUpAsync`. On `Full` (someone got there first), the modal refreshes the counts and
    shows a message.
  - **Not registered, self sign-up off:** "{Slot title} — Your session will be assigned".
  - The other options never render.
- Server-side, the filter runs on data the page already loaded. Sign-up and check-in re-check
  everything, so a hidden option id can't be used directly.
- The Dashboard "today" list uses the same filter, which means one extra query for my registrations.

### `/admin/breakouts` (new, Admin-only)

This is the people side of breakouts. Slot and option CRUD stays in `/admin/schedule`.

- It's scoped to the active event and linked in the nav under **Schedule** as "Breakouts".
- It has day tabs, and each day lists its slots.
- **Slot view:** option cards with title, time, location, and a headcount pill "8 / 12". Over
  capacity turns the pill red. Below them is **Unassigned (N)**, a list with checkboxes, a search
  box, "select all", and an **Assign to [option ▾]** button. Each row also has a quick "Assign to…"
  dropdown. A toggle for the slot's "Allow self sign-up" sits in the header.
- **Option view** (click a card): the registration roster with Name, Type badge (Guest or event
  role), Registered (self/by whom, time), and **Move to…** / **Remove**. Anyone past capacity is
  flagged "over capacity" in registration order. A **Bulk add** panel has:
  - Add all guests
  - Add all staff (at this event)
  - Roles checklist (Staff / Volunteer / Medical Staff / Admin), then "Add selected roles"
  - Groups checklist (shown only when the event has groups)
  - A search-and-add box
  - "Copy list from…" (options in **other** slots)

  Each bulk action shows a snackbar like "Added 14 · skipped 3 already in this slot".
- Attendance for options still lives on `/admin/attendance`. From the option view, a **"View
  attendance →"** link goes there when the option is tracked.

## Part 4: QR Check-in

### Data

`ScheduleItem` gains two columns:

```csharp
public string? CheckInCode { get; set; }              // random token in the QR URL; unique; created on first "Show QR"
public SelfCheckInMode SelfCheckInMode { get; set; }  // ButtonAndQr = 0 (default) | QrOnly = 1
```

`AttendanceMethod` gains `Qr = 2`, so the roster reads "9:42 AM · QR".

- **The QR carries a random code, not the `ScheduleItemId`.** Item ids reach the browser in various
  places, and a "QR only" item must not be checkable by anyone who guesses or sees its id. The code
  is 10 characters from an unambiguous alphabet (no 0/O/1/I), with a unique index (NULLs allowed).
- An Admin can **regenerate** the code, for example if a photo of the QR is going around. The
  printed copy then stops working.
- "Copy to new day" doesn't copy the code. The copy gets its own code the first time its QR is shown.

### Scan flow: `/checkin/{code}`

It's a Blazor page with `[AllowAnonymous]` and `LoginLayout` (a centered card, no nav).

1. **Unknown code** shows "This check-in code isn't valid."
2. **Not signed in** shows the item title and time and two buttons: **"I'm a guest"** →
   `/join?returnUrl=/checkin/{code}` and **"Staff sign in"** → `/login?returnUrl=/checkin/{code}`.
   The existing forms post a hidden `returnUrl`, and the endpoints redirect there after sign-in
   instead of `/hub/schedule` / `/dashboard`. Only local paths are accepted: `/` but not `//` or
   `/\`. A guest who hasn't joined still has to type the event code, which keeps #298's rule
   that the join QR isn't a substitute for the code.
3. **Signed in** calls `AttendanceService.QrCheckInAsync(user, code)`, which uses the same rules as
   `SelfCheckInAsync` (tracked, guest event match, breakout registration, window) and records
   `Method = Qr`. The page shows a big green "✓ You're checked in to {title}" (or "✓ Already checked
   in at 9:42 AM"), with a link to the schedule. Failures show a plain message:
   - Outside the window: "Check-in for {title} opens at 1:30 PM" or "…closed at 3:00 PM".
   - A guest signed in to a different event: "This session is part of {event}", with a "Join
     {event}" link (`/join?returnUrl=…`).
   - Not registered for a breakout option: "You're not signed up for this session."
   - Not tracked any more: "Check-in isn't open for this item."

The check-in runs in `OnInitializedAsync`, which Blazor Server calls twice (prerender, then the
interactive circuit). The insert is idempotent, so the second call returns `AlreadyCheckedIn` and
the page shows success either way.

### "QR only" items on `/hub/schedule`

The "I'm here" button isn't shown. Inside the window, the row shows a small hint instead: "📷 Scan
the QR code at the session to check in". `SelfCheckInAsync` (the button path) returns
`NotAllowed` for QR-only items, so the button path can't be used directly.

### Admin

- **`/admin/schedule` form:** under "Track attendance", a **Check-in** select with "Button + QR"
  and "QR only". The same field goes on the Admin edit form on `/hub/schedule`.
- **`/admin/attendance` roster header:** a **"📷 QR code"** button opens a modal
  (`fadeIn`/`popIn`) with the QR image, the check-in URL, **Print** (opens the print page),
  and **Regenerate** (with a `confirm`).
- **Print page `/admin/attendance/item/{id}/qr`:** Admin-only and uses `PrintLayout`. It shows the
  event name, item title, day and time, a large QR, "Scan to check in", and the short URL
  underneath for anyone whose camera won't scan. It's sized for one letter-size page.
- **The QR PNG is `GET /admin/attendance/item/{id}/qr.png`,** Admin-only. It creates the code if
  missing and reuses `GuestAccessService.GenerateJoinQrPng(url)` with the public base URL
  (`PublicBaseUrl.From`).

## Explicitly Out of Scope

- Waitlists and self-service switching between options (both are in the issue's "later" list).
- Linking a `GuestAttendee` to a `User` for one history across events.
- Per-event **permissions**. The event role is a label for rosters and filters only.
- Sign-up deadlines (see the open questions below), notifications about assignments, and
  SignalR-live counts (the admin pages have Refresh, as `/admin/attendance` does).
- Nested slots, and options on a different day from their slot.
- Rotating or expiring QR codes. The QR is static, and the time window plus Regenerate are the protection.
- Sign-up lists on ordinary (non-breakout) items. Attendance on those needs no sign-up.

## Defaults Taken

None of these were contested in review. Each is a small change if it needs to go the other way.

1. **Do Staff-role users see every option on `/hub/schedule`?** The issue says only Admins do, so
   Staff get the participant view. That differs from group-specific items, which Staff see in full
   today.
2. **Sign-up deadline.** I left it out. Self sign-up stays open until an Admin turns it off on the
   slot. An automatic close (for example, at slot start) would be a one-line rule.
3. **Staff self check-in on ordinary (non-breakout) tracked items** doesn't require being on the
   event's staff list. Requiring it would be stricter but could lock out someone who wasn't added.
4. **Backfill covers active users only.** Deactivated users are skipped. If one is reactivated
   later, an Admin re-adds them on `/admin/event-staff`.

## Testing

There's no automated test suite (project convention). Verification is `dotnet build` with no new
warnings, plus a walkthrough on local Postgres 16 with headless Chromium:

- **Migration:** existing users land in `EventStaff` for every event. A Volunteer's group lands only
  on the event the group belongs to. `Users.GroupId` is gone. There are no shadow columns.
- `/admin/event-staff`: add, add all, change role and group, remove. `/admin/users` create with
  "Add to event staff" works. Duplicating an event with "Staff" checked copies people but not groups.
- The Volunteer group override still shows on `/hub/schedule`.
- The #308 roster lists only this event's staff.
- **Breakouts:** create a slot with 2 options (capacity 1 and unlimited). As guest A, see the
  placeholder, pick the capacity-1 option, then reload and see only that option. As guest B, see it
  marked Full. Two tabs racing for the last spot produce one row. Guest A's "I'm here" works on their
  option, and posting a check-in for the other option returns `NotAllowed`. Turn self sign-up off and
  see "Your session will be assigned".
- `/admin/breakouts`: the Unassigned count is right. Multi-select assign works, and so does assigning
  past capacity (it gets flagged). Moving someone who's checked in removes the check-in. Bulk add by
  role and "copy list from" report the skipped count. The option CSV includes no-shows.
- Deleting a slot removes its options, registrations, and check-ins.
- **QR:** open the QR modal and print page. Scan while signed out as a guest, join with the code and
  land checked in. Scan as staff while signed out, sign in and land checked in. Scan again and see
  "Already checked in". Scan outside the window and see the opens/closed message. A guest from
  another event gets the "part of {event}" message. On a QR-only item, the hub shows the hint and no
  button. After Regenerate, the old URL shows "isn't valid". The roster shows "· QR".

## Implementation Notes (2026-09-26)

- **Migrations:** `AddEventStaff` creates `EventStaff`, runs the backfill SQL, and then drops `Users.GroupId`.
  Its `Down` copies groups back onto `Users` before dropping the table. `AddBreakoutsAndQrCheckIn` adds
  the five `ScheduleItems` columns (`AllowSelfSignup` defaults to `true` in the database too) and
  `ScheduleItemRegistrations`. Both run automatically on startup. Verified against Postgres 16: the
  backfill skips inactive users, puts a Volunteer's group only on its own event, and Down/Up round-trips.
- **`CheckInResult`** gained `WrongEvent` (a guest from another event, previously `NotAllowed`),
  `NotRegistered` (a breakout option they didn't sign up for), and `QrOnly` (the button on a QR-only item).
  `QrCheckInAsync` returns a `QrCheckInOutcome` with the item, event name, and check-in time for the
  result page.
- **Copying an option** keeps it in the same slot, locked to the slot's day, since the common case is
  "Workshop A → Workshop B". To copy it to another day as a regular item, pick "None" in Breakout.
  This replaces the spec's "keep the parent only if the day matches" rule.
- **Moving a slot to another day** moves its options with it.
- **Walk-ins on an option** are signed up to it. If they were already in another option of the same
  slot, they're moved, because the Admin at the door is placing them in this room.
- **`/hub/schedule`** only shows "I'm here" on an option to the person signed up for it. Admins see every
  option, but tapping one they aren't in would only fail.
- **"I'm here" moved into the item detail modal** (Tyler, 2026-09-26) to keep the day list at-a-glance.
  The button, the "✓ Checked in" chip, and the QR-only hint no longer appear on the schedule rows.
  Tapping a row opens the modal on mobile as before. Desktop table rows now open it too, and the
  Edit/Copy/Del, Groups, and location-image controls stop the click from reaching the row.
- **Check-in status on the day list** (Tyler, 2026-09-26): tracked items that apply to the viewer
  show a green "✓ Checked in" badge, or a red "Not checked in" badge once the window has opened.
  Nothing shows before the window opens. Breakout options only show it to the person signed up.
- **Location photo in the detail modal** stays a 200px-tall cropped banner. Tapping it opens the full
  photo in the lightbox, because tall photos lose their top and bottom in the banner. The desktop
  table thumbnail now has a fixed width, so portrait photos crop the same way instead of shrinking
  to a sliver.
- **`/admin/event-staff` became `/admin/team`** (Tyler, 2026-09-26, after a workflow review) so that
  staffing an event takes one page. "+ Add person" either adds an existing account or invites a new
  person (creating the account, adding them to the team, and emailing an invite or showing a copyable
  link). Each row has role, group, and an **In Hub directory** toggle that creates or removes their
  linked `StaffMember` card. An empty team offers "Copy the team from…", "Add everyone who has an
  account", or adding people one at a time. Nav: People → Team, Groups, Guests, Attendance,
  Staff Directory (was "Staff"), Accounts (was "Users"). The old URL still routes to Team.
- **Groups are scoped to the active event.** `GroupService.GetAllAsync` returned every event's groups
  (leaderboard, transactions, board, admin), and `/admin/groups` created new groups under the first
  existing group's event. The CCN group seed is insert-only now (pitfall #19).
- **`returnUrl` is also carried through `/change-password`,** so a staff member whose first sign-in
  is from a QR scan (temporary password) still lands back on the check-in.
- **Page files:** `Pages/Admin/EventStaffAdmin.razor` (to avoid clashing with the `EventStaff`
  entity), `Pages/Admin/Breakouts.razor`, `Pages/CheckIn.razor`, and `Pages/Admin/CheckInQrPrint.razor`.
  Nav: "Event Staff" is under People, and "🔀 Breakouts" is under Schedule.
- **Verified with headless Chromium** against a fresh database:
  - Admin: create a user with "Add to event staff". Create a slot and an option from the form, with the
    day locked and times prefilled.
  - Guests: the placeholder, then picker, confirm, and pick (the other options stay hidden). The
    capacity-1 option shows Full to the next guest. "I'm here" on an option doesn't open the detail
    modal. QR-only items show the hint. Assigned-only slots show "will be assigned" until an Admin
    assigns, then the option.
  - QR: signed out → join → checked in, and signed out → staff login → change password → checked in.
    Also rescan, not signed up, outside the window ("opens at 7:30 PM"), wrong event, bad code, the
    roster showing "· QR", the print page, and regenerate invalidating the old code.
  - Breakouts page: multi-select assign past capacity (flagged), move a checked-in person (the
    check-in is removed), bulk add guests, staff, and roles with skip counts, and the option CSV with
    `Registered` and no-shows.
  - Cascades: deleting a guest removes their sign-ups. Deleting a slot removes its options, sign-ups,
    and check-ins.
  - The hub Admin edit form preserves the breakout fields. Duplicating an event copies event staff
    (role, no group).

