# Schedule Export

**Date:** 2026-10-14
**Status:** Implemented (2026-10-14) on `feature/335-schedule-export` (#335). See "Implementation Notes".
**Driver:** Event admins build a schedule on staging, then need to move it to production. Staging
and production are separate databases. Faster schedule setup (#317) added a spreadsheet import and
"Copy from another event", but copying only works inside one database and there's no export.
This completes the 2026-07-18 export/import sketch.

## How it works

Copying a schedule already does the hard part: `ScheduleImportService.CopyAsync` maps days by
position, matches activities and groups by name, and keeps breakout slots with their options. It
just reads its source straight from the database. This feature splits copying into two halves with
a **portable schedule** in between:

```
event's schedule ──Export──▶ PortableSchedule ──Apply──▶ target event
                               │          ▲
                     .json download    .json upload (another site)
```

- **Export** reads an event's schedule into a `PortableSchedule`, with references written as names
  (location, type, activity, group). Location and type IDs are kept as well, so the same
  database still matches exactly.
- **Apply** resolves those names in the target database, shows a preview, and adds the items.
- "Copy from another event" becomes Export then Apply in memory, so there's one engine for both.

## Design Decisions

| Topic | Decision |
|---|---|
| Formats | **Both.** `⬇ Export (.json)` is the full-detail file for moving a schedule to another site. `⬇ Export (.xlsx)` is the current schedule in the spreadsheet-template layout, for reading or editing. |
| What the .json carries | Everything "Copy from another event" carries: day, times, title, description, type, location, additional info, activity, presenter name **and bio**, attendance tracking and check-in mode, **breakout slots with their options, capacity, and self sign-up**, and per-group overrides (group, activity, location, note). Not carried: sign-ups, check-ins, QR check-in codes (a new code is made on first use), who created the item. |
| What the .xlsx carries | The template's 10 columns, plus an informational **Breakout** column (`Slot` / `Option of: <slot title>`). The spreadsheet format can't rebuild breakouts or group overrides. Re-importing it adds slots and options as ordinary items, with a warning on each such row, and its help sheet points to the .json for moving schedules. |
| Days | Stored as a **day index** (day 1, day 2…) plus the original date for reference. Day 1 of the file becomes day 1 of the target event, the same rule as Copy. Days past the target's end are skipped and counted in the preview. |
| Where to import | The existing **⬆ Import** dialog accepts `.json` alongside `.xlsx` and `.csv`. A .json shows a Copy-style preview: source event and dates, day-by-day mapping with item counts, duplicates, new locations, and anything that didn't match. |
| Already on the schedule | **Skipped** (same day, start time, and title), as with spreadsheet import and Copy. Safe to run twice. An item edited on staging after it was already imported to prod isn't updated. Delete it on prod and import again. An option whose slot is skipped is skipped too. |
| Locations | Matched by ID (same database), then by name. Unknown names are **created** by default (without an image). With the box unticked, an item's location name goes into Additional Info instead, and a group override's location is dropped. The preview lists the new names. |
| Types | Matched by ID, then `SystemName`, then `Name`. A type that isn't enabled for the event is enabled. An unknown type falls back to the event's default type, with a warning. |
| Activities and groups | Event-scoped, so matched **by name in the target event** (groups also by short name). No match means the activity link or group override is dropped, and the preview counts them. |
| Validation | The file must have `format: "hbda-events-schedule"` and `version: 1`, at most 2,000 items, and a title, day index, and start time on every item. Anything else is refused with a plain message, and nothing is saved. Upload limit 5 MB. |
| Who | Admin only, like the rest of `/admin/schedule`. Export endpoints use the same Admin policy as the template download. |

## Out of scope

- Updating items that already exist on the target ("merge"), or replacing the target's schedule.
- Exporting locations themselves (images, sort order), info pages, sponsors, team, or theme.
- Live sync between staging and production.
- Schedule templates as a database entity (v1.2.0 roadmap). They'd reuse this engine.

## Testing

Manual, in the running app (no automated suite yet):

1. Export (.json) an event with a breakout slot (two options, capacity, self sign-up off), a group override, a presenter bio, and attendance tracking.
2. Import that file into a different event with different dates. Day 1 lands on day 1, and every field above survives.
3. Edit the file to rename a location, then import. The preview lists the new location. With the box ticked it's created; with it unticked the name lands in Additional Info.
4. Import the same file twice. The second time everything is "already here".
5. Upload a broken or foreign .json. You get a clear message, and nothing is saved.
6. "Copy from another event" still works as before (same preview, same result).
7. Export (.xlsx) opens in Excel, with the Breakout column filled. Re-importing it warns on breakout rows.

## Implementation Notes (2026-10-14)

- **Code:** `Services/PortableSchedule.cs` (file model, JSON options, `ReadAsync` validation) and `ScheduleImportService` (`ExportAsync`, `PreviewAsync`, `ApplyAsync`, plus a private `Resolver` and `Plan` that Preview and Apply share so they always agree). `PreviewCopyAsync`/`CopyAsync` are now Export then Preview/Apply, and `CopyPreview` became `SchedulePreview`.
- **Same database:** location and type IDs are written alongside the names and matched first, so "Copy from another event" resolves exactly as it did before (verified: no new locations, same items).
- **Copy dialog:** it now lists what didn't match (activities and group overrides left off). The old copy dropped those silently.
- **Format strictness:** `format` and `version` have no defaults, so a stray JSON file (`{"hello": 1}`) is refused instead of looking like an empty export. Explicit nulls (`"items": null`, `"groups": null`) are normalized rather than crashing.
- **Day index:** negative indexes are allowed in the file (an item dated before its event's start, left after the event's dates were edited) and are skipped like days past the end.
- **Import button:** "⬆ Import spreadsheet" is now "⬆ Import", since it takes .json too.
- **Verified** with two app instances on two separate Postgres databases (the staging → production case), via Playwright: export, import with day shift (Jun → Oct), the duplicate skip, the out-of-range day skip, a case-insensitive location match, new location created once for both an item and a group override, the unticked box putting the name in Additional Info, a second import offering 0 items, five malformed files with clear messages, Copy from another event, and Breakout-column warnings in the spreadsheet import.
