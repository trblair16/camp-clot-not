# Camp Clot Not — Claude Code Briefing

## What This Is
A Blazor Server (.NET 8) web app for Camp Clot Not (CCN), a camp for kids with bleeding disorders run by HBDA (Alabama chapter). The 2026 theme is Super Mario Party. The platform is being built to run camp scoring/competition for June 20-25, 2026, with a longer-term goal of replacing the chapter's Yapp subscription (~$1,600/year) for all chapter events.

**Primary spec:** `REQUIREMENTS.md`  
**Schema redesign spec:** `docs/superpowers/specs/2026-04-28-schema-redesign.md`

---

## Current State (as of 2026-05-06)

**Active branch:** `feature/3-board-game` (cut from dev)
**Released:** v0.1.0 — Foundation, v0.2.0 — Competition Core (both tagged on main)

**v0.1.0 — Done:**
- Blazor Server project: entities, repositories, services, SignalR hub, MudBlazor pages
- Schema redesign: 23 entity classes, Option C enum naming (`Currency`, `AwardKind`, `Role`, `Feature`, `Permission`), 22-table PostgreSQL schema
- `AddDbContextFactory` pattern (Blazor Server concurrency fix)
- BCrypt/cookie auth: login via native POST to `/account/login`, 24-hour sliding sessions
- RBAC: role + per-user authority claims loaded into session at login
- User management: create, deactivate, reset password (`/admin/users`)
- Group management: create, edit, delete — scoped to CCN 2026 event (`/admin/groups`)
- Startup seed service: all reference data + CCN 2026 event + admin user (idempotent)
- Railway infrastructure: PORT env var, `postgres://` URI conversion, `/health` endpoint

**v0.2.0 — Done:**
- Real-time leaderboard: colorful group cards with rank badges (gold/silver/bronze), Fredoka One coin/star currency pills, SignalR `ScoresUpdated` live updates, recent transactions feed
- Log transaction UI: select group, currency type, amount, note; broadcasts `ScoresUpdated` on post
- Transaction audit log: void (admin-only, confirmation dialog) + reinstate with confirmation; voided rows dimmed with strikethrough; `ScoresUpdated` fires on both
- Projector display (`/display`): full-screen dark scoreboard, AppBar-free `DisplayLayout`, SignalR live updates
- Group admin: native color picker with live preview swatch; team logo upload (base64 stored in `TokenAssetPath`, 500 KB limit); logo shown in dashboard avatar and projector display
- SeedService: Mario-themed placeholder groups (6 groups, awaiting real names from Vicki/Katelyn/Amanda) with upsert-by-stable-ID pattern
- Program.cs: `UseUrls` only applied when Railway `PORT` env var is set — dev uses `launchSettings.json`
- MainLayout: `MudAppBar` as direct child of `MudLayout` so content clears the nav bar correctly
- Legacy 2021 Python/React capstone removed; CI replaced with `dotnet-build.yml`

**v0.3.0 — Up next (Board & Block Hit, target ~May 19):**
1. SVG board display — winding snake path, typed spaces, group tokens at current positions
2. Board admin UI — create/edit `BoardSpace` rows, load `ScriptedBlockHit` sequences per group per day
3. Block hit trigger (admin tablet) — broadcasts SignalR events; animation plays on `/display`
4. Step-by-step token movement animation on the projector via SignalR
5. `GroupBoardPos` updated in DB when token lands
6. `/display` redesigned: board ~70% of screen, leaderboard sidebar ~30%

**Open decisions (still pending):**
- [ ] Board space count (waiting on activity list from Katelyn/Vicki — mockup uses 20)
- [ ] Board visible all week on projector, or only at gatherings?
- [ ] Display mode: login or PIN-protected URL (`/display?pin=XXXX`)?
- [ ] Group names: 6 placeholder Mario groups in seed — update `SeedGroupsAsync` when real names confirmed

---

## Architecture — Critical Points

- **Railway.app hosting** — `PORT` env var applied via `UseUrls` only when set (Railway); dev uses `launchSettings.json`. `postgres://` URI converted in `Program.cs`. HTTPS terminated externally — no `ForceHttpsRedirection` in prod.
- **Blazor Server + SignalR** — projector display (/display route) receives real-time updates via SignalR hub (`/camphub`). Block hit animation MUST play on projector via SignalR broadcast triggered from admin tablet — plan hub message types before building.
- **No Flask middleware in v1** — service → repository → EF Core → PostgreSQL.
- **Append-only transactions** — coins/stars never deleted, only voided. Totals always computed from non-voided transactions, never stored.
- **Reinstate clears void fields (known trade-off)** — `ReinstateAsync` nulls out `VoidedAt`/`VoidedBy`, losing the record that a void ever happened. Accepted for v1 (small admin team, camp context). If full void history is ever needed, add `ReinstatedAt`/`ReinstatedBy` columns to `Transaction` and a new migration — do not change the null-out behavior without that migration in place.
- **Pre-scripted board** — block hit and mini-game spinner are NOT random. Pre-scripted by admin before camp.
- **Auth** — BCrypt/cookie for v1 (small known user base, camp reliability). Auth0 deferred to v2 chapter platform when member self-service matters.
- **Login flow** — Blazor Server can't set cookies from a SignalR circuit. Login uses a native HTML form POST to `/account/login` (minimal API endpoint), which issues the cookie and redirects.

---

## Schema

| Concept | Tables |
|---|---|
| Event scoping | `EventType`, `Event`, `Theme` |
| Feature flags | `Capability`, `EventCapability` |
| Activities | `ActivityTypeCategory`, `ActivityType`, `Activity` |
| Competition | `CurrencyType`, `Group`, `Transaction`, `BoardSpace`, `GroupBoardPos`, `ScriptedBlockHit`, `ScriptedMiniGame` |
| Awards | `AwardType`, `CamperAward` |
| Auth/RBAC | `UserRole`, `Authority`, `UserRoleAuthorityLink`, `UserAuthorityLink`, `User` |

**Column convention:** `Name` (short label) + `Description` (human-readable) + `SystemName` (stable code identifier, what C# enums map to) on all reference/catalog tables.

**Enum naming (Option C):** Entity class names match table names (`CurrencyType`, `UserRole`). Enum names reflect code intent (`Currency`, `Role`). Full mapping: `CurrencyType`→`Currency`, `AwardType`→`AwardKind`, `UserRole`→`Role`, `Capability`→`Feature`, `Authority`→`Permission`.

**Seed IDs:** Stable hardcoded GUIDs in `SeedService.Id` static class. CCN 2026 EventId = `00000009-0009-0009-0009-000000000001`.

**Auth evaluation:** Load role authorities + user additions at login → cache in session claims. Zero DB cost on subsequent checks.

---

## Branching & Versioning

```
main              — stable releases. Protected. Tagged at each version.
archive/2021      — frozen 2021 Python/React capstone
dev               — integration branch (merge features here first)
feature/N-name    — feature branches off dev
```

**Release flow:** `feature/*` → PR to `dev` → PR to `main` → tag (`v0.1.0`, `v0.2.0`, etc.)  
**Hotfix flow:** branch off the tag → fix → tag new patch → merge back to main and dev

---

## Key Files

| File | Purpose |
|---|---|
| `REQUIREMENTS.md` | Full functional spec, animation spec, infrastructure, decisions |
| `docs/superpowers/specs/2026-04-28-schema-redesign.md` | Schema redesign rationale and full table definitions |
| `mockup/ccn-mockup-v2.jsx` | React prototype — primary visual reference for UI |
| `mockup/assets/ccn-logo-2026.png` | Camp logo |
| `CampClotNot/` | Blazor Server project |
| `CampClotNot/appsettings.Development.json` | Local DB connection string + seed credentials (gitignored) |
| `CampClotNot/Services/SeedService.cs` | Startup seed — all reference data + CCN 2026 event |

---

## Local Dev Setup

- **DB:** PostgreSQL local, database `hbda_dev`
- **Connection string:** `CampClotNot/appsettings.Development.json` (gitignored — set password and seed credentials locally)
- **Run migrations:** `dotnet ef database update` from `CampClotNot/`
- **Start app:** `dotnet run` from `CampClotNot/` or F5 in Visual Studio
- **Login (dev):** `tyler@hbda.local` / `DevAdmin1!` (seeded from appsettings.Development.json)
- **Mockup preview:** `cd mockup/preview && npm run dev` → http://localhost:5173
