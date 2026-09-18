# Named Guest Identity

**Date:** 2026-09-18
**Status:** Approved, pending implementation plan
**Driver:** Camp Harvest 2026 is ~1 month out (roughly mid-October). Tyler wants to deliver a
set of guest-facing and admin-facing improvements to Vicki before then. This is sub-project 1
of a 4-part roadmap.

## Context

The guest access system shipped for Men's Retreat (v1.1.0 sub-project 5, PR #298) is fully
anonymous: scanning the `/join` QR code and entering a valid event code signs the browser in
with a cookie carrying only an `EventId` claim (`GuestAccessService.cs`). `ClaimTypes.Name` is
hardcoded to the literal string `"Guest"`. There is no persistent guest identity at all — every
event join is a clean slate, and there is no way to recognize the same person across events or
even across repeat visits within one event beyond the cookie itself.

For Camp Harvest, guests are actual named people (families/chapter members, not staff), and two
follow-on features depend on knowing who a guest is:
- Sub-project 2 (guest push notifications) needs somewhere to attach a `PushSubscription`.
- Sub-project 3 (attendance tracking) needs to attribute a check-in to a specific person and
  recognize repeat visits.

This sub-project establishes that identity layer. It does not implement push or attendance.

## Roadmap (for traceability)

1. **Named guest identity** (this doc)
2. Guest push notifications — high priority, must ship before Camp Harvest
3. Attendance tracking
4. Admin event configurability (theme UI, event duplication) — previously flagged in the
   v1.1.0 design doc as "needed before Camp Harvest 2026"

## Data Model

New entity `GuestAttendee` (`Data/Entities/GuestAttendee.cs`):

```csharp
public class GuestAttendee
{
    public Guid GuestAttendeeId { get; set; }
    public string FirstName { get; set; } = "";   // as typed, trimmed — display value only
    public string LastName { get; set; } = "";    // as typed, trimmed
    public string NormalizedFirstName { get; set; } = ""; // trimmed, whitespace-collapsed, lowercased
    public string NormalizedLastName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
```

- Unique index on `(NormalizedFirstName, NormalizedLastName)` — this is the cross-event
  matching key.
- Matching is **exact-after-normalization only** — no fuzzy/typo tolerance. Normalization:
  `Trim()`, collapse internal whitespace to single spaces, `ToLowerInvariant()`.
- If two different real people share a normalized name, they merge into one `GuestAttendee`
  record (explicit product decision — acceptable at chapter scale, avoids a disambiguation
  screen). Display name (`FirstName`/`LastName`) is set once at creation and never overwritten
  by later joins, so casing doesn't flip-flop between visits.

New join entity `GuestEventVisit` (`Data/Entities/GuestEventVisit.cs`):

```csharp
public class GuestEventVisit
{
    public Guid GuestEventVisitId { get; set; }
    public Guid GuestAttendeeId { get; set; }
    public GuestAttendee GuestAttendee { get; set; } = null!;
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public DateTime FirstJoinedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
```

- Unique index on `(GuestAttendeeId, EventId)`. Upserted on every join — a repeat scan of the
  same event just bumps `LastSeenAt`, no duplicate rows.
- This is the record of "which events has this guest joined," used by the admin guest page
  and available for sub-project 3 (attendance) to build on rather than inventing a parallel
  table.

## Join Flow

`Join.razor` gains two fields — **First Name** and **Last Name** — alongside the existing
Event Code field, submitted together in one form (single-step, per product decision).

`POST /account/join` (`Program.cs`) changes to:

1. Validate the code (`GuestAccessService.ValidateCodeAsync` — unchanged).
2. Read `firstName`/`lastName` from the form; trim; reject (redirect back with an error) if
   either is empty after trimming. Mirrors the existing server-side re-validation pattern used
   on `/account/change-password` for password length.
3. `GuestAccessService.GetOrCreateGuestAsync(firstName, lastName)` — normalizes, looks up by
   `(NormalizedFirstName, NormalizedLastName)`, returns the existing record or creates a new
   one.
4. Upsert the `GuestEventVisit` row for `(guest.GuestAttendeeId, event.EventId)`.
5. `GuestAccessService.SignInGuestAsync` — cookie claims become `GuestClaimTypes.GuestAttendeeId`
   + `GuestClaimTypes.EventId` + `ClaimTypes.Name = guest.FirstName` (replacing the hardcoded
   `"Guest"` literal). Expiry unchanged: event `ExpDate` + 1 day.
6. Redirect to `/hub/schedule` — unchanged.

Resubmitting the form for an event the guest already joined is harmless and idempotent (same
guest record, `GuestEventVisit` upsert just updates `LastSeenAt`) — no special-case redirect
needed for an already-signed-in guest.

`GuestNav.razor` gets a small "Hi, {FirstName}" touch since a real name now exists — cosmetic,
not load-bearing for this sub-project.

## Admin Guest Page

`/admin/guests`, `@attribute [Authorize(Roles = "Admin")]` — matches the existing
`/admin/sponsors` and `/admin/locations` form-panel-plus-table pattern.

- Table: First Name, Last Name, events joined (from `GuestEventVisit`, event name + last-seen
  date), created date.
- Inline rename (re-normalizes `NormalizedFirstName`/`NormalizedLastName` on save — this can
  cause two previously-distinct guest rows to collide if an admin's edit makes them match;
  out of scope to guard against this edge case for v1, since it requires the same admin to
  independently mistype two different guests into the same normalized name).
- Delete — removes the `GuestAttendee` and its `GuestEventVisit` rows. No FK guard needed yet
  since nothing else references `GuestAttendeeId` in this sub-project; sub-projects 2 and 3
  will need to reconsider this once `PushSubscription`/attendance rows exist.

`GuestAccessService` gains `GetOrCreateGuestAsync`, `GetAllGuestsWithVisitsAsync`,
`UpdateGuestNameAsync`, `DeleteGuestAsync` — same shape as `SponsorService`'s CRUD methods.

## Explicitly Out of Scope

- Push notifications for guests — they already exist for staff (`PushSubscription`, VAPID,
  `PushNotificationService`); extending them to guests is sub-project 2, next up and high
  priority for Camp Harvest.
- Attendance/check-in tracking — sub-project 3.
- Email capture, phone capture, "full member" upgrade tier (the fuller tiered-identity model
  sketched in `REQUIREMENTS.md`'s v2.1.0 roadmap entry) — not requested, not needed for Harvest.
- Fuzzy/typo-tolerant name matching or a disambiguation UI for name collisions.
- Any change to the existing anonymous-cookie mechanics beyond adding the guest identity
  claims (cookie expiry, event-code validation, and the `/join` → `/hub/schedule` redirect all
  stay as they are today).

## Testing

- Unit: name normalization (whitespace collapse, casing, empty-after-trim rejection),
  `GetOrCreateGuestAsync` match/create branching, `GuestEventVisit` upsert idempotency.
- Manual: join a fresh event with a new name (creates guest + visit); rejoin the same event
  (no duplicate visit row, `LastSeenAt` updates); join a second event with the same name
  (same `GuestAttendeeId`, new `GuestEventVisit` row); admin rename/delete on `/admin/guests`.
