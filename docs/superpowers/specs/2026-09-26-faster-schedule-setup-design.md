# Faster Schedule Setup

**Date:** 2026-09-26
**Status:** Implemented (2026-09-26) on `claude/guest-attendance-extend-djwotf`, alongside #311.
**Driver:** Vicki spends a long time entering each event's schedule. She usually has it in a Word
doc and retypes it one item at a time on `/admin/schedule`. Tyler asked for three things: a smarter
form, a spreadsheet format she can fill in and import, and copying the schedule from a previous
event of the same kind.

## Where the time goes today

1. Every item goes through a long form, with times typed as free text.
2. After **Add**, the form resets completely. The day jumps back to the event's first day and the
   times clear, so entering a 12-item day means picking that day 12 times.
3. **Copy** copies one item, and only to the next day.
4. New events start with an empty schedule. #310's duplication deliberately doesn't copy the
   schedule, and there's no other way to bring one over.
5. The schedule already exists in a document, so it gets retyped by hand.

## Decisions

| Topic | Decision |
|---|---|
| Smarter form | `/admin/schedule` gets **day tabs** ("All" plus each event day). A new item defaults to the tab being viewed. After **Add**, the form keeps the day and type, and the start time is prefilled with the end of that day's latest item ("next open slot"). The same start-time default applies to the Admin **+ Add Event** form on `/hub/schedule`, which already defaults to the viewed day. |
| Lenient times | Both forms and the import accept `9`, `930`, `9:30`, `9:30a`, `2pm`, `2:15 PM`, and `14:00`. A range like `9:00 – 10:30 AM` in the start field fills both times (the `AM`/`PM` at the end applies to both). |
| Spreadsheet format | An **.xlsx template** built for the active event: the Schedule sheet has headers, and dropdowns for Day (the event's dates), Type (enabled types), Location (existing locations; typing a new one is allowed with a warning), and Track Attendance (Yes/No). A second sheet has short instructions. Download it from `/admin/schedule` ("⬇ Spreadsheet template"). |
| Import sources | Upload the filled **.xlsx** (or a **.csv**), or **paste** rows copied from Excel, Google Sheets, or a Word table (tab-separated). |
| Columns | `Day, Start, End, Title, Type, Location, Additional Info, Description, Presenter, Track Attendance`. They're matched by header name in any order, with a few synonyms (Date→Day, Time/Start Time→Start, End Time→End, Event/Name→Title, Room/Where→Location, Notes→Additional Info). With no header row, the template's column order is assumed. |
| Day values | A date (`10/17/2026`, `2026-10-17`, `Oct 17`, `Sat 10/17`, or an Excel date cell), a weekday name (when it's unique within the event), or `Day 1` / `1`. It must fall within the event. |
| Unknown type | It falls back to the event's first enabled type, with a warning. A known type that isn't enabled for this event is enabled on import. |
| Unknown location | It's created as a new location on import (the default), with a warning listing the new names. With that box unticked, the text goes into Additional Info instead. |
| Preview | Nothing is saved until the Admin reviews a preview table with a status per row: ✓ ready, ⚠ warning (imported), ✗ error (skipped, with the reason), or "already on the schedule" (the same day, start, and title; skipped). **Import N items** commits everything in one save. |
| Breakouts in the import | Out of scope. Slots and options are set up on the form after importing. |
| Copy from a past event | "Copy from another event" on `/admin/schedule`. Events of the **same kind** (`EventTypeId`) are listed first. Days map by position: the source's day 1 becomes the target's day 1. Source days past the end of the target are skipped and counted. A preview shows item counts per day, and anything already on the target schedule with the same day, start, and title is skipped. |
| What copying brings | Title, times, description, type (enabled for the target if needed), location (locations are global), additional info, presenter, attendance tracking and check-in mode, and **breakout slots with their options, capacity, and self sign-up**. Activities and group assignments are matched **by name** in the target event and dropped when there's no match. Not copied: sign-ups, check-ins, QR codes. |
| Event duplication (#310) | Unchanged. Copying the schedule is a separate action on the schedule page, so it also works for an event that already exists. |

## Out of scope

- Copying one whole day to other days. The per-item Copy stays, and copying a whole event covers
  the recurring case.
- Parsing a free-form Word document. Vicki pastes a table or fills in the template.
- Round-tripping to another environment (staging → prod). That's the 2026-07-18 export/import sketch.

## Testing

Build with no new warnings, then a headless-Chromium walkthrough:

- **Form:** add an item on a day tab. The day stays on that tab, the start is prefilled, and the type
  is kept. `930` and `2pm` parse.
- **Template:** download the .xlsx, fill it with ClosedXML (dates as real Excel dates and as text),
  and upload it. The preview shows new locations, an unknown type, a bad day, and a duplicate.
  Importing adds exactly the ready and warning rows.
- **Paste:** paste tab-separated text and get the same preview.
- **Copy:** copy CCN's schedule into another event. The days map by position, a breakout slot keeps
  its options, and running it twice skips everything as already there.

## Implementation Notes (2026-09-26)

- **`Services/ScheduleImportService.cs`** holds the engine: `TryParseTime`/`TryParseTimeRange`/`TryParseDay`,
  `ReadXlsx`/`ReadDelimited`, `ParseAsync` (preview), `ImportAsync`, `BuildTemplateAsync`, and
  `GetCopySourcesAsync`/`PreviewCopyAsync`/`CopyAsync`. The template is served by
  `GET /admin/schedule/template.xlsx` (Admin only, active event).
- **New dependency: ClosedXML 0.104.2** (MIT), used to read and write .xlsx.
- **Dropdowns use named ranges** (`List1`–`List4` on a hidden `Lists` sheet). A direct cross-sheet list
  reference is written as an Excel-only x14 extension, which other apps (and openpyxl) drop.
- **Paste box** binds on input, so **Preview** works right after pasting without clicking away first.
- **Verified** in headless Chromium against Postgres:
  - Copying CCN's schedule into an empty event keeps the breakout slot → option link, and a second
    copy offers 0 items.
  - The downloaded template was filled with openpyxl (text days, a real Excel date cell, Excel time
    cells, `930`, and `2-3 pm`) and uploaded. The preview flagged a new location, an unknown type, a
    bad day, a missing title, and a duplicate, and **Add** saved exactly the three good rows (with the
    presenter and attendance flag).
  - Pasting tab-separated rows with `Date/Time/Event/Room` headers worked.
  - On a day tab, the form defaults to that day and the next open slot, and stays there after
    **Add**.
  - Twelve time formats parsed as intended (`11:30-1pm`, `7`, `6:30a`, `12:15`, `14:00`,
    `9–10:30 AM`, `8:00 a.m.`, `1030`, `noon - 1`). `25:00` and `soonish` are rejected with a
    reason.

