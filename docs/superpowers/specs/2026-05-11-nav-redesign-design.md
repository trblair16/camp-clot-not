# Navigation Redesign — Design Spec
**Date:** 2026-05-11  
**Branch:** feature/94-ui-overhaul  
**Status:** Approved, pending implementation plan

---

## Problem

The current navigation is a single horizontal header bar containing the logo, 📺 Projector button, 3 staff nav links, 3 admin nav links, and Sign Out — 8+ items crammed into one row. Known issues:

- Dashboard stays highlighted on every page (active-state bug: `TrimEnd('/')` on `"/"` produces `""`, and `EndsWith("")` is always true)
- Wraps to two rows on 375px phones (iPhone SE minimum)
- Admin links always visible to staff volunteers who never need them
- Reaching the top bar one-handed on a phone is ergonomically awkward; staff use phones exclusively during camp

---

## Approach

**Responsive dual-nav with CSS media queries** — one `AppNav` component renders both a mobile bottom bar and a desktop top bar; CSS shows/hides the appropriate variant at a 768px breakpoint. No JS, no Blazor render-time branching, no flicker.

This gives phone-first volunteers thumb-friendly navigation while preserving a natural top bar for Tyler and Amanda on laptops. Two nav UI patterns, one component, one CSS breakpoint.

---

## Navigation Structure

### Routes & Ownership

| Route | Label | Roles |
|---|---|---|
| `/` | 📊 Dashboard | All |
| `/board` | 🗺️ Activities | All (Board now; Mini-Games tab added in v0.4.0) |
| `/transactions` | 📋 Transactions | Admin only |
| `/admin/board` | ⚙️ Board Admin | Admin only |
| `/admin/groups` | 👥 Groups | Admin only |
| `/admin/users` | 🔑 Users | Admin only |
| (overlay) | 📺 Leaderboard | All |

**Hub tab (`/hub`):** Intentionally excluded until v0.5.0 ships. Adding a placeholder now would confuse volunteers. The `AppNav` component is designed so adding the Hub tab in v0.5.0 is a 10-minute addition.

**Transactions** is removed from the staff nav entirely — confirmed admin-only audit tool. Staff log scores via the FAB; they don't need the transaction log.

### What Each Role Sees

| Role | Primary nav items | Admin items |
|---|---|---|
| Staff | Dashboard, Activities | — |
| Admin | Dashboard, Activities | Transactions, Board Admin, Groups, Users, Sign Out |
| All | 📺 Leaderboard button | — |

---

## Mobile Layout (≤768px)

### Header (sticky, top)
- Left: CCN logo image (`/img/ccn-logo-2026.png`) — no text title
- Right: 📺 Leaderboard button (neo-brutalist: cream bg, black border, shadow offset)
- No nav links in the mobile header

### Bottom Nav Bar (fixed, bottom)
- Cream background, 4px solid black top border, shadow offset upward
- Full viewport width
- Tab items: icon above, short label below, Fredoka One font
- Active tab: accent color + thick black bottom indicator line
- Inactive tabs: muted (`--text-light` color)
- Minimum touch target: 44px height per tab
- FAB (`＋ Log Score`) shifts up to `bottom: 72px` so it clears the bar

**Staff bottom bar (2 tabs):**
```
| 📊 Dashboard | 🗺️ Activities |
```

**Admin bottom bar (4 tabs):**
```
| 📊 Dashboard | 🗺️ Activities | 📋 Transactions | ⚙️ Admin |
```

### Admin Bottom Sheet (mobile, admin only)
Tapping ⚙️ Admin slides up a panel from the bottom bar containing:
- Board Admin
- Groups
- Users
- Sign Out

Panel style: cream bg, 4px solid black top border, thick shadow, border-radius top corners only. Tapping the backdrop dismisses it. Each row: 48px tall, Fredoka One label, left-aligned icon, right chevron.

---

## Desktop Layout (>768px)

### Header (sticky, top)
Black band, 4px solid `--color-primary` (yellow) bottom border — same as current.

```
[CCN Logo]    [📊 Dashboard] [🗺️ Activities] [📋 Transactions*] [⚙️ Admin*]    [📺 Leaderboard]  [Sign Out]
```
`*` admin-only

- Logo: image only, no text (same asset as mobile)
- Nav links: Fredoka One, neo-brutalist pill style, active state uses link's accent color + matching border/shadow
- ⚙️ Admin: single button that toggles an inline dropdown panel (cream bg, black border, shadow) containing Board Admin, Groups, Users stacked as links. Dismissal uses a full-screen transparent backdrop div (`position:fixed;inset:0;z-index:49`) rendered behind the panel when open — clicking it sets `_adminDropdownOpen = false`. No JS interop needed.
- Sign Out: far right, de-emphasized (border only, no fill, smaller)
- 📺 Leaderboard: yellow fill button, always visible

---

## Active State Logic

Replace the current `EndsWith` check with path-exact comparison:

```csharp
private bool IsActive(string href) =>
    href == "/"
        ? Nav.ToBaseRelativePath(Nav.Uri) == ""        // root only
        : Nav.Uri.Contains(href, StringComparison.OrdinalIgnoreCase);
```

This fixes the Dashboard-always-highlighted bug and correctly handles nested admin routes (e.g. `/admin/board` activates the ⚙️ Admin section).

---

## Component Structure

### New: `Shared/AppNav.razor`
Single component containing all nav markup. Uses `@inject NavigationManager Nav` and `@inject ThemeService ThemeSvc`. Role visibility via `<AuthorizeView Roles="Admin">`.

Internal structure:
```
AppNav.razor
├── Mobile header div        (display:flex @media ≤768px, display:none @media >768px)
├── Desktop header div       (display:none @media ≤768px, display:flex @media >768px)
├── Bottom bar div           (display:flex @media ≤768px, display:none @media >768px)
└── Admin bottom sheet div   (conditional on _adminSheetOpen, mobile only)
```

CSS media queries live in a `<style>` block inside `AppNav.razor` — scoped to this component, no global side effects.

### Modified: `Shared/MainLayout.razor`
- Remove all current nav markup and `_showProjector` state (projector stays)
- Replace with `<AppNav />`
- `ProjectorOverlay` remains in `MainLayout` — layout-level concern, not nav

### Logo Asset
Copy `mockup/assets/ccn-logo-2026.png` → `wwwroot/img/ccn-logo-2026.png`. Referenced as `<img src="/img/ccn-logo-2026.png" alt="Camp Clot Not" style="height:40px">`. The `mockup/assets/` source is not committed to wwwroot and is not served by Blazor static files.

---

## Out of Scope

- Hub tab — added in v0.5.0 only
- Any JS-based device detection (CSS breakpoint is sufficient)
- Animations on the bottom sheet beyond a simple CSS transition (slide-up is enough)
- Changes to `ProjectorOverlay`, `LogTransactionDialog`, or any page-level components

---

## Open Questions (Resolved)

| Question | Decision |
|---|---|
| Bottom nav vs simplified top bar vs hybrid? | Hybrid (CSS media query) |
| Hub tab now or at v0.5.0? | v0.5.0 only |
| Transactions for staff? | Admin only — removed from staff nav |
| Board + Mini-Games grouping? | "Activities" tab, `/board` for now, tabbed page in v0.4.0 |
| Logo: emoji or image? | Image — `ccn-logo-2026.png` already exists in repo |
| "Projector" label? | Renamed to "Leaderboard" for domain clarity |
