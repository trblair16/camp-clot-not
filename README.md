# Camp Clot Not — Score Tracker

A Blazor Server web application for managing group competition scoring at Camp Clot Not (CCN), an annual summer camp for children with bleeding disorders hosted by HBDA (Alabama chapter). The 2026 theme is **Super Mario Party**.

**Deployed on Railway:** *(link TBD pre-camp)*

---

## Motivation

Camp Clot Not gives kids aged 6–18 with bleeding disorders a camp experience built around their needs and safety. During the week, staff run activities and competitions across cabin groups, tracking coins and stars to announce final standings at closing ceremonies. This platform replaces manual scorekeeping with a real-time, role-secured scoring system designed to run reliably on a staff tablet and project live to a display screen throughout camp week.

Longer term, the platform is intended to replace the chapter's Yapp subscription (~$1,600/year) for all HBDA events — camp is the pilot.

---

## Core Features

**Released (v0.2.0):**
- Real-time leaderboard ranked by stars then coins, live-updating via SignalR
- Coin and star transaction logging with staff attribution and optional note
- Transaction audit log — admins can void transactions (with confirmation) or reinstate voided ones; voided rows are visually distinguished
- Full-screen projector display at `/display` for ceremonies, live via SignalR
- Role-based access — Admin / Staff / Display roles with per-authority RBAC
- Group management with color picker and team logo upload (stored in PostgreSQL)
- User management — create, deactivate, reset password for staff accounts

**Planned:**
- SVG board game with pre-scripted block hit mechanic and token animations (v0.3.0)
- Evening mini-game spinner, pre-scripted (v0.4.0)
- Full projector board display with real-time token movement (v0.4.0)

---

## Tech Stack

| Layer | Technology |
|---|---|
| App framework | Blazor Server (.NET 8) |
| UI components | MudBlazor 6 |
| ORM | EF Core 8 + Npgsql |
| Database | PostgreSQL (Railway) |
| Real-time | ASP.NET Core SignalR |
| Auth | BCrypt + ASP.NET Core cookie sessions |
| Hosting | Railway.app |

---

## Workflow / Standards

### Branching

```
main           — stable releases only, tagged at each version
dev            — integration branch; feature branches merge here first
feature/N-name — feature branches cut from dev
```

**Release flow:** `feature/*` → PR to `dev` → PR to `main` → `git tag vX.Y.Z`

### Versioning

| Version | Milestone | Status |
|---|---|---|
| v0.1.0 | Foundation — auth, RBAC, user/group management, Railway | Released |
| v0.2.0 | Competition core — leaderboard, transactions, display | Released |
| v0.3.0 | Board game — SVG board, block hit animation | Planned ~May 19 |
| v0.4.0 | Mini-games + projector board | Planned ~May 30 |
| v1.0.0 | Camp-ready | Target June 8 |

### Code Conventions

- `IDbContextFactory<AppDbContext>` throughout — never inject `AppDbContext` directly
- Append-only transactions — never delete, only void; totals computed from non-voided rows
- Stable seed IDs in `SeedService.Id` — never use `Guid.NewGuid()` for seed rows
- Enum naming Option C: `Currency`, `AwardKind`, `Role`, `Feature`, `Permission`
- `nameof()` for all `SystemName` seed values

---

## Local Dev Setup

**Prerequisites:** .NET 8 SDK, PostgreSQL

1. Clone the repo
2. Create `CampClotNot/appsettings.Development.json` (gitignored — not committed):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=hbda_dev;Username=postgres;Password=YOUR_PASSWORD"
     },
     "Seed": {
       "AdminEmail": "tyler@hbda.local",
       "AdminPassword": "DevAdmin1!"
     }
   }
   ```
3. Apply migrations: `dotnet ef database update` from `CampClotNot/`
4. Start: `dotnet run` from `CampClotNot/` or F5 in Visual Studio
5. Open `https://localhost:63533` and log in with seed credentials

**Mockup preview** (visual reference only): `cd mockup/preview && npm run dev` → `http://localhost:5173`

---

## Testing / Documentation

Automated tests are planned post-v1.0.0 when the feature set stabilizes. All milestones are currently validated by manual testing against the scenarios in each PR's test plan.

| Document | Purpose |
|---|---|
| `REQUIREMENTS.md` | Full product spec and feature decisions |
| `CLAUDE.md` | Architecture constraints and AI pair programming context |
| `docs/superpowers/specs/2026-04-28-schema-redesign.md` | Schema design rationale |
| `mockup/ccn-mockup-v2.jsx` | React prototype — primary visual reference for UI |
