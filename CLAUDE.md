# Camp Clot Not — Claude Code Briefing

## What This Is
A Blazor Server (.NET 8) web app for Camp Clot Not (CCN), a camp for kids with bleeding disorders run by HBDA (Alabama chapter). The 2026 theme is Super Mario Party. The platform is being built to run camp scoring/competition for June 20-25, 2026, with a longer-term goal of replacing the chapter's Yapp subscription (~$1,600/year) for all chapter events.

**Primary spec:** `REQUIREMENTS.md`  
**Schema redesign spec:** `docs/superpowers/specs/2026-04-28-schema-redesign.md`

---

## Current State (as of 2026-05-16)

**Active branch:** `feature/96-mini-game-spinner`  
**Released to dev:** v0.3.1 — UI Overhaul (PR #95, merged)

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

**v0.3.0 — Done (Board & Block Hit):**
- SVG board with winding snake path, BoardComponent, BoardService
- Pre-scripted block hit — admin trigger via `/admin/board`, animation on `/board/display` via SignalR
- ThemeService + CSS variables foundation

**v0.3.1 — Done (UI Overhaul, PR #95):**
- Neo-brutalist redesign — cream bg (`#F2ECD8`), black borders, shadow offsets, rank rows
- ThemeService extended with flat palette CSS vars
- MainLayout: black header band, light MudBlazor theme
- Dashboard: rank rows replacing card grid; score pills with ccn-coin.png / ccn-star.png
- Groups confirmed: Mini Marios (MM, red), Blue Shell Bandits (BB, blue), Mushroom Militia (MU, green), Luma Legends (LL, purple) — 4 groups final

**v0.4.0 — Done (Mini-Game Spinner, feature/96):**
- `LiveHub` (renamed from `CampHub`) at `/livehub` — adds `MiniGameSpinTriggered`, `MiniGameSpinRevealed`, `MiniGameSpinReset` hub events
- `MiniGameService` — GetActivities, GetScripts, Upsert, TriggerSpin, Reveal, ResetDay
- `/admin/games` — combined Game Admin page (absorbs old `/admin/board`); tabs: Board Spaces, Block Hit Scripts, Mini-Game Scripts, Reset
- `/minigames` — admin trigger page; day selector, Start Spin → Reveal Result → log coins/stars
- `/minigames/display` — projector display page (Admin only); yellow cells → hand cycles green → lands green + pulses
- `/board/display` — renamed from `/display` (file: `BoardDisplay.razor`)
- Activities nav is now a dropdown: Board Game (`/board`) + Mini-Game (`/minigames`)
- `LoginLayout.razor` — bare layout for login page (no nav, no MudBlazor providers)
- Login page redesigned: `LoginLayout`, `position:fixed` centering, ccn-logo-2026.png (background made transparent via Python Pillow flood fill)
- `RedirectToLogin.razor` — fixed `NavigationException` with `IHttpContextAccessor`
- Two new MinuteToWinIt activities seeded: "Mushroom Kingdom Trivia Showdown", "Yoshi Egg Rescue Relay"
- `gh` CLI installed at `$env:LOCALAPPDATA\Programs\gh\gh.exe` (user PATH)

**Next:** v0.5.0 — Camp Info Hub + PWA

---

## Pitfalls to Avoid (Lessons from v0.4.0)

**1. Never use PowerShell for source file text replacement.**  
PowerShell 5.1's `Get-Content` reads UTF-8-without-BOM files using the system code page (CP1252 on US Windows), corrupting multi-byte characters like emoji. Always use the **Edit tool** for replacements in source files. If you must use PowerShell, use `[System.IO.File]::ReadAllText(path, [System.Text.Encoding]::UTF8)` and `[System.IO.File]::WriteAllText(path, content, [System.Text.Encoding]::UTF8)` explicitly.

**2. Hub rename requires two separate changes.**  
Renaming a SignalR hub class (e.g., `CampHub` → `LiveHub`) and renaming its route (e.g., `/camphub` → `/livehub`) are independent changes. A `replace_all` on the class name will not touch the route string literal. Check and update both explicitly, and grep for both the old class name AND the old route string before closing.

**3. Login page layout — use `LoginLayout`, not `MainLayout`.**  
The login page must use `LoginLayout.razor` (bare layout: ThemeHead only, no nav). Using `MainLayout` puts a 72-80px nav header above the page content, which breaks any `height:100vh` centering attempt. `LoginLayout` must NOT include `MudDialogProvider` or `MudSnackbarProvider` — they render DOM elements that can interfere with positioning.

**4. Centering in Blazor Server — use `position:fixed` on the element itself.**  
Do not rely on `height:100vh` + flex centering on a wrapper inside a Blazor layout. Blazor's component tree and MudBlazor providers introduce DOM elements that break this. For the login card: `position:fixed;top:50%;left:50%;transform:translate(-50%,-50%)` directly on the card element is the only reliable approach. For the logo above it, use a separate `position:fixed` element with `bottom:calc(50% + Npx)`.

**5. `RedirectToLogin` — use `IHttpContextAccessor`, not `Nav.NavigateTo(forceLoad:true)`.**  
`Nav.NavigateTo(forceLoad:true)` during Blazor Server prerendering throws `NavigationException`. The dev exception page catches this before Blazor can process it as a redirect. Fix: check `IHttpContextAccessor.HttpContext` — if non-null and response not started, use `Response.Redirect("/login")` directly. Fall back to `Nav.NavigateTo` for the interactive circuit (where `HttpContext` is null). `IHttpContextAccessor` is already registered in `Program.cs`.

**6. Read the request before touching anything.**  
If asked to "make cells more visible against the cream background," that means style the cells — not replace the entire page background. Do not make sweeping changes beyond the stated scope.

**7. Image background removal requires flood fill, not crop.**  
Python Pillow `img.getbbox()` + `crop()` only removes surrounding whitespace — it does not make the background transparent. Use a flood fill from all four edges with a colour tolerance to make the actual background pixels transparent. See the Python script used for `ccn-logo-2026.png` for the pattern.

---

## UI Design Direction — Mario Party Reference

Reviewed actual Mario Party Superstars / Mario Party 9 screenshots 2026-05-07. Reference images at `References/` (local only, not committed).

**The correct aesthetic:**
- **Background:** Cream (`#F2ECD8`) dotted pattern for all main app pages. Deep purple-violet only for the board/display projector pages.
- **Leaderboard layout:** Full-width horizontal ROWS, not a grid of cards.
- **Portraits:** Small square with colored border — not a circle.
- **Panels:** Opaque solid-colored rectangles with thick black borders and 3-4px offset box shadow (neo-brutalist). No blur, no translucency.
- **Typography:** Fredoka One for headings/labels. Nunito for body.
- **Score display:** `ccn-coin.png` / `ccn-star.png` image assets — not emoji.

---

## Architecture — Critical Points

- **Railway.app hosting** — PORT env var is injected, never hardcode. postgres:// URI converted in Program.cs. HTTPS terminated externally — no ForceHttpsRedirection in prod.
- **Blazor Server + SignalR** — projector displays receive real-time updates via `LiveHub` at `/livehub`. Block hit and mini-game animations MUST play on the projector via SignalR broadcast triggered from admin tablet. Hub events defined in `Hubs/LiveHub.cs`.
- **No Flask middleware in v1** — service → repository → EF Core → PostgreSQL.
- **Append-only transactions** — coins/stars never deleted, only voided. Totals always computed from non-voided transactions, never stored.
- **Reinstate clears void fields (known trade-off)** — `ReinstateAsync` nulls out `VoidedAt`/`VoidedBy`. If full void history is ever needed, add `ReinstatedAt`/`ReinstatedBy` to `Transaction` and a new migration.
- **Pre-scripted games** — block hit and mini-game spinner are NOT random. Pre-scripted by admin before camp via `/admin/games`.
- **Auth** — BCrypt/cookie for v1. Login uses native HTML form POST to `/account/login` (cookie can't be set from SignalR circuit).
- **3-way split pattern** — all interactive game features follow: trigger page (admin tablet) → projector display page (Admin only, opened in separate tab) → admin scripting page under `/admin/games`. Board game: `/board`, `/board/display`. Mini-game: `/minigames`, `/minigames/display`.

---

## Groups (CCN 2026 — Confirmed)

| ID | Name | Short | Color | Logo |
|---|---|---|---|---|
| Group1 | Mini Marios | MM | #E74C3C (red) | mini-marios-logo.png |
| Group2 | Blue Shell Bandits | BB | #2980B9 (blue) | blue-shell-bandits-logo.png |
| Group3 | Mushroom Militia | MU | #27AE60 (green) | mushroom-militia-logo.png |
| Group4 | Luma Legends | LL | #8E44AD (purple) | luma-legends-logo.png |

Groups 5 & 6 removed from seed. SeedGroupsAsync upserts by ID and purges stale entries on restart.

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

**Seed IDs:** Stable hardcoded GUIDs in `SeedService.Id`. CCN 2026 EventId = `00000009-0009-0009-0009-000000000001`.

**SpaceType is NOT an enum** — board space type comes from `ActivityTypeCategory.SystemName` via the `BoardSpace → Activity → ActivityType → Category` chain.

---

## Branching & Versioning

```
main              — stable releases. Protected. Tagged at each version.
dev               — integration branch (merge features here first)
feature/N-name    — feature branches off dev (N = GitHub issue number, open issue first)
```

**Release flow:** `feature/*` → PR to `dev` → PR to `main` → tag  
**Issue-first rule:** Open a GitHub issue before creating a branch. Branch name must use the issue number GitHub assigns.  
**`gh` CLI:** Installed at `$env:LOCALAPPDATA\Programs\gh\gh.exe`. Use PowerShell (not Bash) to invoke it.

---

## Key Files

| File | Purpose |
|---|---|
| `REQUIREMENTS.md` | Full functional spec, animation spec, infrastructure, decisions |
| `CampClotNot/Hubs/LiveHub.cs` | SignalR hub at `/livehub` — all real-time events |
| `CampClotNot/Services/BoardService.cs` | Board logic + block hit SignalR broadcasts |
| `CampClotNot/Services/MiniGameService.cs` | Mini-game logic + spinner SignalR broadcasts |
| `CampClotNot/Services/SeedService.cs` | Startup seed — all reference data + CCN 2026 event |
| `CampClotNot/Services/ThemeService.cs` | CSS variable tokens — `ThemeConfig.CssVariables` injected via `ThemeHead.razor` |
| `CampClotNot/Shared/ThemeHead.razor` | Injects CSS vars into `:root`, defines body styles, shared animations |
| `CampClotNot/Shared/LoginLayout.razor` | Bare layout for login page — ThemeHead only, no nav |
| `CampClotNot/Pages/Login.razor` | Login page — uses `LoginLayout`, `position:fixed` centering |
| `CampClotNot/Pages/Board.razor` | Board game trigger — Admin + Staff |
| `CampClotNot/Pages/BoardDisplay.razor` | Board projector display at `/board/display` — Admin + Display |
| `CampClotNot/Pages/MiniGames.razor` | Mini-game trigger at `/minigames` — Admin only |
| `CampClotNot/Pages/MiniGamesDisplay.razor` | Mini-game projector at `/minigames/display` — Admin only |
| `CampClotNot/Pages/Admin/Games.razor` | Combined game admin at `/admin/games` |
| `CampClotNot/appsettings.Development.json` | Local DB + seed credentials (gitignored) |

---

## Local Dev Setup

- **DB:** PostgreSQL local, database `hbda_dev`
- **Connection string:** `CampClotNot/appsettings.Development.json` (gitignored)
- **Run migrations:** `dotnet ef database update` from `CampClotNot/`
- **Start app:** `dotnet run` from `CampClotNot/` or F5 in Visual Studio
- **Login (dev):** `tyler@hbda.local` / `DevAdmin1!` (seeded from appsettings.Development.json)
- **gh CLI:** `& "$env:LOCALAPPDATA\Programs\gh\gh.exe" <command>` from PowerShell
