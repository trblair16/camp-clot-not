# HBDA Events

A Blazor Server web app (installable as a PWA) that runs events for the Hemophilia & Bleeding Disorders of Alabama (HBDA) chapter. It started as the scoring system for **Camp Clot Not (CCN) 2026**, a camp for kids with bleeding disorders with a Super Mario Party theme. It has since grown into a platform for every chapter event.

**Events run on it:** CCN 2026 (June, `v1.0.x`), Men's Retreat 2026 (July, `v1.1.x`). Next up: Camp Harvest 2026 (October).

**Hosting:** Railway (production and a `dev` staging environment).

---

## Features

**Event Hub (staff and guests)**
- Day-by-day schedule with per-group overrides, presenters, breakout slots, and personal schedules from session picks
- Announcements with pinning, urgent badges, reactions, and Web Push notifications
- Info pages (Markdown and role-restricted PDFs), documents, staff directory, sponsors
- Dashboard: today's schedule, latest news, sponsors

**Guest access**
- Join with an event code or QR (`/join`), with no account needed
- Named guests (`GuestAttendee`), push subscriptions, "I'm here" check-in, and session sign-ups

**Event operations (admin)**
- Events: create, set active, capability flags per event, guest code/QR, "Copy setup from…" another event
- Per-event theme editor: presets, colors, logo/banner upload
- Schedule setup: smart form, .xlsx template and import, paste import, copy a past event's schedule
- Team (`/admin/team`): per-event staff and the Hub directory, with listed-only people who can't sign in
- Breakouts with capacity, attendance roster, walk-ins, per-session QR check-in, CSV export
- Users and roles (Admin / Staff / Volunteer / MedicalStaff), invite and password-reset emails (Resend)
- Incident reports with a print view matching the Children's Harbor form

**Camp competition (CCN)**
- Coins and stars per group (append-only transactions, void/reinstate) with a live leaderboard
- SVG board game with pre-scripted block hits, evening mini-game spinner, Bowser event, awards
- Projector display pages updated in real time over SignalR (`/board/display`, `/minigames/display`)

---

## Tech Stack

| Layer | Technology |
|---|---|
| App framework | Blazor Server (.NET 8), PWA (service worker + manifest) |
| UI components | MudBlazor 6 plus a custom theme-variable CSS system |
| ORM / DB | EF Core 8 + Npgsql, PostgreSQL |
| Real-time | ASP.NET Core SignalR (`LiveHub` at `/livehub`) |
| Auth | BCrypt + cookie sessions; guest cookies from event codes |
| Push / email | Web Push (VAPID) via `Lib.Net.Http.WebPush`; Resend HTTPS API |
| Other | ImageSharp (upload resize), ClosedXML (schedule import), QRCoder, Markdig, Serilog |
| Hosting | Railway (Docker), migrations applied automatically at startup |

---

## Workflow

```
main           — production; each release is an annotated tag
dev            — integration branch and staging; feature branches merge here first
feature/N-name — cut from dev (N = GitHub issue number; open the issue first)
```

**Release flow:** `feature/*` → PR to `dev` → PR to `main` → tag `vX.Y.Z`. Release history is in [`docs/RELEASES.md`](docs/RELEASES.md).

Each feature starts with a design spec in `docs/superpowers/specs/` and an implementation plan in `docs/superpowers/plans/`. PRs carry a manual test checklist.

### Code conventions

- `IDbContextFactory<AppDbContext>` throughout, with the synchronous `CreateDbContext()`. Never inject `AppDbContext` directly
- Append-only transactions: never delete, only void. Totals are computed from non-voided rows
- Stable seed IDs in `SeedService.Id`. Seed methods are insert-only for anything an admin can edit
- Anything that sets a cookie (login, password, guest join) is a native form POST to a minimal-API endpoint
- Enum naming Option C: `Currency`, `AwardKind`, `Role`, `Feature`, `Permission`

`CLAUDE.md` lists the full set of pitfalls and architecture notes.

---

## Local Dev Setup

**Prerequisites:** .NET 8 SDK, PostgreSQL

1. Clone the repo.
2. Create `CampClotNot/appsettings.Development.json` (gitignored):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=hbda_dev;Username=postgres;Password=YOUR_PASSWORD"
     },
     "Seed": {
       "AdminEmail": "tyler@hbda.local",
       "AdminPassword": "YOUR_DEV_ADMIN_PASSWORD"
     },
     "Vapid": {
       "PublicKey": "<P-256 public key>",
       "PrivateKey": "<P-256 private key>"
     }
   }
   ```
   A real VAPID key pair is required. Pages that use the push service fail without one. `Email:ResendApiKey`/`Email:From` are optional; without them, Admins get copyable reset links instead of emails.
3. Start with `dotnet run` from `CampClotNot/` (or F5 in Visual Studio). Migrations and seed data are applied at startup.
4. Open `https://localhost:63533` and log in with the seed credentials.

---

## Testing

There's no automated test suite yet (planned, see the roadmap in `CLAUDE.md`). CI (`.github/workflows/dotnet-build.yml`) runs a Release build on every push and PR. Each change is validated against the manual test checklist in its PR.

---

## Documentation

| Document | Purpose |
|---|---|
| `CLAUDE.md` | Current state, roadmap, architecture, pitfalls, key files, dev/prod setup |
| `docs/RELEASES.md` | Release tags and the full CCN 2026 build log |
| `docs/superpowers/specs/` | Design spec for each feature |
| `docs/superpowers/plans/` | Implementation plan for each feature |
| `docs/superpowers/handoffs/` | Session handoff notes from the pre-camp build |
| `REQUIREMENTS.md` | Original CCN 2026 product spec (historical; specs supersede it) |
| `mockup/ccn-mockup-v2.jsx` | React prototype used as the original visual reference |
