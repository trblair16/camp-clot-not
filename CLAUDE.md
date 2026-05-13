# Camp Clot Not — Claude Code Briefing

## What This Is
A Blazor Server (.NET 8) web app for Camp Clot Not (CCN), a camp for kids with bleeding disorders run by HBDA (Alabama chapter). The 2026 theme is Super Mario Party. The platform is being built to run camp scoring/competition for June 20-25, 2026, with a longer-term goal of replacing the chapter's Yapp subscription (~$1,600/year) for all chapter events.

**Primary spec:** `REQUIREMENTS.md`  
**Schema redesign spec:** `docs/superpowers/specs/2026-04-28-schema-redesign.md`

---

## Current State (as of 2026-05-12)

**Active branch:** `dev` (v0.3.1 PR #95 merged from `feature/94-ui-overhaul`)  
**Released:** v0.3.1 — UI Overhaul + Block Hit Animation + Real Groups (PR #95, pending merge to main + tag)

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
- Seed credentials: `Seed:AdminEmail` / `Seed:AdminPassword` in `appsettings.Development.json`

**v0.3.0 — Done (Board & Block Hit):**
- SVG board with rectangular loop path, `BoardComponent`, `BoardService`
- Pre-scripted block hit — admin trigger via `/board`, step-by-step token animation on `/display` via SignalR
- ThemeService + CSS variables foundation
- Display page: board (70%) + leaderboard sidebar (30%)

**v0.3.1 — Done (UI Overhaul, issue #94):**
- Neo-brutalist redesign — cream bg, black borders, box-shadow offsets, rank rows replacing card grid
- Dashboard: full-width rank rows, circle logo avatars, neo-brutalist score pills
- Block hit animation fully working on `/display` (see pitfalls below for the root cause journey)
- Dice block cycling 1–12 on projector; admin triggers from `/board`
- `/board` has "📺 Open Display" button to open projector in new tab
- Display page: logo+tagline header, dashboard-style leaderboard, vertically centered
- **4 confirmed CCN 2026 groups seeded** (replaces 6 placeholders):
  - Group1: Mini Marios, MM, #E74C3C (youngest boys)
  - Group2: Blue Shell Bandits, BB, #2980B9 (middle boys)
  - Group3: Mushroom Militia, MU, #27AE60 (older boys)
  - Group4: Luma Legends, LL, #8E44AD (girl group)
- Group logos: white bg removed + transparent borders trimmed via ImageMagick, resized ~230KB
- Logo paths seeded into `TokenAssetPath` — no manual upload required
- Circle logo avatars on: dashboard standings, dashboard quick actions, projector overlay, board SVG tokens

**Up next — v0.4.0 (~June 7):**
- Evening game spinner (mini-game picker with animated pointing hand)
- Admin config UI: board space management, scripted block hit/mini-game sequences, camper awards, score lock
- PIN-protected display URL (`/display?pin=XXXX`) instead of full auth for projector
- SignalR reconnect indicator (never show blank screen)

**v0.5.0 (~June 7):**
- Camp Info Hub: announcements, schedule, staff directory, info pages (markdown)
- PWA: manifest + service worker + install flow for staff phones

---

## Known Open Decisions

- [ ] Board space count (pending activity list from Katelyn/Vicki)
- [ ] ActivityType seed data (same dependency)
- [ ] Stars: staff judgment only, or can groups spend coins?
- [ ] Coin/star weighting for final standings tiebreak
- [ ] Board visible all week or only at gatherings?
- [ ] Session timeout behavior
- [ ] How are staff accounts created pre-camp?
- [ ] Score lock flow / winner declaration

---

## UI Design Direction — Mario Party Reference

Reviewed actual Mario Party Superstars screenshots 2026-05-07. Reference images at `References/` (local only, not committed).

**Target aesthetic:**
- **Background (display/projector):** Dark — dark navy currently used; full purple-violet (#3d0066) deferred post-camp
- **Leaderboard:** Full-width horizontal rows. #1 row = bright solid-color band.
- **Avatars:** Circle with logo image. (Note: REQUIREMENTS.md §2.4 specifies squares — we deviated to circles to match the group token shape and it looks better. Revisit if needed.)
- **Buttons/panels:** Opaque with thick black/colored borders. No blur, no translucency.
- **Score display:** Coin/star image + large number with neo-brutalist border.

---

## Architecture — Critical Points

- **Railway.app hosting** — PORT env var injected, never hardcode. `postgres://` URI converted in Program.cs. HTTPS terminated externally — no `ForceHttpsRedirection` in prod.
- **Blazor Server + SignalR** — projector display (`/display`) receives real-time updates via `CampHub` at `/camphub`. Block hit animation plays on projector via SignalR broadcast triggered from admin tablet.
- **No Flask middleware in v1** — service → repository → EF Core → PostgreSQL.
- **Append-only transactions** — coins/stars never deleted, only voided. Totals always computed from non-voided transactions, never stored.
- **Reinstate clears void fields (known trade-off)** — `ReinstateAsync` nulls out `VoidedAt`/`VoidedBy`. Accepted for v1. If full void history needed, add `ReinstatedAt`/`ReinstatedBy` columns and a migration.
- **Pre-scripted board** — block hit and mini-game spinner are NOT random. Pre-scripted by admin before camp.
- **Auth** — BCrypt/cookie for v1. Auth0 deferred to v2.
- **Login flow** — Blazor Server can't set cookies from a SignalR circuit. Login uses native HTML form POST to `/account/login` (minimal API endpoint).
- **`AddDbContextFactory` pattern** — required for Blazor Server to avoid concurrent EF Core command errors on the same circuit. Always use `factory.CreateDbContext()` + `using var db`, never inject `AppDbContext` directly into components or scoped services that multiple circuits share.

---

## Pitfalls & Key Findings

### Blazor Server + SignalR Hub Setup
**CRITICAL — Always set up `HubConnection` in `OnAfterRenderAsync(firstRender: true)`, never in `OnInitializedAsync`.**

During Blazor Server prerendering, `OnInitializedAsync` runs server-side without an interactive circuit. The `HubConnectionBuilder` creates a server-to-server WebSocket back to itself — this connection appears to work briefly (explaining why early events like `BlockHitTriggered` fired) but silently drops before sustained event streams like `TokenMoveStep` (400ms × N steps) arrive. This was the root cause of 3+ hours of debugging the token animation.

```csharp
// WRONG — runs during prerendering, creates dead connection
protected override async Task OnInitializedAsync()
{
    _hub = new HubConnectionBuilder()...Build();
    await _hub.StartAsync();
}

// RIGHT — only runs on the interactive circuit
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender) return;
    _hub = new HubConnectionBuilder()...Build();
    // register handlers...
    await _hub.StartAsync();
}
```

This applies to **every Blazor Server page/component** that creates a `HubConnection`. Currently fixed in `Board.razor` and `Display.razor`.

### Blazor Render Batching for Animations
When `StateHasChanged` is called rapidly from hub callbacks, Blazor may batch multiple renders into one DOM patch — causing all animation steps to coalesce into a single jump (the "zip to destination" bug). Two fixes in place:
1. SignalR's .NET client awaits each hub callback before processing the next, and `Task.Delay(600)` between server sends gives Blazor time to dispatch each render separately.
2. For the first step (overlay dismissal), a client-side `await Task.Delay(900)` in the Display handler separates the overlay dismissal from the first token move — the user sees the board briefly before movement begins.

### CSS `transform: translate()` on SVG Elements
CSS transitions between two `translate()` values always animate in a **straight line in 2D space**, not along the board path. For adjacent board spaces this is acceptable (each hop is a short straight-line segment along the perimeter). For non-adjacent jumps (e.g., Blazor coalescing all steps into one render), the token cuts diagonally through the board interior. Always ensure one render per step.

### SVG `<text>` in Blazor Razor
Razor's parser treats `<text>` as its own pseudo-element and rejects attributes on it. Always wrap SVG `<text>` elements in a `<g>`:
```razor
<g><text x="0" y="1" text-anchor="middle" ...>@content</text></g>
```
This pattern is used in `BoardComponent.razor` for both space icons and token fallback labels.

### SVG `<image>` with Circular Clip
To display a logo inside a circular SVG token, use CSS `clip-path: circle()` on the `<image>` element — no `<clipPath>` / `<defs>` needed, and it works in the element's local coordinate space:
```razor
<image href="@group.TokenAssetPath" x="-11" y="-11" width="22" height="22"
       style="clip-path:circle(11px at center)" preserveAspectRatio="xMidYMid meet" />
```
Note: `<clipPath>` with `userSpaceOnUse` (default) uses the root SVG coordinate system, not the element's local transform — don't use it for elements inside translated `<g>` tokens.

### Image Processing (ImageMagick)
ImageMagick 7 is installed at `C:\Program Files\ImageMagick-7.1.1-Q16-HDRI\`. Use `magick` (not `convert`) in Git Bash.

Remove white backgrounds (floodfill from all 4 corners, 15% fuzz):
```bash
magick input.png -alpha set -fuzz 15% \
  -fill none \
  -draw "color 0,0 floodfill" \
  -draw "color 0,%[fx:h-1] floodfill" \
  -draw "color %[fx:w-1],0 floodfill" \
  -draw "color %[fx:w-1],%[fx:h-1] floodfill" \
  output.png
```

Trim transparent borders and resize:
```bash
magick input.png -trim +repage -resize 400x400\> output.png
```

Always trim AFTER removing backgrounds — the transparent border from the old white bg will otherwise make logos appear tiny in circle containers.

### SeedService — Removing Groups with Dependents
When removing seeded groups, always delete dependent rows before the group itself or the FK constraint will fail. Order: `GroupBoardPositions` → `ScriptedBlockHits` → `Groups`. Also check `SeedGroupBoardPositionsAsync` — it has a hardcoded group ID array that must be updated in sync.

### Medals Array Sizing
The medals array in `Display.razor` and `Dashboard.razor` must match the actual group count. With 4 groups, the array must have exactly 4 entries. An undersized array throws `IndexOutOfRangeException` at render time (silent crash in Blazor Server — shows as "Reload" error page).

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

**Column convention:** `Name` + `Description` + `SystemName` on all reference/catalog tables.

**Enum naming (Option C):** `CurrencyType`→`Currency`, `AwardType`→`AwardKind`, `UserRole`→`Role`, `Capability`→`Feature`, `Authority`→`Permission`.

**Seed IDs:** Stable hardcoded GUIDs in `SeedService.Id`. CCN 2026 EventId = `00000009-0009-0009-0009-000000000001`. Group IDs: `0000000a-000a-000a-000a-00000000000{1-4}`.

**Auth evaluation:** Role authorities + user additions loaded at login → cached in session claims. Zero DB cost on subsequent checks.

---

## Branching & Versioning

```
main              — stable releases. Protected. Tagged at each version.
archive/2021      — frozen 2021 Python/React capstone
dev               — integration branch (Railway staging deploys here)
feature/N-name    — feature branches off dev
```

**Release flow:** `feature/*` → PR to `dev` → PR to `main` → tag  
**Hotfix flow:** branch off the tag → fix → tag new patch → merge back to main and dev

---

## Key Files

| File | Purpose |
|---|---|
| `REQUIREMENTS.md` | Full functional spec, animation spec, infrastructure, decisions |
| `docs/superpowers/specs/2026-04-28-schema-redesign.md` | Schema redesign rationale and full table definitions |
| `mockup/ccn-mockup-v2.jsx` | React prototype — primary visual reference for UI |
| `CampClotNot/` | Blazor Server project |
| `CampClotNot/appsettings.Development.json` | Local DB connection string + seed credentials (gitignored) |
| `CampClotNot/Services/SeedService.cs` | Startup seed — reference data, groups, board spaces, admin user |
| `CampClotNot/Services/BoardService.cs` | Block hit phases (Phase1/2/3), RunStepsAsync, board positions |
| `CampClotNot/Hubs/CampHub.cs` | SignalR hub at `/camphub` — broadcasts all real-time events |
| `CampClotNot/Pages/Display.razor` | Projector page — hub in OnAfterRenderAsync, token animation |
| `CampClotNot/Pages/Board.razor` | Admin board page — 3-phase block hit trigger UI |
| `CampClotNot/Shared/BoardComponent.razor` | Reusable SVG board — tokens, spaces, loop path |
| `CampClotNot/wwwroot/img/` | Group logos (PNG, transparent bg, ~230KB each) |
| `CampClotNot/wwwroot/js/board.js` | Client-side board utilities (currently minimal) |

---

## Local Dev Setup

- **DB:** PostgreSQL local, database `hbda_dev`, user `postgres`, password `postgres`
- **Connection string:** `CampClotNot/appsettings.Development.json` (gitignored)
- **Run migrations:** `dotnet ef database update` from `CampClotNot/`
- **Start app:** `dotnet run` from `CampClotNot/`
- **Login (dev):** `tyler@hbda.local` / `DevAdmin1!`
- **Ports:** https://localhost:63533 / http://localhost:63534
- **Kill running instance:** `Get-Process dotnet | Stop-Process -Force` (PowerShell)
- **Mockup preview:** `cd mockup/preview && npm run dev` → http://localhost:5173
