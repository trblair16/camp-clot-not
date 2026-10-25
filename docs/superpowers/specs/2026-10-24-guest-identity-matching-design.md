# Guest Identity Matching

**Date:** 2026-10-24
**Status:** Draft — spec under review
**Driver:** Reviewing Camp Harvest 2026 attendance surfaced sponsors/speakers showing up twice on
`/admin/attendance` — once as their `EventStaff` team role, once as a brand-new `GuestAttendee`
created when they personally scanned the QR code and typed their own name. Tracked in issue #354.

## Context

- Join flow: `Join.razor` (`/join`) collects First/Last name only (no email) and POSTs to
  `/account/join` in `Program.cs`, which calls `GuestAccessService.GetOrCreateGuestAsync` →
  `RecordVisitAsync` → `SignInGuestAsync`. `GuestAttendee` has a unique index on normalized
  `(FirstName, LastName)` — exact match after trim/collapse-whitespace/lowercase, no fuzzy matching
  anywhere in this codebase.
- `EventStaff` always has a non-nullable `UserId`. `Speaker`/`Sponsor` are `Role` enum values that
  are team-only labels — when someone is added via `/admin/team` with "Can sign in" unchecked, the
  underlying `User.CanSignIn = false`, `PasswordHash = ""`, email is optional, and `User.UserRoleId`
  is force-mapped to `Volunteer` while `EventStaff.UserRoleId` keeps the real label (Speaker/Sponsor/
  etc.).
- `AttendanceService.GetRosterAsync` currently unions two independent lists per event: every
  `GuestEventVisit` → `GuestAttendee`, and every active `EventStaff` → `User`. There is no
  cross-matching between the two. If a team member also guest-joins, they appear in both lists —
  two roster rows for one person.
- The same name-collision problem applies to anyone with a real sign-in account (`CanSignIn = true`)
  who scans the QR code and types their name instead of logging in: they get a disconnected
  `GuestAttendee` instead of being pointed at their real account.

## Goals

1. If the name typed at `/join` matches an existing `CanSignIn = true` account anywhere in the
   system, block guest creation and direct them to sign in instead.
2. If it matches a `CanSignIn = false` `EventStaff` record for *this specific event* (any role, not
   just Speaker/Sponsor), let them continue as a guest — same access level, no elevated permissions —
   but link the visit to that team record so Attendance shows one row tagged with their role instead
   of two.
3. Clean up the duplicates that already exist on Attendance today.

## Non-goals

- Fuzzy/nickname matching ("Bob" vs "Robert"). The only matching anywhere in this codebase is exact
  match after normalization, and this feature doesn't introduce a new matching paradigm.
- An escape hatch for two unrelated people who happen to share a name and collide with case 1 — hard
  block, no bypass.
- Any admin review UI for ambiguous matches — ambiguity falls back silently to a plain guest.

## Design Decisions

| Question | Decision |
|---|---|
| Scope of the "real account" check (case 1) | Site-wide — any `User` with `CanSignIn = true` whose normalized name matches, regardless of event. |
| Scope of the "team record" check (case 2) | This event only — `EventStaff` rows for the specific `EventId` being joined. |
| Check order | This event's team roster is checked first. A match with `CanSignIn = true` is treated as case 1 (sign in). A match with `CanSignIn = false` is case 2 (linked guest). Only when nothing on this event's roster matches do we fall back to the site-wide case-1 check. |
| Which `EventStaff` roles qualify for case 2 | Any role, not just Speaker/Sponsor — any `CanSignIn = false` team member on this event. |
| Match precision | Exact match after normalization (reuse `GuestAccessService.Normalize`) — no fuzzy matching. |
| Multiple matches, case 1 involved | Case 1 (sign-in) always wins over case 2 on ambiguity, *within the same roster check*. Note this never applies across the event-roster/site-wide boundary: if this event's team roster has any match at all (even a single `CanSignIn = false` one), that decides the outcome and the site-wide case-1 check is never consulted — so a `CanSignIn = false` team-only record on this event is linked even if an unrelated person elsewhere in the system happens to have the same name and a real sign-in account. This is intentional; the outcome is benign since case 2 only ever grants guest-level access. |
| Multiple matches, all case 2 (ambiguous team members) | Don't guess — fall back to a plain, unlinked guest. No logging. |
| Case 1 UX | Hard block. "An account already exists for this name — please sign in," with a `/login` link. No "continue as guest anyway" escape hatch. |
| Case 2 UX/access | Guest access only — identical cookie/session to any other guest, no elevated permissions. The only effect is the Attendance roster tagging them with their team role instead of "Guest" and collapsing to one row. |
| Existing duplicates | Backfilled via a startup repair in `SeedService`, using the same matching logic, applied to existing unlinked `GuestEventVisit` rows. Runs on every startup, not just once — see Backfill section below. |

## Data Model

One new nullable column:

```csharp
// GuestEventVisit.cs
public Guid? LinkedEventStaffId { get; set; }
public EventStaff? LinkedEventStaff { get; set; }
```

FK → `EventStaff.EventStaffId`, nullable, `OnDelete(DeleteBehavior.SetNull)` in `OnModelCreating`
(matching other optional-FK patterns in this codebase). It lives on `GuestEventVisit` rather than
`GuestAttendee` because a person's team role — or whether they're on the team at all — is specific to
one event, not the person globally. Migration: `AddGuestEventVisitTeamLink`.

## New Service: `GuestIdentityMatchService`

```csharp
public enum GuestIdentityMatch
{
    NoMatch,
    SignInRequired,
    LinkedTeamMember   // check TeamEventStaffId for which one
}

public record GuestIdentityMatchResult(GuestIdentityMatch Kind, Guid? TeamEventStaffId = null);

public interface IGuestIdentityMatchService
{
    Task<GuestIdentityMatchResult> MatchAsync(string firstName, string lastName, Guid eventId);
}
```

Logic (mirrors the Design Decisions table above):

1. Normalize `firstName`/`lastName` (reuse `GuestAccessService.Normalize`).
2. Query this event's `EventStaff` joined to `Users`, filtered to a normalized name match.
   - Any match has `CanSignIn = true` → `SignInRequired`.
   - Exactly one match, all `CanSignIn = false` → `LinkedTeamMember` with that `EventStaffId`.
   - More than one match, all `CanSignIn = false` → `NoMatch` (ambiguous, don't guess).
   - No matches → go to step 3.
3. Query all `Users` site-wide for a normalized name match with `CanSignIn = true`.
   - Any match → `SignInRequired`.
   - No match → `NoMatch`.

This is the single source of truth for the matching rule, used by both the live join path and the
startup backfill — the rule is never re-implemented or kept in sync across two places.

## Join Flow Changes

`Program.cs`, `/account/join` handler — right before `GuestAccessService.GetOrCreateGuestAsync`:

- Call `IGuestIdentityMatchService.MatchAsync(firstName, lastName, eventId)`.
- `SignInRequired` → redirect back to `Join.razor` with an error query flag (same pattern as the
  existing bad-event-code error). No `GuestAttendee`/`GuestEventVisit` is created. The page shows
  "An account already exists for this name — please sign in," with a link to `/login`.
- `LinkedTeamMember` → proceed with `GetOrCreateGuestAsync`/`RecordVisitAsync` exactly as today.
  Once the `GuestEventVisit` row exists, if its `LinkedEventStaffId` is still null, set it to the
  matched `EventStaffId`.
- `NoMatch` → unchanged, today's behavior.

In the implemented code, `MatchAsync` actually runs on every `/account/join` POST — before any
`GuestEventVisit` lookup, not only the first time one is created for a `(GuestAttendee, EventId)`
pair. This is arguably more correct than re-matching once: it keeps re-evaluating the match as
accounts are created/deactivated or team rosters change between visits. It's fine to run on every
request — `MatchAsync` is idempotent (same inputs always produce the same result) and cheap (two
small, indexed queries), so repeating it per request has no observable cost or side effect beyond
the one join attempt being evaluated.

## Attendance Roster Changes

`AttendanceService.GetRosterAsync`:

- When building the staff-side list (today: every active `EventStaff`/`User` for the event), skip
  any `EventStaff` row whose `EventStaffId` appears among this event's `GuestEventVisit
  .LinkedEventStaffId` values — that person is already represented by their guest entry.
- When building the `RosterEntry` for a linked guest visit, set its role/title tag from the linked
  `EventStaff.Title` (falling back to the role name) instead of the generic "Guest" label.
- Net effect: one roster row per person, correctly tagged, check-in status driven by whichever path
  (guest or staff) they actually used to interact with the event.

## Backfill

A new idempotent step in `SeedService`, following the same pattern as the existing Speaker/Sponsor
account-role repair: on startup, find every `GuestEventVisit` with `LinkedEventStaffId IS NULL`, run
`MatchAsync` for its `(GuestAttendee.FirstName/LastName, EventId)`, and set the link when the result
is `LinkedTeamMember`. `SignInRequired`/`NoMatch` results are left untouched — this backfill only
resolves case-2 duplicates (the actual complaint); it does not retroactively force anyone with a real
account to "sign in" for a visit that already happened.

This repair runs on every app startup, not once — `SeedAsync()` is called unconditionally on every
boot. That's deliberate, not an oversight: it only re-scans visits that are *still* unlinked, so after
the first run finds everything there is to find, subsequent runs are cheap (a pre-filter checks each
visit's name against its event's team roster before ever calling the more expensive `MatchAsync`, so
a visit with zero chance of matching is skipped entirely). The benefit of re-running is that it
opportunistically catches team members added to the roster *after* a guest already joined — a case
the live `/join` check can never resolve retroactively since it only runs at join time.

**Known limitation:** pre-existing duplicates where the *other* half of the duplicate is a real
sign-in account (`CanSignIn = true`), rather than a team-only record, are deliberately left as-is by
this backfill. Per the Non-goals above, there is no "ambiguous match" admin review UI and no
retroactive sign-in enforcement for a visit that already happened — so that category of pre-existing
duplicate is a known, accepted gap, not something this backfill is expected to clean up.

## Testing Plan

- `GuestIdentityMatchService`: single sign-in match (site-wide and on-event), single team match,
  mixed multi-match (sign-in wins), multi-match all-team (ambiguous → `NoMatch`), no match at all,
  and the "no match on this event, falls back to site-wide" path.
- Manual: reproduce a team member scanning the QR code themselves at an event they're staffed on —
  confirm one roster row, correctly tagged, not two.
- Manual: reproduce someone with a real account typing their name at `/join` — confirm the hard-block
  message and working `/login` link, and that no `GuestAttendee` row gets created.
- Manual: seed an existing unlinked duplicate (mirroring the Amanda-style case) and confirm the
  startup backfill links it and Attendance collapses to one row.
- Confirm `dotnet build` has no new warnings beyond the existing baseline of 3.
