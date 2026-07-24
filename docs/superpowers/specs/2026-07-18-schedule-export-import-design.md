# Schedule Export/Import — Design Sketch

**Date:** 2026-07-18
**Status:** Sketch for future scoping — not planned for immediate implementation.
**Driver:** With a real staging environment now in place (`dev` → Railway staging), Vicki/Amanda
could build out a schedule there without touching prod, then need a way to move it over without
re-entering everything by hand. Raised alongside the existing v1.2.0 `ScheduleTemplate` roadmap
item, which this should share an engine with rather than duplicate.

## Problem

Two related but distinct needs, both currently unserved:

1. **Within one environment**: "start Men's Retreat's schedule from CCN's" — already scoped as
   `ScheduleTemplate` + `ScheduleTemplateItem` in the v1.2.0 roadmap (data layer only so far, no
   admin UI).
2. **Across environments**: "I built this on staging, now put it in prod" — new, not currently
   covered by anything. Staging and prod are separate databases; a `ScheduleItem` row references
   `LocationId`, `ActivityId`, `ScheduleItemTypeId`, and (per group) `GroupId` — all
   `Guid.NewGuid()`'d independently in each environment when an admin creates them through the UI.
   Copying a `ScheduleItem` row's raw GUIDs from staging to prod will reference the wrong thing
   or nothing at all.

## Key idea: one engine, two entry points

Both needs are "take a set of schedule items, replay them against a target event, re-resolving
FK references by name instead of trusting the original GUIDs." The only difference is where the
intermediate representation lives:

- **Template (same environment)**: intermediate representation is a `ScheduleTemplate` DB row.
- **Export/Import (cross environment)**: intermediate representation is a downloaded JSON file
  containing the same shape, re-uploaded on the other side.

Build the serialize → match/validate → apply pipeline once; both features are thin wrappers
around it (DB round-trip vs. file round-trip). This also means the template feature — already
roadmapped — becomes the natural place to land this rather than a parallel system.

## Portable schedule item shape

References resolved by **name**, not GUID, since GUIDs mean nothing outside their environment:

```
PortableScheduleItem
  CampDay          (DateOnly, or day-offset-from-event-start — see Open Questions)
  StartTime / EndTime
  Title / Description
  ScheduleItemTypeName   (matches ScheduleItemType.Name — e.g. "Meal", "Presentation")
  LocationName?          (matches Location.Name; null if LocationOther used instead)
  LocationOther?
  ActivityName?          (matches Activity.Name, if set)
  PresenterName? / PresenterBio?
  AppliesToAllGroups
  MaxCapacity?
  GroupOverrides[]       (per ScheduleItemGroup row)
    GroupShortName       (matches Group.ShortName — e.g. "BB", "MU")
    ActivityName?
    LocationName?
    Note?
```

`CreatedBy` is deliberately **not** carried over — on import/apply, the importing admin becomes
the creator of the new rows, same as any other admin action.

## Cross-environment reference resolution

This is the actual hard part the user flagged, not the file format. On import:

1. For each `ScheduleItemTypeName` referenced: look up by `Name` in the target environment.
   Low-risk in practice — the 6 seeded types (Activity, Meal, Travel, Free, Mandatory,
   Presentation) exist everywhere via `SeedScheduleItemTypesAsync`, and an event only needs the
   ones enabled via `EventScheduleItemType` for its own dropdown, not a hard blocker for import.
2. For each `LocationName` referenced: look up by `Name`. **Locations are not event-scoped** in
   the current schema (`Location` has no `EventId` — confirmed by reading the entity directly)
   so this is a database-wide name lookup, not scoped to the target event.
3. For each `ActivityName` / `GroupShortName` referenced: same name-lookup approach. Groups
   *are* event-scoped (`Group.EventId` exists), so group matching is scoped to whichever event
   the import is targeting — and will simply have nothing to match for events with no
   competition groups (Men's Retreat has none per the sub-project 4 notes), which is fine —
   group-level overrides just don't apply.
4. Anything that doesn't resolve gets surfaced in a **pre-commit review screen**, not silently
   dropped or silently auto-created:
   - "3 of 4 locations matched. 'Riverwalk Pavilion' has no match in this environment — [Create
     it] or [Map to existing: dropdown]."
   - Same pattern for activity names and schedule item types (rare, but events can toggle a
     type off entirely via `EventScheduleItemType`, or theoretically add custom ones later).
   - Nothing gets written to the DB until the admin resolves every unmatched reference.

## Granularity

- **Whole event**: all `ScheduleItem` rows for the source event's `EventId`.
- **Single day**: filtered to one `CampDay`. On import, the admin picks which day in the target
  event the items should land on (source and target events almost certainly have different date
  ranges) — so day-level export should probably store `CampDay` as a day-offset from the source
  event's `EffDate` rather than an absolute date, and apply it as an offset from the target
  event's `EffDate` on import. Absolute dates would silently misfire the first time this crosses
  a differently-dated event.

## Where this lives in the UI

Natural home is `/admin/schedule`, alongside the existing day-tab view:
- **Export**: "Export Day" / "Export Event" actions → downloads a `.json` file.
- **Import**: "Import Schedule" action → file upload → resolution/review screen (above) →
  confirm → bulk-insert `ScheduleItem` + `ScheduleItemGroup` rows for the *active* event.
- If/when the template UI ships (v1.2.0+), "Save as Template" and "Apply Template" are the same
  flow with a DB row standing in for the file.

## Explicitly out of scope for a first pass

- Round-tripping `Announcement`, `StaffMember`, `Sponsor`, or `IncidentReport` data — this spec
  is schedule-only. Could generalize the same name-matching engine later if there's appetite.
- Conflict resolution for *re-importing* into an event that already has schedule items (e.g.
  "merge" vs. "replace" semantics) — first pass should probably just require an empty/near-empty
  target schedule and refuse (or warn loudly) otherwise, punting real merge logic to a follow-up.
- Automated environment-to-environment sync/push — this is a manual, admin-initiated,
  file-in-hand action, not a live sync between staging and prod.

## Open questions to resolve before implementation planning

- Confirm whether `Activity` should resolve by `Name` alone, or `Name` + its `LocationId`/type
  chain — activities can be reused across different contexts (MinuteToWinIt vs. board-space
  placeholders), so name alone might be ambiguous in a database with many activities.
- Decide the day-offset-vs-absolute-date question above before writing the JSON shape, since
  it's awkward to change after real exported files exist in the wild (e.g. saved on someone's
  desktop between sessions).
- Whether this ships before or after the in-DB `ScheduleTemplate` UI — building the shared engine
  first and standing up templates on top of it (rather than export/import on top of templates)
  might be the more natural build order, since templates have no cross-environment GUID problem
  to solve and are a smaller first cut of the same engine.
