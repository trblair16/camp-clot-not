# Breakout Slots, Session Sign-ups, Per-Event Staff, and QR Check-in: Implementation Plan

**Goal:** Implement `docs/superpowers/specs/2026-09-26-breakout-slots-session-signups-design.md`
(issue #311 plus QR check-in).

**Branch:** `claude/guest-attendance-extend-djwotf` (off latest `dev`). The PR targets `dev`.

## Global Constraints

- Explicit FK configuration for every relationship (pitfall #13). After each `migrations add`, read the
  migration and check for shadow columns (`UserId1`, `ScheduleItemId1`, …).
- Synchronous `factory.CreateDbContext()` (pitfall #14) and the primary-constructor DI style.
- Modals use `fadeIn`/`popIn` (pitfall #12), FABs use `ccn-fab-mobile` (pitfall #11), and `@onclick`
  lambdas containing `$"..."` use single-quoted attributes (pitfall #15).
- Idempotent inserts use the catch-`DbUpdateException`-and-re-query pattern from `AttendanceService.InsertAsync`.
- Schedule reads are cached for 60s (`sched.ev.*`, `sched.day.*`). Any write to `ScheduleItems`
  evicts both keys.
- Verify with `dotnet build` (baseline 3 warnings, no new ones), plus local Postgres and headless
  Chromium for the walkthroughs in the spec's Testing section.

## Tasks

### 1. Per-event staff: data + migration
- `EventStaff` entity, DbSet, and config (unique `(EventId, UserId)`; Event cascade, User/UserRole
  restrict, Group set-null).
- Remove `User.GroupId`/`User.Group`.
- Migration `AddEventStaff`: create the table, run the backfill SQL from the spec, then drop `Users.GroupId`.
- `SeedAdminUserAsync` also adds the new admin as staff at CCN 2026.

### 2. Per-event staff: service + consumers
- `EventStaffService` (spec Part 1) and its DI registration.
- `AuthService.CreateUserAsync`/`UpdateUserAsync` drop `groupId`.
- `/admin/users`: remove the group picker and column, and add "Add to {active event} staff" to create.
- `/hub/schedule`: the Volunteer group comes from `EventStaff` for the viewed event.
- `AttendanceService.GetRosterAsync`: staff = active users with `EventStaff` at the event. The badge shows the event role.
- `EventSetupService`: `EventCopyOptions.EventStaff`, `CopyCounts.EventStaff`, and the `/admin/events` checkbox (default on).
- `/hub/staff` "Import from Users" lists the event's staff.

### 3. `/admin/event-staff` page + nav (People section)

### 4. Breakouts + QR: data + migration
- `ScheduleItem`: `IsBreakoutSlot`, `AllowSelfSignup`, `ParentScheduleItemId` (self FK, cascade),
  `CheckInCode` (unique), `SelfCheckInMode`.
- `ScheduleItemRegistration` entity + config (check constraint, unique `(SlotScheduleItemId, GuestAttendeeId)` and
  `(SlotScheduleItemId, UserId)`, and explicit FKs).
- Enums: `SelfCheckInMode`, and `AttendanceMethod.Qr = 2`.
- Migration `AddBreakoutsAndQrCheckIn`.

### 5. Schedule DTO + forms
- `ScheduleItemDto` gains `IsBreakoutSlot`, `AllowSelfSignup`, `ParentScheduleItemId`, and `SelfCheckInMode` (trailing optional).
- `UpsertAsync` validates one level of nesting, the same day as the slot, and no re-parent once there are registrations.
- `/admin/schedule` and the `/hub/schedule` Admin form get the Breakout select, the self sign-up/capacity
  fields, and the Check-in mode select. Options are indented under their slot in the admin table.
- The delete confirmation for a slot names its options and sign-ups.

### 6. `RegistrationService`
- Self sign-up (with an option-row `FOR UPDATE` lock for capacity), admin assign (bulk, with skip counting),
  move, remove (both delete a check-in on the old option), unassigned list, expected people with filters,
  counts, and "my registrations".

### 7. Attendance changes
- A shared identity resolver.
- `SelfCheckInAsync` rejects QR-only items and requires a registration for options.
- `QrCheckInAsync(user, code)` → `(CheckInResult, ScheduleItem?)` with the same rules and `Method = Qr`.
- The roster for options is registrations plus check-ins, with `IsRegistered`. Walk-in on an option registers them too.
- The item CSV gets a `Registered` column for options.
- `EnsureCheckInCodeAsync` and `RegenerateCheckInCodeAsync`.

### 8. Hub + Dashboard visibility
- `ScheduleVisibility.Apply(items, isAdmin, myRegistrations)`, used by `/hub/schedule` and the Dashboard.
- Placeholder rows, the option picker modal, "Your session will be assigned", and the QR-only hint.

### 9. `/admin/breakouts` page + nav (Schedule section)

### 10. QR check-in
- `/checkin/{code}` page, and `returnUrl` on `/join` + `/login` and their POST endpoints (local paths only).
- `GET /admin/attendance/item/{id}/qr.png`, the print page `/admin/attendance/item/{id}/qr`, and the
  QR modal on `/admin/attendance`.

### 11. Verification + docs
- Build, run the migrations on local Postgres, and walk through the spec's Testing section in headless Chromium.
- Mark the spec implemented and add any deviations. Update the CLAUDE.md current state and key files.
