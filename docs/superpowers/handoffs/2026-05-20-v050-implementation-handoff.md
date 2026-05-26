# Claude Code Handoff — v0.5.0 Camp Info Hub + PWA (Implementation)

**Date:** 2026-05-20
**Prepared by:** Tyler Blair (via brainstorming session with Claude Code)
**Repo:** https://github.com/trblair16/camp-clot-not
**Design spec:** `docs/superpowers/specs/2026-05-19-camp-info-hub-pwa-design.md`
**Full project briefing:** `CLAUDE.md` (read this first)

---

## What This Project Is

A Blazor Server (.NET 8) web app for Camp Clot Not (CCN), a summer camp for kids with bleeding disorders run by HBDA (Alabama chapter). Theme: Super Mario Party 2026. Camp runs June 20–25, 2026.

Long-term goal: replace the chapter's Yapp subscription (~$1,600/yr) for all chapter events. The camp app is the pilot.

---

## Current State

**v0.4.0 is merged to `main` and `dev`.** Tag it if not already done:
```powershell
& "$env:LOCALAPPDATA\Programs\gh\gh.exe" release view v0.4.0 2>$null || git tag v0.4.0 && git push origin v0.4.0
```

**Local working directory is on `feature/96-mini-game-spinner`** (old branch, already merged). There are two uncommitted files sitting in the working directory that must travel to the new branch:
- `.gitignore` — modified (adds `.claude/settings.local.json` and `.superpowers/` entries)
- `docs/superpowers/specs/2026-05-19-camp-info-hub-pwa-design.md` — new file (untracked)

These files will carry over automatically when you switch branches and create the new one.

---

## Your First Steps (Before Writing Any Code)

### 1. Get to a clean starting point
```powershell
git checkout dev
git pull origin dev
git status   # should be clean except for the two files above
```

### 2. Open a GitHub issue for v0.5.0
```powershell
& "$env:LOCALAPPDATA\Programs\gh\gh.exe" issue create `
  --title "v0.5.0 — Camp Info Hub + PWA" `
  --body "Implements the Camp Info Hub (Schedule, Announcements, Staff Directory, Info Pages) and PWA installability. See docs/superpowers/specs/2026-05-19-camp-info-hub-pwa-design.md for the full design spec." `
  --label "enhancement"
```
Note the issue number GitHub assigns. You need it for the branch name.

### 3. Create and checkout the feature branch
```powershell
git checkout -b feature/N-camp-info-hub   # replace N with the actual issue number
```

### 4. Commit the pending files
```powershell
git add .gitignore docs/superpowers/specs/2026-05-19-camp-info-hub-pwa-design.md
git commit -m "docs: v0.5.0 design spec + gitignore updates"
```

### 5. Run the local DB cleanup SQL
Before any migrations, clean up the stale Group 5 & 6 rows from `hbda_dev`:
```sql
DELETE FROM group_board_pos WHERE group_id IN (
    '0000000a-000a-000a-000a-000000000005',
    '0000000a-000a-000a-000a-000000000006'
);
DELETE FROM scripted_block_hits WHERE group_id IN (
    '0000000a-000a-000a-000a-000000000005',
    '0000000a-000a-000a-000a-000000000006'
);
DELETE FROM groups WHERE group_id IN (
    '0000000a-000a-000a-000a-000000000005',
    '0000000a-000a-000a-000a-000000000006'
);
```
Run this via `psql hbda_dev` or your preferred PostgreSQL client.

---

## What to Build

**Read the full design spec first:** `docs/superpowers/specs/2026-05-19-camp-info-hub-pwa-design.md`

Follow the implementation order in spec Section 9 exactly:

1. **Group seed cleanup** — remove `Id.Group5`/`Id.Group6` constants + stale cleanup `foreach` loop from `SeedService.cs`
2. **Entity classes** — 5 new files in `CampClotNot/Data/Entities/`
3. **AppDbContext** — add 5 DbSets + `OnModelCreating` config (composite PK, unique index)
4. **Migrations** — 4 in order: InfoPage → StaffMember → Announcement → ScheduleEvent (includes ScheduleEventGroup bridge)
5. **SeedService** — add InfoPage stable IDs (`0000000e-000e-*` prefix) + `SeedInfoPagesAsync()`
6. **`dotnet ef database update`** from `CampClotNot/` — apply all migrations
7. **Services** — 4 concrete service classes + `Program.cs` `AddScoped` registrations
8. **Markdig NuGet** — `dotnet add package Markdig` from `CampClotNot/`
9. **`HubSubNav.razor`** — shared sub-nav component
10. **Hub pages** — `Hub.razor` (redirect) + 4 sub-pages in `CampClotNot/Pages/Hub/`
11. **`AppNav.razor`** — Hub tab in desktop header + mobile bottom bar
12. **Manual test** — all Hub sections, all roles, Display redirect to `/`
13. **PWA** — manifest, `_Host.cshtml`, service worker, offline page, `ConnectionIndicator.razor`, `InstallPromptBanner.razor`
14. **Manual test** — PWA installability

---

## Architecture You Must Know

**Stack:** Blazor Server (.NET 8) + EF Core + PostgreSQL + MudBlazor + SignalR (`LiveHub` at `/livehub`)

**Service pattern:** Concrete class (no interfaces) → `IDbContextFactory<AppDbContext>` via primary constructor → each method creates and disposes its own context. Follow `MiniGameService.cs` exactly.

**Auth:** Cookie auth. Login via native POST to `/account/login`. No Blazor circuits involved.

**Redirect pattern:** For Display role redirect away from `/hub`, use `IHttpContextAccessor` — the same pattern as `RedirectToLogin.razor`. Never use `Nav.NavigateTo(forceLoad:true)` during prerendering.

**Layouts:** All Hub pages use `MainLayout` (standard). Do NOT create a new HubLayout.

**CSS tokens:** Always `var(--color-primary)`, `var(--black)`, `var(--bg-base)`, `var(--panel-bg)` etc. from `ThemeHead.razor`. Never hardcode hex values that duplicate a token.

**Markdown rendering:** `@((MarkupString)Markdig.Markdown.ToHtml(page.Body))` — add Markdig NuGet first.

**Nav active state:** Follow `IsActive(href)` pattern in `AppNav.razor`. Hub tab active when any `/hub/*` route is active.

---

## Key Design Decisions (Already Made — Do Not Revisit)

| Decision | Choice |
|---|---|
| Sub-nav architecture | Page-per-route + shared `HubSubNav.razor` (not a Blazor layout) |
| Hub root `/hub` | Redirects to `/hub/schedule` |
| Service pattern | Concrete classes, no interfaces |
| Response caching | Skip entirely — post-go-live concern |
| Dashboard pinned banner | DO NOT TOUCH `Dashboard.razor` in v0.5.0 |
| Display role | Redirect to `/` (not to login) |
| `ScheduleEvent.MaxCapacity` | Nullable int, stored silently — no UI |
| Admin Hub designer | Post-go-live — not in scope |

---

## Critical Pitfalls (from CLAUDE.md — Do Not Skip)

1. **Never use PowerShell `Set-Content` on source files.** It corrupts emoji. Use the **Edit tool** for all source file changes. If you must use PowerShell for file I/O, use `[System.IO.File]::ReadAllText/WriteAllText` with explicit `UTF8` encoding.

2. **When renaming anything with two parts (class name + route string), update both explicitly.** `replace_all` on the class name won't touch a route string literal. Grep for both before closing.

3. **`height:100vh` inside a Blazor layout does not reliably center content.** Use `position:fixed;top:50%;left:50%;transform:translate(-50%,-50%)` on the element directly.

4. **`RedirectToLogin` uses `IHttpContextAccessor`.** Do not use `Nav.NavigateTo(forceLoad:true)` during prerendering — it throws `NavigationException`.

5. **Read the request before touching anything.** Do not make sweeping changes beyond the stated scope.

6. **Do not add `MudDialogProvider` or `MudSnackbarProvider` to `LoginLayout`.**

---

## Scope Boundaries

**In scope for v0.5.0:**
- 5 new entities + 4 EF Core migrations
- Group seed cleanup (dead constants + SQL)
- 4 service classes
- HubSubNav + 5 Hub pages (Hub redirect + Schedule + Announcements + Staff + Info)
- AppNav Hub tab
- PWA (manifest, service worker, offline.html, ConnectionIndicator, InstallPromptBanner)
- Markdig NuGet

**Explicitly NOT in scope (do not build):**
- Changes to `Dashboard.razor`
- Response caching / `IMemoryCache`
- Guest attendee access / event code login
- Session registration / breakout sign-up UI
- `/admin/hub` designer page
- Push notifications

---

## Future Entities (Noted, Not Built)

Design with these in mind — `ScheduleEvent.MaxCapacity` already exists for them:

```
GuestAttendee       — GuestId, EventId, FirstName, LastName, CreatedAt, SessionCookieToken
SessionRegistration — RegistrationId, ScheduleEventId, GuestAttendeeId?, FirstName, LastName, RegisteredAt
```

The `GetLatestPinnedAsync()` method on `AnnouncementService` is a stub for the future Dashboard banner — implement the method but do not call it from Dashboard.

---

## Local Dev

- **DB:** PostgreSQL local, database `hbda_dev`
- **Migrations:** `dotnet ef database update` from `CampClotNot/`
- **Run app:** `dotnet run` from `CampClotNot/`
- **Login:** `tyler@hbda.local` / `DevAdmin1!` (seeded from `appsettings.Development.json`)
- **gh CLI:** `& "$env:LOCALAPPDATA\Programs\gh\gh.exe" <args>` — PowerShell only, not Bash

---

## Key Files

| File | Purpose |
|---|---|
| `docs/superpowers/specs/2026-05-19-camp-info-hub-pwa-design.md` | Full v0.5.0 design spec — primary reference |
| `CLAUDE.md` | Architecture notes, pitfalls, confirmed group list |
| `REQUIREMENTS.md` §8, §8.9 | Original requirements (spec supersedes where they conflict) |
| `CampClotNot/Data/AppDbContext.cs` | Add new DbSets here |
| `CampClotNot/Services/SeedService.cs` | Add stable IDs + seed method |
| `CampClotNot/Services/MiniGameService.cs` | Reference for service pattern |
| `CampClotNot/Shared/AppNav.razor` | Add Hub tab — follow existing dropdown/tab pattern |
| `CampClotNot/Shared/ThemeHead.razor` | CSS tokens — never hardcode duplicates |
| `CampClotNot/Pages/Admin/Games.razor` | Reference for tabbed admin page + MudDialog pattern |

---

## Branching

- Branch off `dev`: `feature/N-camp-info-hub` (N = GitHub issue number)
- PR: `feature/N-camp-info-hub` → `dev`
- Do not push to `main` directly
- Tag `v0.4.0` on main if not already done before starting

---

## PWA Icons (Manual Step — Tyler Does This)

Generate from CCN logo PNG via PWABuilder or RealFaviconGenerator. Three files needed:
- `wwwroot/icons/icon-192.png`
- `wwwroot/icons/icon-512.png`
- `wwwroot/icons/icon-512-maskable.png`

Leave placeholder files with a clear comment. Tyler will replace them before camp.

---

## Permissions

This project has `.claude/settings.local.json` with `bypassPermissions` mode — no permission prompts will appear. You can work fully autonomously.
