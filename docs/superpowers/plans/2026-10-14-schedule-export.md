# Schedule Export: Plan

Spec: `docs/superpowers/specs/2026-10-14-schedule-export-design.md` (#335)

1. `Services/PortableSchedule.cs`: the file model (`PortableSchedule`, `PortableScheduleItem`, `PortableItemGroup`), the JSON options (camelCase, enums as strings), and `Read(stream)` returning the schedule or a plain error message.
2. `ScheduleImportService`:
   - `ExportAsync(eventId)`: the event's schedule as a `PortableSchedule`. Slots come before their options, and options carry `parentKey`.
   - `PreviewAsync(schedule, targetEventId)`: per-day mapping, duplicates, new locations, unmatched types/activities/groups. Returns `SchedulePreview`, which replaces `CopyPreview`.
   - `ApplyAsync(schedule, targetEventId, createLocations, userId)`: resolves references and adds everything in one save.
   - `PreviewCopyAsync` / `CopyAsync` become Export then Preview/Apply.
3. `ScheduleImportService.BuildTemplateAsync` → a shared workbook builder. `BuildExportXlsxAsync(eventId)` fills the rows and adds the Breakout column. The spreadsheet import recognizes a `Breakout` header and warns on those rows.
4. `Program.cs`: `GET /admin/schedule/export.json` and `/admin/schedule/export.xlsx` for the active event (Admin).
5. `Pages/Admin/Schedule.razor`: Export buttons; the Import dialog accepts `.json` and shows the schedule preview; the Copy dialog uses `SchedulePreview`.
6. Verify: `dotnet build` (3 baseline warnings), then run the app on a local Postgres and walk the spec's testing list with Playwright.
