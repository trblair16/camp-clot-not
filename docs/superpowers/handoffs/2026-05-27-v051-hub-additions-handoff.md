# v0.5.1 Handoff — Hub Additions (Incident Reports + Sponsors + Info Cleanup)

## Context

This handoff continues work on `feature/103-mobile-pwa-fixes` (GitHub issue #103).
The original v0.5.1 mobile/PWA fixes are **already committed** on this branch (2 commits).
This handoff covers the **additional features** added to scope during the 2026-05-27 session.

**Do NOT create a new branch.** Continue on `feature/103-mobile-pwa-fixes` off the current HEAD.

---

## What's Already Done (do not redo)

All 7 original items from `2026-05-26-v051-handoff.md` are committed:
- User edit dialog on `/admin/users`
- PWA `short_name` → "CCN 2026"
- `viewport-fit=cover` in `_Layout.cshtml`
- Desktop nav mobile suppression (`@media (max-width: 768px) { display: none !important }`)
- Bottom nav safe-area-inset padding
- Activities → Transactions swap for Admin in mobile bottom nav
- PWA icons regenerated from `ccn-logo-2026.png` (tall 3-row version)
- Children's Harbor logo extracted to `CampClotNot/wwwroot/img/childrens-harbor-logo.png`

---

## Features to Build

### 1. Incident Report

**What:** A floating "🚨 Report Incident" button visible on all Hub pages. Opens a form modal. All roles (Admin, Staff, Volunteer) can submit. Only Admin can view submitted reports.

**Floating button placement:** In `HubSubNav.razor` — it's included on every hub page, so the button lives there as a fixed-position element (bottom-right, same visual pattern as the "+ Log Score" button on the Dashboard). The form modal also lives in HubSubNav.

**Form fields (match the Children's Harbor paper form exactly):**
- Date of Incident (date picker, required)
- Date Completed (date, auto-filled to today, read-only or editable)
- Persons Involved (textarea)
- Description of the Incident (large textarea — "include what led up to and responses to the incident")
- Recommended Action (textarea)
- Submitter info: **auto-captured from auth state** — do NOT show signature fields. Record `SubmittedByName` and `SubmittedByRole` from the logged-in user's claims.

**Admin view:** New "Incidents" tab in `HubSubNav` — **admin only** (use `AuthorizeView`). Page at `/hub/incidents`. Lists all submitted reports newest-first. Each row shows date, submitter name/role, persons involved (truncated), and an "Acknowledge" button. Acknowledged reports show who acknowledged and when.

**Print view:** Route `/hub/incidents/{id}/print`. Uses a bare `PrintLayout` (no nav — identical concept to `LoginLayout` but for printing). Mirrors the Children's Harbor paper form layout:
- Children's Harbor logo (`/img/childrens-harbor-logo.png`) top-right
- "INCIDENT REPORT FORM" centered title
- Fields laid out like the paper form (Date of Incident | Date Completed on same row, then Persons Involved, then Description block, then Recommended Action block, then at bottom: "Submitted by: [name] ([role]) on [date]")
- A "🖨️ Print" button that calls `window.print()` (hidden on actual print via `@media print`)
- `@media print` CSS hides the print button and any other non-form chrome

**`IncidentReport` entity:**
```csharp
public class IncidentReport
{
    public Guid IncidentReportId { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public DateTime DateOfIncident { get; set; }
    public DateTime DateCompleted { get; set; }
    public string PersonsInvolved { get; set; } = "";
    public string Description { get; set; } = "";
    public string RecommendedAction { get; set; } = "";
    public Guid SubmittedByUserId { get; set; }
    public string SubmittedByName { get; set; } = "";
    public string SubmittedByRole { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public bool IsAcknowledged { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public string? AcknowledgedByName { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
```

**`IncidentReportService` methods needed:**
- `GetAllForEventAsync(Guid eventId)` — ordered by SubmittedAt descending
- `GetByIdAsync(Guid id)` — for print view
- `SubmitAsync(IncidentReport report)` — sets SubmittedAt = UtcNow, links to CCN 2026 event
- `AcknowledgeAsync(Guid id, Guid adminUserId, string adminName)` — sets IsAcknowledged, AcknowledgedBy*, AcknowledgedAt

---

### 2. Sponsors

**What:** Admin manages a list of sponsors (name, logo URL, optional website link, sort order). All users can view a Sponsors tab in the Hub sub-nav showing logo tiles.

**`Sponsor` entity:**
```csharp
public class Sponsor
{
    public Guid SponsorId { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Name { get; set; } = "";
    public string LogoUrl { get; set; } = "";   // external URL — no file upload
    public string? Website { get; set; }
    public int SortOrder { get; set; }
}
```

**`SponsorService` methods:**
- `GetAllForEventAsync(Guid eventId)` — ordered by SortOrder
- `UpsertAsync(Sponsor s)` — same pattern as LocationService.UpsertAsync
- `DeleteAsync(Guid sponsorId)`

**Display page:** `/hub/sponsors` — responsive grid of logo tiles. Each tile: logo image (constrained height, e.g. 80px), sponsor name below. If Website is set, the tile is a link. Visible to Admin, Staff, Volunteer.

**Admin CRUD page:** `/admin/sponsors` — same style as `/admin/locations`. Table of sponsors with Edit/Delete inline. "Add Sponsor" form panel with: Name (required), Logo URL (required), Website (optional), Sort Order (default 0). Add link to admin dropdown in `AppNav.razor` (both desktop panel and mobile sheet).

---

### 3. Info Tab Cleanup

Remove `schedule-overview` and `packing` from the seeded InfoPage set in `SeedService.SeedInfoPagesAsync`. These slugs are redundant (Schedule has a full tab; packing is pre-camp only). **Do NOT delete existing database records** — just remove them from the seed list so they aren't created on fresh databases. Keep `rules`, `faq`, and `medical`.

The stable seed IDs for the removed pages (`InfoPageScheduleOverview`, `InfoPagePacking`) should also be removed from `SeedService.Id` to keep it clean.

---

## Hub Sub-Nav Structure (final state after this PR)

| Tab | Route | Roles |
|---|---|---|
| 📅 Schedule | `/hub/schedule` | Admin, Staff, Volunteer |
| 📣 Announcements | `/hub/announcements` | Admin, Staff, Volunteer |
| 👥 Staff | `/hub/staff` | Admin, Staff, Volunteer |
| 📋 Info | `/hub/info` | Admin, Staff, Volunteer |
| 🏆 Sponsors | `/hub/sponsors` | Admin, Staff, Volunteer |
| 🚨 Incidents | `/hub/incidents` | Admin only |

The Incidents tab must be wrapped in `<AuthorizeView Roles="Admin">` in `HubSubNav.razor` so Staff/Volunteer never see it.

---

## Service / Architecture Pattern

Follow the **v0.5.0 service pattern** (not the older repository pattern):
- Services inject `IDbContextFactory<AppDbContext>` directly — no separate `IXRepository` interface
- Use `using var db = factory.CreateDbContext()` in every method
- Register as `builder.Services.AddScoped<IncidentReportService>()` and `builder.Services.AddScoped<SponsorService>()` in `Program.cs`
- Add `DbSet<IncidentReport>` and `DbSet<Sponsor>` to `AppDbContext` under the `// Hub (Camp Info)` section
- Neither entity needs explicit PK config in `OnModelCreating` — EF finds `IncidentReportId` and `SponsorId` by convention

---

## Files to Create

| File | Purpose |
|---|---|
| `CampClotNot/Data/Entities/IncidentReport.cs` | Entity |
| `CampClotNot/Data/Entities/Sponsor.cs` | Entity |
| `CampClotNot/Services/IncidentReportService.cs` | Service |
| `CampClotNot/Services/SponsorService.cs` | Service |
| `CampClotNot/Pages/Hub/Incidents.razor` | Admin review list |
| `CampClotNot/Pages/Hub/IncidentPrint.razor` | Print view at `/hub/incidents/{id}/print` |
| `CampClotNot/Pages/Hub/Sponsors.razor` | Public sponsor logo grid |
| `CampClotNot/Pages/Admin/Sponsors.razor` | Admin CRUD |
| `CampClotNot/Shared/PrintLayout.razor` | Bare layout for print (no nav, no MudBlazor providers) |

## Files to Modify

| File | Change |
|---|---|
| `CampClotNot/Data/AppDbContext.cs` | Add `DbSet<IncidentReport>` and `DbSet<Sponsor>` |
| `CampClotNot/Program.cs` | Register `IncidentReportService` and `SponsorService` |
| `CampClotNot/Services/SeedService.cs` | Remove `schedule-overview` and `packing` from InfoPage seeds |
| `CampClotNot/Shared/HubSubNav.razor` | Add Sponsors + Incidents tabs; add floating Report Incident button + modal |
| `CampClotNot/Shared/AppNav.razor` | Add `/admin/sponsors` to desktop panel and mobile admin sheet |
| `CampClotNot/Migrations/` | New EF migration for IncidentReport + Sponsor tables |

---

## PrintLayout

Create `CampClotNot/Shared/PrintLayout.razor`:
- Inherits `LayoutComponentBase`
- No nav, no MudBlazor providers (same rule as LoginLayout — MudBlazor DOM elements interfere with layout)
- Include `ThemeHead` for CSS variables
- Just renders `@Body`
- `@media print` CSS to hide the Print button should live in `IncidentPrint.razor` itself

`IncidentPrint.razor` declares `@layout PrintLayout` at the top and `[Authorize(Roles = "Admin")]`.

---

## CCN 2026 Event ID

All new entities are scoped to `EventId = SeedService.Id.EventCcn2026` which equals `new Guid("00000009-0009-0009-0009-000000000001")`.

---

## Pitfalls

- **PrintLayout must NOT include MudDialogProvider or MudSnackbarProvider** — same rule as LoginLayout. These render DOM elements that can interfere with print layout. Include only `ThemeHead`.
- **Do not delete existing InfoPage records** — removing from seed just prevents creation on fresh databases. Existing `schedule-overview` and `packing` records in prod/dev remain and can be managed manually.
- **Floating button z-index** — the Report Incident button should be `z-index: 60` (above the bottom nav at 40, above hub sub-nav, below any modals at 100). Position: `fixed; bottom: calc(80px + env(safe-area-inset-bottom)); right: 20px` so it clears the mobile bottom bar.
- **`@media print` hides the button** — add `@media print { .report-incident-btn { display: none; } }` in HubSubNav's style block.
- **Never use PowerShell for source file text replacement** — always use the Edit tool (encoding corruption risk, CLAUDE.md pitfall #1).
- **EF migration command:** Run from `CampClotNot/` directory: `dotnet ef migrations add AddIncidentReportAndSponsor`

---

## PR Flow

When all work is done, PR `feature/103-mobile-pwa-fixes` → `dev` → `main`. Close GitHub issue #103 when merged to main.

## v0.5.2 Note (do not implement here)

Feedback → GitHub Issues feature was discussed and scoped to v0.5.2. The repo is **public** so a separate private repo for feedback issues should be considered before building. Open a new GitHub issue for this after v0.5.1 merges.
