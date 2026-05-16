# Nav Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current MainLayout nav with a single `AppNav` component that renders a mobile bottom bar (≤768px) and desktop top bar (>768px) via CSS media queries, using the CCN logo image, fixing the active-state bug, and hiding admin links from staff.

**Architecture:** One new `AppNav.razor` component owns all nav markup and CSS. MainLayout becomes a thin shell: `<AppNav>` + `@Body` + `<ProjectorOverlay>`. The Leaderboard toggle passes from MainLayout to AppNav as an `EventCallback` parameter. No JS interop — CSS media queries handle breakpoint switching.

**Tech Stack:** Blazor Server, MudBlazor 6.11, CSS custom properties (`--black`, `--color-primary`, etc. from ThemeHead), `NavigationManager` for active-state detection.

---

## File Map

| Action | File | Responsibility |
|---|---|---|
| Create | `CampClotNot/Shared/AppNav.razor` | All nav markup, CSS, active-state logic, admin dropdown/sheet state |
| Modify | `CampClotNot/Shared/MainLayout.razor` | Remove nav markup; add `<AppNav OnLeaderboardClick>` + `_showProjector` state only |
| Modify | `CampClotNot/Shared/ThemeHead.razor` | Add `.ccn-fab-mobile` media query rule so FAB clears the bottom bar on mobile |
| Modify | `CampClotNot/Pages/Dashboard.razor` | Add `ccn-fab-mobile` class to FAB button |

---

## Task 1: Create `AppNav.razor`

**Files:**
- Create: `CampClotNot/Shared/AppNav.razor`

- [ ] **Step 1: Create the file with this complete content**

```razor
@inject NavigationManager Nav
@inject ThemeService ThemeSvc

<style>
    /* ── Breakpoint control ── */
    .nav-mobile-header  { display: flex; }
    .nav-desktop-header { display: none; }
    .nav-bottom-bar     { display: flex; }

    @@media (min-width: 769px) {
        .nav-mobile-header  { display: none; }
        .nav-desktop-header { display: flex; }
        .nav-bottom-bar     { display: none; }
    }

    /* ── Mobile header ── */
    .nav-mobile-header {
        background: var(--black);
        border-bottom: 4px solid var(--color-primary);
        padding: 10px 16px;
        align-items: center;
        justify-content: space-between;
        position: sticky;
        top: 0;
        z-index: 50;
    }

    /* ── Desktop header ── */
    .nav-desktop-header {
        background: var(--black);
        border-bottom: 4px solid var(--color-primary);
        padding: 12px 18px;
        align-items: center;
        justify-content: space-between;
        gap: 12px;
        flex-wrap: wrap;
        position: sticky;
        top: 0;
        z-index: 50;
    }

    /* ── Desktop nav link ── */
    .nav-link {
        font-family: 'Fredoka One', cursive;
        font-size: 12px;
        padding: 6px 14px;
        border-radius: 6px;
        border: 3px solid #555;
        cursor: pointer;
        text-decoration: none;
        box-shadow: 2px 2px 0 #555;
        background: transparent;
        color: #ccc;
        transition: all .1s;
        display: inline-block;
        line-height: 1.4;
    }

    /* ── Desktop admin dropdown panel ── */
    .nav-admin-dropdown { position: relative; }
    .nav-admin-panel {
        position: absolute;
        top: calc(100% + 8px);
        right: 0;
        background: var(--panel-bg);
        border: 3px solid var(--black);
        border-radius: 10px;
        box-shadow: 4px 4px 0 var(--black);
        min-width: 180px;
        z-index: 51;
        overflow: hidden;
    }
    .nav-admin-panel a,
    .nav-admin-panel button {
        display: block;
        width: 100%;
        padding: 12px 16px;
        font-family: 'Fredoka One', cursive;
        font-size: 13px;
        color: var(--black);
        text-decoration: none;
        border: none;
        border-bottom: 2px solid #E8E0CC;
        background: transparent;
        cursor: pointer;
        text-align: left;
    }
    .nav-admin-panel a:last-child,
    .nav-admin-panel button:last-child { border-bottom: none; }
    .nav-admin-panel a:hover,
    .nav-admin-panel button:hover { background: #F5EED8; }

    /* ── Bottom bar ── */
    .nav-bottom-bar {
        position: fixed;
        bottom: 0;
        left: 0;
        right: 0;
        background: var(--bg-base);
        border-top: 4px solid var(--black);
        z-index: 40;
        justify-content: stretch;
    }
    .nav-tab {
        flex: 1;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        padding: 8px 4px 6px;
        gap: 2px;
        border: none;
        border-top: 3px solid transparent;
        background: transparent;
        cursor: pointer;
        text-decoration: none;
        color: var(--text-light);
        font-family: 'Fredoka One', cursive;
        font-size: 10px;
        letter-spacing: 0.5px;
        min-height: 56px;
        transition: color .15s, border-color .15s;
    }
    .nav-tab.active {
        color: var(--black);
        border-top-color: var(--black);
    }
    .nav-tab .tab-icon { font-size: 20px; line-height: 1; }

    /* ── Admin bottom sheet ── */
    .nav-admin-sheet {
        position: fixed;
        bottom: 64px;
        left: 0;
        right: 0;
        background: var(--panel-bg);
        border-top: 4px solid var(--black);
        border-radius: 16px 16px 0 0;
        box-shadow: 0 -4px 0 var(--black);
        z-index: 45;
        animation: slideUp .2s ease;
    }
    .nav-admin-sheet a,
    .nav-admin-sheet button {
        display: flex;
        align-items: center;
        padding: 16px 20px;
        font-family: 'Fredoka One', cursive;
        font-size: 16px;
        color: var(--black);
        text-decoration: none;
        border: none;
        border-bottom: 2px solid #E8E0CC;
        background: transparent;
        cursor: pointer;
        width: 100%;
        text-align: left;
    }
    .nav-admin-sheet a:last-child,
    .nav-admin-sheet button:last-child { border-bottom: none; }
</style>

@* ── MOBILE HEADER ──────────────────────────────────────────────── *@
<div class="nav-mobile-header">
    <img src="/img/ccn-logo-2026.png" alt="Camp Clot Not" style="height:38px;object-fit:contain" />
    <button class="ccn-btn" style="font-size:12px;padding:7px 14px;background:var(--color-primary)"
            @onclick="OnLeaderboardClick">
        📺 Leaderboard
    </button>
</div>

@* ── DESKTOP HEADER ─────────────────────────────────────────────── *@
<div class="nav-desktop-header">
    <img src="/img/ccn-logo-2026.png" alt="Camp Clot Not" style="height:40px;object-fit:contain" />

    <AuthorizeView>
        <Authorized>
            <div style="display:flex;gap:6px;flex-wrap:wrap;align-items:center">
                <a href="/" class="nav-link" style="@ActiveStyle("/", "#D12B2B")">📊 Dashboard</a>
                <a href="/board" class="nav-link" style="@ActiveStyle("/board", "#F5C800")">🗺️ Activities</a>

                <AuthorizeView Roles="Admin" Context="adminCtx">
                    <a href="/transactions" class="nav-link" style="@ActiveStyle("/transactions", "#2563C4")">📋 Transactions</a>

                    <div class="nav-admin-dropdown">
                        @if (_adminDropdownOpen)
                        {
                            <div style="position:fixed;inset:0;z-index:49"
                                 @onclick="() => _adminDropdownOpen = false"></div>
                        }
                        <button class="nav-link" style="@AdminDropdownStyle()"
                                @onclick="() => _adminDropdownOpen = !_adminDropdownOpen">
                            ⚙️ Admin @(_adminDropdownOpen ? "▲" : "▼")
                        </button>
                        @if (_adminDropdownOpen)
                        {
                            <div class="nav-admin-panel">
                                <a href="/admin/board"   @onclick="() => _adminDropdownOpen = false">⚙️ Board Admin</a>
                                <a href="/admin/groups"  @onclick="() => _adminDropdownOpen = false">👥 Groups</a>
                                <a href="/admin/users"   @onclick="() => _adminDropdownOpen = false">🔑 Users</a>
                            </div>
                        }
                    </div>
                </AuthorizeView>

                <button class="nav-link" style="@ActiveStyle("/__lbd__", "#F5C800")"
                        @onclick="OnLeaderboardClick">
                    📺 Leaderboard
                </button>
                <button onclick="window.location='/logout'" class="nav-link"
                        style="border-color:#D12B2B;box-shadow:2px 2px 0 #D12B2B;color:#D12B2B;opacity:.8">
                    Sign Out
                </button>
            </div>
        </Authorized>
    </AuthorizeView>
</div>

@* ── BOTTOM BAR (mobile) ─────────────────────────────────────────── *@
<AuthorizeView>
    <Authorized>
        <div class="nav-bottom-bar">
            <a href="/" class="nav-tab @(IsActive("/") ? "active" : "")">
                <span class="tab-icon">📊</span>
                <span>Dashboard</span>
            </a>
            <a href="/board" class="nav-tab @(IsActive("/board") ? "active" : "")">
                <span class="tab-icon">🗺️</span>
                <span>Activities</span>
            </a>
            <AuthorizeView Roles="Admin" Context="mobileAdminCtx">
                <button class="nav-tab @(_adminSheetOpen ? "active" : "")"
                        @onclick="() => _adminSheetOpen = !_adminSheetOpen">
                    <span class="tab-icon">⚙️</span>
                    <span>Admin</span>
                </button>
            </AuthorizeView>
        </div>
    </Authorized>
</AuthorizeView>

@* ── ADMIN BOTTOM SHEET (mobile) ─────────────────────────────────── *@
@if (_adminSheetOpen)
{
    <div style="position:fixed;inset:0;z-index:44;background:rgba(26,26,26,.4)"
         @onclick="() => _adminSheetOpen = false"></div>
    <div class="nav-admin-sheet">
        <a href="/transactions" @onclick="() => _adminSheetOpen = false">📋 Transactions</a>
        <a href="/admin/board"  @onclick="() => _adminSheetOpen = false">⚙️ Board Admin</a>
        <a href="/admin/groups" @onclick="() => _adminSheetOpen = false">👥 Groups</a>
        <a href="/admin/users"  @onclick="() => _adminSheetOpen = false">🔑 Users</a>
        <button onclick="window.location='/logout'">🚪 Sign Out</button>
    </div>
}

@code {
    [Parameter] public EventCallback OnLeaderboardClick { get; set; }

    private bool _adminDropdownOpen;
    private bool _adminSheetOpen;

    private bool IsActive(string href) =>
        href == "/"
            ? Nav.ToBaseRelativePath(Nav.Uri) is "" or "/"
            : ("/" + Nav.ToBaseRelativePath(Nav.Uri))
                .StartsWith(href, StringComparison.OrdinalIgnoreCase);

    private string ActiveStyle(string href, string color)
    {
        var active = href.StartsWith("/__")
            ? false
            : IsActive(href);
        return active
            ? $"border-color:{color};box-shadow:2px 2px 0 {color};background:{color};color:#1A1A1A"
            : "";
    }

    private string AdminDropdownStyle()
    {
        var adminActive = ("/" + Nav.ToBaseRelativePath(Nav.Uri))
            .StartsWith("/admin", StringComparison.OrdinalIgnoreCase);
        return adminActive
            ? "border-color:#9B59B6;box-shadow:2px 2px 0 #9B59B6;background:#9B59B6;color:white"
            : "";
    }
}
```

- [ ] **Step 2: Verify the file was created**

```
dir CampClotNot\Shared\AppNav.razor
```
Expected: file exists, non-zero size.

---

## Task 2: Simplify `MainLayout.razor`

**Files:**
- Modify: `CampClotNot/Shared/MainLayout.razor`

- [ ] **Step 1: Replace the entire file content with this**

```razor
@inherits LayoutComponentBase
@inject NavigationManager Nav
@inject ThemeService ThemeSvc

<ThemeHead />
<MudThemeProvider IsDarkMode="false" Theme="@_mudTheme" />
<MudDialogProvider />
<MudSnackbarProvider />

<ProjectorOverlay Visible="_showProjector" OnClose="@(() => _showProjector = false)" />

<AppNav OnLeaderboardClick="@(() => _showProjector = true)" />

<div style="padding:20px 18px 80px">
    @Body
</div>

@code {
    private bool _showProjector;

    private static readonly MudTheme _mudTheme = new()
    {
        Palette = new Palette
        {
            Primary         = "#F5C800",
            Secondary       = "#D12B2B",
            Success         = "#2B8A3E",
            Info            = "#2563C4",
            Background      = "#F2ECD8",
            Surface         = "#FFFEF7",
            DrawerBackground= "#FFFEF7",
            TextPrimary     = "#1A1A1A",
            TextSecondary   = "#4A4035",
            AppbarBackground= "#1A1A1A",
            AppbarText      = "#F5C800",
        }
    };
}
```

Note: `padding-bottom: 80px` gives clearance above the mobile bottom bar. On desktop the extra padding is harmless.

- [ ] **Step 2: Build to confirm no compile errors**

Run from `CampClotNot/`:
```
dotnet build 2>&1 | findstr /i "error warning succeeded failed"
```
Expected: `Build succeeded` with at most the existing `CS0618` obsolete warning on `Palette`.

---

## Task 3: FAB mobile offset

**Files:**
- Modify: `CampClotNot/Shared/ThemeHead.razor`
- Modify: `CampClotNot/Pages/Dashboard.razor`

- [ ] **Step 1: Add `.ccn-fab-mobile` rule to `ThemeHead.razor`**

After the `@@keyframes pulseBorder` block, add:

```css
    /* FAB clears the bottom nav bar on mobile */
    @@media (max-width: 768px) {
        .ccn-fab-mobile { bottom: 80px !important; }
    }
```

- [ ] **Step 2: Add `ccn-fab-mobile` class to the FAB in `Dashboard.razor`**

Find the FAB button (search for `＋ Log Score`). Change:

```razor
<button class="ccn-btn" style="position:fixed;bottom:24px;right:24px;background:var(--color-accent);color:white;
```

To:

```razor
<button class="ccn-btn ccn-fab-mobile" style="position:fixed;bottom:24px;right:24px;background:var(--color-accent);color:white;
```

---

## Task 4: Manual verification

- [ ] **Step 1: Start the app**

Run from `CampClotNot/`:
```
dotnet run
```
Navigate to `https://localhost:63533` and log in with `tyler@hbda.local` / `DevAdmin1!`.

- [ ] **Step 2: Verify desktop layout (>768px browser window)**
  - Logo image renders in the top-left (not the 🏁 emoji)
  - Nav links visible: Dashboard, Activities, Transactions, ⚙️ Admin ▼, 📺 Leaderboard, Sign Out
  - Click ⚙️ Admin ▼ — dropdown opens showing Board Admin / Groups / Users
  - Click anywhere outside dropdown — it closes
  - Click Dashboard — it highlights red, no other link highlights
  - Click Activities — it highlights yellow, Dashboard deactivates
  - Click 📺 Leaderboard — full-screen leaderboard overlay appears, ✕ Exit closes it
  - No bottom bar visible

- [ ] **Step 3: Verify mobile layout (narrow browser ≤768px)**
  - Resize browser to ≤768px wide (or use DevTools mobile emulation)
  - Header: logo image on left, 📺 Leaderboard button on right, no nav links
  - Bottom bar visible: Dashboard | Activities | ⚙️ Admin tabs
  - Click ⚙️ Admin tab — bottom sheet slides up with Transactions / Board Admin / Groups / Users / Sign Out
  - Click backdrop — sheet dismisses
  - Active tab has black top border indicator
  - FAB (＋ Log Score) sits above the bottom bar, not hidden behind it

- [ ] **Step 4: Verify active-state fix**
  - Navigate to `/board` — Activities tab/link is highlighted, Dashboard is not
  - Navigate to `/transactions` — Transactions link highlighted (desktop), none on mobile bottom bar
  - Navigate to `/admin/groups` — ⚙️ Admin highlights purple on desktop

---

## Task 5: Commit

- [ ] **Step 1: Stage and commit**

```
git add CampClotNot/Shared/AppNav.razor
git add CampClotNot/Shared/MainLayout.razor
git add CampClotNot/Shared/ThemeHead.razor
git add CampClotNot/Pages/Dashboard.razor
```

Commit message:
```
feat: responsive nav redesign (AppNav) — refs #94

AppNav.razor: single component with CSS media query breakpoint at 768px.
Mobile: slim header (logo + Leaderboard btn) + fixed bottom tab bar
(Dashboard / Activities / Admin). Admin tab opens bottom sheet with
Transactions, Board Admin, Groups, Users, Sign Out. Desktop: full top
bar with logo, nav links, admin dropdown (backdrop dismissal), Leaderboard,
Sign Out.

Active-state fix: root "/" only activates Dashboard (ToBaseRelativePath
empty-string check). Admin section highlights for any /admin/* route.

Logo: ccn-logo-2026.png replaces emoji in both breakpoints.
"Projector" label renamed to "Leaderboard" everywhere.
FAB shifts up 80px on mobile to clear bottom bar.
MainLayout reduced to AppNav + Body + ProjectorOverlay.
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement | Task |
|---|---|
| AppNav single component | Task 1 |
| CSS media query at 768px | Task 1 |
| Logo image in both headers | Task 1 |
| "Leaderboard" replaces "Projector" | Task 1, 2 |
| Active-state bug fix | Task 1 (`IsActive`) |
| Admin links hidden from staff | Task 1 (`AuthorizeView Roles="Admin"`) |
| Admin dropdown on desktop with backdrop | Task 1 |
| Admin bottom sheet on mobile | Task 1 |
| FAB clears bottom bar | Task 3 |
| MainLayout simplified | Task 2 |
| Hub tab excluded | ✅ Not present in AppNav |
| No JS interop | ✅ Pure CSS + Blazor state |

**Placeholder scan:** No TBDs, TODOs, or vague steps. All code is complete.

**Type consistency:**
- `IsActive(string href)` — defined and called consistently in Task 1
- `ActiveStyle(string href, string color)` — defined and called consistently in Task 1
- `AdminDropdownStyle()` — defined and called consistently in Task 1
- `OnLeaderboardClick` EventCallback — defined in Task 1, passed in Task 2
- `_showProjector` bool — defined in Task 2, used in Task 2
- `Palette` (not `PaletteLight`) — consistent with MudBlazor 6.11 as already in codebase
