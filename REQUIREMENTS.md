# Camp Clot Not — Super Party 2026
## Software Requirements, Animation/Asset Specification & Architecture Guide

**Prepared by:** Tyler Blair | April 2026 | Updated May 2026  
**Claude Code Handoff Document**

This document is the primary reference for Claude Code. All pending decisions are marked **⚠️ PENDING** and resolved decisions are marked **✅ DECIDED**. Update this file directly when decisions are resolved and commit as a `chore/N-name` issue close.

A working React prototype mockup exists in the project files (`ccn-mockup-v2.jsx`) and serves as the primary visual reference. The mockup captures structure and logic; the Blazor implementation should use proper assets and animations described in Part 2.

---

## Part 1 — Functional Requirements

### 1.1 Technology Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor Server (.NET) |
| Middleware/API | None in v1 — service/repository pattern inside Blazor app. Flask deferred to v2. |
| Database | PostgreSQL (via EF Core + Npgsql) |
| Hosting | Railway.app |
| Repo | github.com/trblair16/Capstone_Fall2021 (to be overhauled, not forked from original) |

✅ **DECIDED:** No Flask middleware in v1. Blazor Server talks directly to PostgreSQL via EF Core using a clean service/repository layer. Flask or .NET Minimal API extracted in v2 when a second client exists.

✅ **DECIDED:** PostgreSQL on Railway — not SQL Server/Azure. Eliminates cloud hosting cost and billing surprise risk.

✅ **DECIDED:** Blazor Server retained as the long-term frontend architecture. PWA support implemented as install layer + offline snapshot (§8.9) without migrating to Blazor WASM or a separate React frontend. Frontend architecture revisited only if a second non-staff client is required (e.g. camper-facing read-only view in a future season).

---

### 1.2 User Roles & Permissions

| Role | Permissions |
|---|---|
| Admin (Tyler / Katelyn / Vicki) | Full access: pre-load board, configure themes, award coins/stars, void any transaction, manage groups, trigger block hit, manage user accounts, pin/urgent announcements, edit info pages, edit staff directory |
| Staff | Award coins and stars to groups, add optional note, view dashboard and board, post normal-priority announcements, view all Hub features. Cannot void others' transactions or access admin config. |
| Volunteer | Log transactions only. View Hub features. No admin access. GroupId assigned to filter schedule view. |

✅ **DECIDED:** Projector display pages (`/board/display`, `/minigames/display`) are Admin-only. Tyler opens them as a separate browser window on his laptop. No `Display` role — removed in v0.5.0.

---

### 1.3 Theme System

The application supports a swappable theme layer so the same competition core can be re-skinned each year. Theming controls:

- Group/team names, colors, and token art
- Location names (pool, med hut, dining hall, cabins)
- Currency names and icons (Coins + Stars this year)
- Board space labels and iconography
- Overall color palette and header branding

For 2026, the active theme is **Super Mario Party**. All Mario-specific naming is defined in the theme layer, not hardcoded in logic. See §7.10 for the full glossary of display names vs. system names.

---

### 1.4 Groups

Competition is tracked at the group level only. Individual camper point tracking has been deliberately removed to reduce complexity.

Each group has:
- Name, short name, color, character/token asset
- Coin total (computed from non-voided coin transactions)
- Star total (computed from non-voided star transactions)
- Current board position (space index)
- Cabin assignment (display reference)

> ⚠️ PENDING: How many groups for 2026? Board confirmed 4-6 groups. Final number depends on cabin groupings and activity finalization. Data model must support variable group count — do not hardcode 4. Board token design and space count follow once finalized.

### 1.5 Currency System — Coins & Stars

**Coins 🪙 — Frequent, activity-based**
- Daily camp activities (participation and placement)
- Evening challenges / Minute to Win It (higher value for winning)
- Possible deductions for behavior (negative transactions)

**Stars ⭐ — Rare, prestigious**
- Awarded directly by staff at key moments (judgment call)
- Camper awards (Clean Camper, Mentoring Camper, etc.) — pre-voted, pre-loaded by Tyler, awarded as bonus stars for the group
- Big Stick Awards (first self-IV access at camp) — converted to group bonus stars
- Branch Awards (returning campers demonstrating continued independence) — converted to group bonus stars
- End-of-camp balance stars — staff can award to help groups that fell behind

> ✅ DECIDED: Groups can spend coins to purchase stars via a coin shop. Staff also award stars directly via judgment. A dedicated Shop page will be built for projector display in the galley (main gathering area) so groups can see what's available. See §1.11.

> ✅ DECIDED: Stars are the primary win condition. Coins provide strategic value — earn them through activities, spend them in the shop to buy stars. Final standings sort: stars descending, tiebreak coins descending. This mirrors real Mario Party mechanics.

### 1.6 Transaction System

Every coin and star movement is a transaction. Append-only — nothing deleted, only voided.

**Fields:** ID, GroupId, CurrencyType (coins/stars), Amount (positive or negative), Note (optional), LoggedBy, CreatedAt, VoidedAt (nullable), VoidedBy (nullable)

- Any staff member can post a transaction
- Admins can void any transaction — reverses the amount and marks the record voided
- Voided transactions stay visible in the log (audit trail) but are visually distinguished
- No approval queue — post immediately, correct via void

✅ **DECIDED:** No approval/request workflow. Keeps volunteer burden minimal. Admins correct via void after the fact.

---

### 1.7 Board Game

A visual game board displayed on the projector during gatherings.

- Winding snake/rectangular path shape — see `ccn-mockup-v2.jsx` for reference layout
- Spaces pre-designed by admin before camp to match the week's schedule
- Space type is determined by the Activity linked to the space — there is no SpaceType enum. `BoardSpace.ActivityId → Activity.ActivityTypeId → ActivityType.CategoryId → ActivityTypeCategory.SystemName`. The category `SystemName` drives space rendering behavior (icon, color, effect).
- Each group has a token sitting on their current board position (GroupBoardPos)
- Board positions persist in DB
- SVG-based rendering with proper icon assets (not emoji)

> ⚠️ PENDING: How many board spaces? At minimum one per activity. Confirmed activities so far: Arts & Crafts, Pool (Blooper Bay), Lake, plus a slate of Mario Party-themed games/activities still being finalized. Space count follows activity finalization — design the board to be configurable rather than hardcoded.

> ✅ DECIDED: Board is shown only during weekly group gatherings — not displayed all week. The board reveal is the theatrical moment that tells campers what their first activity of the day is. Display mode should default to off/idle and be activated by admin for gatherings.

### 1.8 Block Hit Mechanic

The theatrical centerpiece of each group gathering.

- Pre-scripted by admin (Tyler) before camp — each group has a predetermined destination for each day
- The number shown is calculated from current position to destination (accurate to scripted result, appears random)
- Admin triggers the animation from a tablet during the gathering
- Block hit is an overlay/modal on the Board page — not a separate page
- After number reveal, token animates step-by-step across the board to destination space
- Admin can update scripted destinations on the fly if schedule changes

✅ **DECIDED:** Pre-scripted, not truly random. Ensures balanced outcomes and staff control.

✅ **DECIDED:** Block hit animation plays on the projector display via SignalR broadcast, triggered from admin tablet. Architecturally significant — plan SignalR hub message types before building this feature. Suggested hub events: `BlockHitTriggered`, `BlockHitNumberRevealed`, `TokenMoveStep`, `TokenMoveDone`, `ScoresUpdated`.

---

### 1.9 Mini-Games / Evening Challenge Picker

Dedicated page for Minute to Win It and group evening challenges.

- Full list of challenges visible on screen at all times
- Pointing hand icon cycles through the list, slowing to a stop on the result
- Challenge list pre-configured by admin; result is pre-scripted (same philosophy as block hit)
- Designed for projector display during evening gatherings
- After reveal, staff can log coins/stars directly from this page

### 1.11 Coin Shop

A shop page where groups can spend coins to purchase stars, displayed on the projector in the galley during gatherings.

- Shop items configured by admin before camp (e.g. "1 Star = 50 Coins")
- Staff processes purchases on behalf of a group — no self-service by campers
- Purchase posts two transactions atomically: a negative coin transaction and a positive star transaction
- Shop page is projector-friendly: large text, group color coding, visible current coin balances
- Admin can enable/disable the shop (e.g. close it before closing ceremony)

> ✅ DECIDED: Coin exchange for stars is supported. Shop is a dedicated projector-display page in the galley. Staff processes purchases — groups cannot self-serve.

### 1.12 Attendance Tracking

✅ **DECIDED:** Deferred to v2. Build competition system fully for 2026 camp first. Add Attendee/EventAttendance tables in a post-camp sprint.

### 1.13 Display / Projector Mode

Tyler controls the projector from his laptop and needs to present the display view without exposing admin navigation to the audience.

> ✅ DECIDED: `/display` is a dedicated full-screen route with no admin chrome — no nav, no transaction buttons, no admin controls. Tyler opens it as a separate browser window and drags it to the projector screen (extended display). Admin panel has a "Open Display" button that launches `/display` in a new window. No PIN required since Tyler controls which device shows it. The route is still role-restricted (Display or Admin role) to prevent accidental access.

---

## Part 2 — Animation & Asset Specification

The React mockup uses CSS animations and emoji as structural placeholders. This section describes what each animated moment should look and feel like in the production Blazor app.

### 2.1 General Asset Guidance

| Asset | Guidance |
|---|---|
| App logo / header | Camp Clot Not Super Party 2026 logo PNG (in project files) |
| Group tokens | Custom illustrated character badges per group — pixel art or flat vector. AI-generated acceptable as dev placeholder. |
| ? Block | Mario-style yellow ? block SVG or PNG. Must support idle, hit, rolling, and reveal states. |
| Board spaces | Custom SVG icons per space type: coin, star shape, Bowser face, gamepad, checkered flag |
| Pointing hand | Mario-glove style pointing hand SVG. Must support swing/cycling animation. game-icons.net has free options. |
| Icon library | Game Icons (game-icons.net) for themed UI chrome. Lucide/Heroicons for standard UI actions. |
| Animation library | Lottie (via JS interop) for complex sprite animations. CSS keyframes for micro-interactions. |
| Fonts | Display/headers: Fredoka One (Google Fonts). Body: Nunito or similar rounded sans-serif. |

---

### 2.2 Block Hit Animation Sequence

**Phase 1 — Idle:** Block floats with gentle CSS translateY loop. Sparkle/shimmer particles. Pulsing shadow.

**Phase 2 — Hit Trigger:** Block squishes down (scaleY compress) then springs upward. Coin/star pops from top. Flash to white/yellow. Number roll begins immediately.

**Phase 3 — Number Roll:** Large number cycles rapidly (slot-machine flip animation). Starts fast, decelerates over ~2 seconds. Block color shifts yellow → red as it slows.

**Phase 4 — Number Reveal:** Hard stop — no easing. Block shakes, flashes red/gold. Number scales up (popIn: 0.6 → 1.1 → 1.0). Confetti burst. Text: "[Group name] moves X spaces!"

**Phase 5 — Token Movement:** Token lifts off current space. Travels space-by-space along pre-calculated SVG path coordinates. Brief hop + 300ms pause on each intermediate space. Flash on Star/Bowser spaces when passing. Final landing: larger bounce + space glow + floating icons matching space type.

**Implementation:** Pre-calculate SVG coordinates per space. Use JS-stepped setTimeout chain to move token through each waypoint. CSS transition handles the smooth movement between coordinates.

> **CRITICAL:** The full animation sequence plays on the projector (`/display` route) via SignalR broadcast. Tyler's tablet is the trigger only.

---

### 2.3 Mini-Game Spinner

- Full list of challenges visible at all times as styled cards/tiles
- Hand icon appears on left side of list
- On trigger: hand moves down the list highlighting each item as it passes
- Highlighted item: background flashes, text scales up, brief glow
- Movement starts fast, decelerates (ease-out cubic) over ~3 seconds
- Non-highlighted items dim slightly during spin
- Hand stops pointing at final item — item stays highlighted
- Result state: selected item expands/zooms, confetti burst, "Tonight's Challenge!" header

---

### 2.4 Dashboard & Leaderboard

✅ **DECIDED (2026-05-07):** Reference images from Mario Party Superstars confirm the correct layout pattern. See §2.4.1 for the target design. Current implementation (grid of cards) is a known deviation — tracked for v0.3.1 or later.

- **Layout:** Full-width horizontal rows stacked vertically — NOT a grid of cards. Each group gets one row spanning the full width.
- **#1 row treatment:** A bright solid-color band fills the entire #1 row (like the hot-pink highlight stripe in Mario Party Superstars results screen). This is the single most recognizable Mario Party UI pattern.
- **Portrait:** Small square with rounded corners and a colored border. Not a circle.
- **Rank label:** "1st" / "2nd" / "3rd" with outlined white text and colored drop shadow — not a "#1" pill badge.
- **Score display:** Illustrated star/coin icon (styled SVG, not emoji) + large white number.
- **Background:** Deep purple-violet (#3d0066 range) with a warm radial glow/light burst behind center content — NOT dark navy or dark teal-green.
- **UI elements:** Opaque solid-colored panels with thick gold/yellow borders. No backdrop-filter blur, no translucency.
- **Typography:** All key text has thick outlines (white text + dark outline, or colored text + darker-shade outline). Never plain flat text.
- Coin and star totals animate (count-up) when values change
- Sort: stars descending, tiebreak coins descending
- Rank change: rows animate into new positions on score update

#### 2.4.1 Reference Images

Stored locally at `References/` in the repo root (not committed). Key references:

- `Results Leaderboard Reference.jpg` — Mario Party Superstars results screen. Primary layout target.
- `Results Leaderboard Reference 2.jpg` — Older Mario Party results. Shows colored row bands per player.
- `Menu Selector Reference.jpg` — Main menu. Shows the purple-violet background and thick gold-bordered buttons.
- `Cover Reference.jpg` — Mario Party Superstars box art. Shows board aesthetic and overall color energy.

---

### 2.5 Board Visual Design

- Winding snake/rectangular path rendered as thick rounded-corner ribbon
- Each space: distinct rounded shape in its type color with SVG icon
- Space numbers shown small for admin reference
- Spaces with tokens: subtle glow or highlight
- Tokens: circular character badge art, drop shadow, gentle idle bob
- Multiple groups on same space: tokens offset slightly

---

### 2.6 Projector / Display Mode

- Board fills ~70% of screen; leaderboard sidebar ~30%
- Font sizes scaled for 1080p at 10-15 feet: min 24px body, 48px+ for scores
- Reduced UI chrome — no transaction buttons, no admin controls
- Auto-refresh via SignalR real-time push
- Dark background theme
- Graceful reconnect — no blank screen on connection drop

---

### 2.7 Transaction UI

**Log Transaction:**
- Accessible from any page via persistent header button
- Modal: group selector, type (coins/stars), amount (negative = deduction), optional note
- On submit: optimistic UI update, toast confirmation
- Clear error state if submission fails

**Void:**
- Void button on each transaction row (admin role only)
- Single-tap confirmation before voiding
- Voided records: strikethrough, VOIDED badge, grayed out
- Reversal immediate — group totals update in real time

---

## Part 3 — Preliminary Data Model

Use as a starting reference. Refine as implementation progresses. Computed totals (coins, stars) should be derived from the Transaction log, not stored directly.

```
Theme              — ThemeId, Name, Year, CurrencyName1, CurrencyName2, IsActive
Group              — GroupId, ThemeId, DisplayName, ShortName, Color, TokenAssetPath, CabinDisplayName
BoardSpace         — SpaceId, ThemeId, SpaceIndex, SpaceType (enum), Label, IconAssetPath, XPos, YPos
GroupBoardPos      — GroupId, SpaceIndex, UpdatedAt
Transaction        — TxId, GroupId, CurrencyType (enum), Amount, Note, LoggedBy, CreatedAt, VoidedAt?, VoidedBy?
ScriptedBlockHit   — ScriptId, GroupId, CampDay, DestinationSpaceIndex, IsTriggered
ScriptedMiniGame   — ScriptId, CampDay, ActivityLabel, IsTriggered
User               — UserId, DisplayName, Email, PasswordHash, Role (enum: Admin/Staff/Display), IsActive
CamperAward        — AwardId, GroupId, RecipientName, AwardType (enum: Named/BigStick/Branch), BonusStars, AwardedAt
CampSeason         — SeasonId, ThemeId, Year, IsLocked, LockedAt?, LockedBy?

-- Beta / Hub Features (Part 8)
Location           — LocationId, EventId, Name, Description?, Capacity?, SortOrder
ScheduleEvent      — ScheduleEventId, EventId, CampDay, StartTime, EndTime, Title, Description?,
                     LocationId (FK), AppliesToAllGroups, CreatedBy, UpdatedAt
ScheduleEventGroup — ScheduleEventId, GroupId, ActivityId?, LocationId?, Note?
Announcement       — AnnouncementId, EventId, Title, Body, Priority (enum: Normal|Urgent),
                     IsPinned, AuthorId, CreatedAt, ExpiresAt?, IsArchived
StaffMember        — StaffMemberId, EventId, DisplayName, RoleTitle, Phone?, Email?,
                     AvatarEmoji, IsVisible, SortOrder, LinkedUserId?
InfoPage           — PageId, EventId, Slug (unique), Title, Body (markdown), IconEmoji,
                     SortOrder, UpdatedAt, UpdatedByUserId
IncidentReport     — IncidentReportId, EventId, DateOfIncident, DateCompleted,
                     PersonsInvolved, Description, RecommendedAction,
                     SubmittedByUserId, SubmittedByName, SubmittedByRole, SubmittedAt,
                     IsAcknowledged, AcknowledgedByUserId?, AcknowledgedByName?, AcknowledgedAt?
Sponsor            — SponsorId, EventId, Name, LogoUrl, Website?, SortOrder

-- Future / v1.1+
PushSubscription   — SubscriptionId, UserId, Endpoint, P256DH, Auth,
                     Platform (enum: Android|iOS|Desktop), CreatedAt, IsActive
```

⚠️ **PENDING:** Attendance tables — confirmed deferred to v2.

---

## Part 4 — Open Decisions Summary

All items below need resolution at or immediately after the Monday planning meeting. Create each as a blocked GitHub issue. Update to ready after the meeting and assign to the appropriate milestone.

| Decision | Status | Outcome |
|---|---|---|
| How many groups for 2026? | ⚠️ PENDING | 4-6 groups — finalized once cabin groupings confirmed |
| Stars: staff judgment only, or coin exchange too? | ✅ DECIDED | Both — staff award stars directly AND groups can buy stars with coins via shop |
| Coin/star weighting for final standings? | ✅ DECIDED | Stars are win-con, coins are strategic. Sort: stars desc, tiebreak coins desc |
| Board visible all week or only at gatherings? | ✅ DECIDED | Gatherings only — board reveal is the theatrical moment for each day |
| How many board spaces? | ⚠️ PENDING | Minimum one per activity — activity list still being finalized |
| Display mode: login or PIN-protected URL? | ✅ DECIDED | Dedicated `/display` route, no admin chrome, opened as separate window by Tyler |
| How are staff accounts created? | ✅ DECIDED | Admin pre-creates: email, password, first name, last name, role. Depends on auth approach — see §7.2 |
| Session timeout behavior? | ✅ DECIDED | Graceful SignalR reconnect with visible indicator. On hard session expiry: prompt to refresh or auto-refresh |
| Offline/connectivity failure behavior? | ✅ DECIDED | Web app: clear offline error + retry. PWA (future): cached last-known state. No silent failures |
| How is the winner officially declared? | ✅ DECIDED | Closing ceremony, after all camper awards and bonus stars applied. Admin triggers score lock then winner screen |
| Auth0 vs BCrypt/cookie auth? | ⚠️ PENDING | Deeper security review needed. If staying with BCrypt/cookie, must follow proper security standards throughout |

---

## Part 5 — Infrastructure & Deployment

### 5.1 Hosting — Railway.app

✅ **DECIDED:** Host on Railway.app — not Azure. Azure caused billing surprises in 2021 and difficult support experiences. Railway is simpler and usage-based.

- Automatic HTTPS/TLS termination — no certificate or binding configuration needed
- Free public URL: `yourapp.up.railway.app` — no domain purchase required for camp
- Estimated cost for 2-week camp window: $2-4, likely within free $5/month credit
- WebSocket support built in — required for Blazor Server SignalR
- Two Railway services: Blazor app (`web`) + PostgreSQL (internal only, SFO region)
- Production project `camp-clot-not` is live; env vars set; deploy triggers on push to `main`
- Staging environment: defer until needed — use local dev + production dry run

---

### 5.2 Database — PostgreSQL

✅ **DECIDED:** PostgreSQL on Railway — not SQL Server.

- Schema design, querying, and relational modeling concepts are identical to SQL Server
- EF Core with Npgsql provider abstracts the engine — connection string is the only change
- Install PostgreSQL locally via postgresql.org installer (Windows) or Docker Desktop
- Use DBeaver (free, Windows) as local DB GUI — similar feel to SSMS, connects to PostgreSQL
- EF Core migrations handle all schema creation and updates

---

### 5.3 Application Architecture

✅ **DECIDED:** v1 — Blazor Server + EF Core directly. Flask middleware deferred to v2.

```
Blazor Server UI components
        ↓
Service layer (business logic — this IS the logical API)
        ↓
Repository layer (EF Core + Npgsql)
        ↓
PostgreSQL (Railway in prod, local in dev)
```

**v2 extraction path:** Wrap the service layer behind HTTP (Flask or .NET Minimal API) when a second client needs it. The service layer code does not change — only an HTTP routing wrapper is added.

---

### 5.4 Critical Configuration — Claude Code Must Implement

**PORT Environment Variable:**
```csharp
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "5000"}");
```
Railway assigns a dynamic port via the PORT env var. Never hardcode a port. This is the most common cause of Blazor apps working locally but failing on Railway.

**HTTPS — Let Railway Handle It:**
- Do NOT configure HTTPS inside the Blazor app for production
- Railway terminates TLS externally — the app runs HTTP internally
- Disable or environment-gate `ForceHttpsRedirection` (dev only)
- This avoids the IIS/Kestrel binding mismatch experienced in the prior Blazor project

**Environment-Specific Config:**
- All secrets and env-specific values from environment variables — never hardcoded
- Required env vars: `DATABASE_URL` (Railway injects automatically), JWT/auth secret, `AZURE_SIGNALR_CONNECTION` (when implemented)
- `appsettings.Development.json` for local dev; Railway dashboard for staging/prod
- Developer exception pages: enabled in Development, disabled in Production

**SignalR / WebSocket Resilience:**
- Railway supports WebSockets natively — verify during dry run
- Implement graceful reconnect UI: "Reconnecting..." indicator, never a blank screen
- Test by deliberately killing and restarting the Railway service while the app is open
- The block hit animation must survive a reconnect without corrupting board state

**Never commit secrets:**
- `.env` must be in `.gitignore` from the very first commit
- The 2021 repo has a committed `.env` file — this pattern is explicitly prohibited in 2026

---

### 5.5 Environments & Testing Strategy

| Environment | Purpose | Cost |
|---|---|---|
| Local (dev) | All feature development. Blazor + local PostgreSQL. | Free |
| Railway staging | Periodic deploy verification. Paused when not in use. | ~$0.50-1.00 total |
| Railway production | Live for camp. Spun up ~1 week before camp, torn down after. | ~$2-4 total |

**Pre-Camp Dry Run (2-3 weeks before camp) — REQUIRED:**
- Spin up production with real data
- Have Katelyn and Amanda log test transactions from their phones
- Run block hit mechanic on actual projector at full resolution
- Run mini-game spinner on projector
- Deliberately drop/restore internet to test SignalR reconnect
- Multiple simultaneous browser tabs — test concurrent users
- Verify display URL works (with PIN if applicable)
- Test admin void flow end to end
- Tear down and re-deploy from scratch to confirm repeatability

**What NOT to do:**
- Do not leave production running for weeks before camp
- Do not skip staging and deploy directly to production for the first time the day before camp
- Do not rely solely on local testing for anything touching the network, WebSockets, or auth

---

## Part 6 — Versioning, Branching & Project Standards

### 6.1 Versioning — SemVer

Format: `[MAJOR].[MINOR].[PATCH]`

| Segment | When It Changes |
|---|---|
| MAJOR | Breaking architectural change — e.g. 2.0.0 when Flask API extracted |
| MINOR | New feature shipped — e.g. 1.1.0 when push notifications added post-camp |
| PATCH | Bug fix or small non-breaking change — e.g. 1.0.1 for SignalR reconnect fix |

Camp 2026 ships as v1.0.0. Release candidates tagged `v1.0.0-rc.1` etc. during dry run period.

✅ **DECIDED:** SemVer — not calendar-based versioning.

---

### 6.2 Branching Strategy

```
main             — always deployable, protected, Railway production deploys here
dev              — integration branch, Railway staging deploys here
feature/N-name   — new feature work branched off dev
fix/N-name       — bug fix branched off dev
chore/N-name     — config, deps, refactoring
```

N is always the GitHub issue number — open the issue first, use whatever number GitHub assigns. Version numbers belong in tags and PR titles, not branch names.

**Branch Protection Rules — Configure in GitHub Repo Settings at Project Start:**
- `main`: direct pushes blocked — all changes must come through a PR
- `main`: force pushes blocked — history cannot be rewritten
- Violations are blocked at the Git level, not just a convention

**PR & Merge Flow (issue-first — required):**
1. Open a GitHub issue describing the work. Apply appropriate labels.
2. Note the issue number GitHub assigns (e.g. #4). Branch off dev: `git checkout -b feature/4-short-name`
3. Work and commit frequently with meaningful messages
4. Open PR into dev — body includes "Closes #N" to auto-close the issue on merge
5. Self-review PR before merging
6. At milestone boundaries: PR dev into main and tag the release

> Retroactive note: branches 1–3 predate this standard. Starting with issue #4, all branches must have a corresponding GitHub issue opened before the branch is created.

---

### 6.3 Tags & Releases

Tags are immutable — they permanently point to that exact commit.

```bash
git tag v1.0.0 && git push origin v1.0.0
```

- Create a GitHub Release from the tag with release notes
- Rolling back production = point Railway deployment at last known good tag
- Reverting locally: `git checkout v0.3.0`

---

### 6.4 Issues & Labels

| Label | Meaning |
|---|---|
| `feature` | New user-facing functionality |
| `bug` | Something broken or behaving incorrectly |
| `chore` | Config, deps, refactoring — no user-facing change |
| `blocked` | Waiting on a pending decision — cannot proceed |
| `ready` | Decision made, cleared to implement |
| `camp-critical` | Must be complete before camp — cannot slip |
| `post-camp` | Desirable but not blocking camp delivery |

---

### 6.5 Milestones & Delivery Schedule

Camp is June 20-25, 2026.

| Target | Milestone | Scope |
|---|---|---|
| ~May 4 | v0.1.0 — Foundation ✅ | Repo + Railway setup, PostgreSQL + EF Core, auth, Group/Transaction CRUD, branch protection |
| ~May 14 | v0.2.0 — Competition Core ✅ | Leaderboard, coin/star system, transaction log, void flow, staff dashboard, role-based access |
| ~May 28 | v0.3.0 — Board & Block Hit ✅ | SVG board, pre-scripted block hit, step-by-step token movement animation |
| ~May 28 | v0.3.1 — UI Overhaul ✅ | Neo-brutalist redesign, cream bg, rank rows, ccn-coin/star assets, real group names & logos |
| ~May 16 | v0.4.0 — Mini-Games & Display ✅ | Mini-game spinner (3-way split: /minigames, /minigames/display, /admin/games), LiveHub rename, Activities dropdown nav, login page redesign |
| ~June 7 | v0.5.0 — Camp Info Hub + PWA ✅ | Hub features (§8.1–8.4), PWA manifest + service worker + install flow (§8.9), Railway production setup |
| ~May 28 | v0.5.1 — Hub Additions (in progress) | Mobile/PWA fixes, Incident Reports (§8.10), Sponsors (§8.11), Info seed cleanup, PrintLayout |
| ~June 7-8 | v1.0.0-rc — Dry Run | Full dress rehearsal. Staff test on real devices. Projector verified. Issues logged. |
| ~June 13 | v1.0.0 — Camp Ready | Dry run issues resolved. Production seeded. Staff briefed. One week buffer. |
| June 20 | 🏕️ CAMP | Super Clot Not Party '26 is live |
| Post-camp | v1.1.0 | Push notifications, member identity system, Azure SignalR backplane |

---

### 6.6 REQUIREMENTS.md Token Efficiency

This file IS the REQUIREMENTS.md. Reference it as `@REQUIREMENTS.md` in all Claude Code sessions. Do not re-upload the docx. Update pending items directly in this file and commit as a chore issue close.

---

## Part 7 — Gaps, Open Items & Legacy Schema Analysis

### 7.1 Legacy Schema Analysis

The 2021 `hbda_tracking` schema tracked individual attendee points across events. 2026 shifts to group-level only.

| 2021 Table | 2026 Status |
|---|---|
| event | RETAINED — Maps to Event/Theme concept. `type` field is a useful pattern. |
| group | RETAINED & REDESIGNED — `total_points` splits into coin_total/star_total computed from Transaction log, not stored directly. |
| attendee | DEFERRED TO V2 — Simple structure, restore when attendance tracking is added. |
| attendee_points | REPLACED — Individual scoring bridge table. Replaced by group-level Transaction table. |
| pointlog | REPLACED & IMPROVED — Direct ancestor of Transaction table. Adds currency_type, voided_at/voided_by, removes attendee_id. |
| user | RETAINED & EXTENDED — Core structure solid. Password storage must upgrade to ASP.NET Identity or BCrypt. |
| access | SIMPLIFIED — Flatten to Role enum (Admin/Staff/Display) on User table rather than separate lookup. |

✅ **DECIDED:** Do not copy the 2021 schema into EF Core migrations. Use as conceptual reference only. Build models fresh.

> **CRITICAL — .env file:** The 2021 repo has a `.env` file committed to version control. Add `.env` to `.gitignore` from the very first commit in 2026. All secrets go in Railway environment variables only.

---

### 7.2 Authentication & Session Management

> ✅ DECIDED: Admin pre-creates all staff accounts before camp. Fields: email, password (hashed), first name, last name, role (Admin/Staff/Display). Individual accounts provide per-volunteer audit trail on transactions.

> ✅ DECIDED: 24-hour sliding sessions during camp. Graceful SignalR reconnect on connection drop (visible "Reconnecting..." indicator). On hard session expiry: prompt user to refresh or auto-refresh — never a blank/broken screen.

> ✅ DECIDED: `/display` is a dedicated full-screen route opened as a separate browser window by Tyler — no PIN needed. See §1.13.

> ⚠️ PENDING: Auth0 vs BCrypt/cookie auth — requires security review. The 2021 project used Auth0. Current scaffold uses BCrypt + cookie auth. If staying with BCrypt/cookie, must implement proper security standards: HTTPS only, secure/httpOnly cookie flags, CSRF protection, account lockout on repeated failures, no plaintext passwords anywhere in logs or error messages.

**Do NOT carry forward from 2021:**
- Weak/plain password hashing — use ASP.NET Identity or BCrypt
- Secrets in startup scripts or committed `.env` files
- CORS configured for open access — lock to Railway app domain only

---

### 7.3 Admin Setup & Data Seeding Flow

⚠️ **PENDING:** How does Tyler load pre-camp config? Recommendation: seed script for stable config (groups, board layout) + admin UI for scripted sequences (needs last-minute adjustment capability).

**Required pre-camp admin workflows — must be built:**
- Create/edit groups (name, color, token, cabin)
- Create/arrange board spaces (type, position, label, icon)
- Load scripted block hit sequence (per group, per day: destination space index)
- Load scripted mini-game sequence (per day: activity label)
- Create user accounts with role assignment
- Assign camper awards to groups as bonus stars before closing ceremony
- Score lock — freeze transactions before closing ceremony
- Archive/reset — preserve history, reset scores for next year

---

### 7.4 Projector Display Mode — Full Spec

**Layout & Behavior:**
- Route: `/display` (or `/display?pin=XXXX`)
- Board: ~70% of screen. Leaderboard sidebar: ~30%
- No transaction controls, no admin UI — read-only
- Font sizes for 1080p at 10-15 feet: min 24px body, 48px+ scores
- Real-time push via SignalR — not polling
- Dark background theme
- No access to Hub features (§8)

**Block Hit on Projector — ARCHITECTURALLY SIGNIFICANT:**
- Animation MUST play on `/display` via SignalR broadcast
- Tyler's tablet is the trigger device only
- Requires SignalR hub broadcasting to all connected clients
- Plan hub message types before building: `BlockHitTriggered`, `BlockHitNumberRevealed`, `TokenMoveStep`, `TokenMoveDone`, `ScoresUpdated`
- Cannot be easily retrofitted — design the hub with these events from day one

✅ **DECIDED:** Block hit animation plays on projector via SignalR broadcast triggered from admin tablet.

⚠️ **PENDING:** Should leaderboard sidebar always be visible, or should display auto-cycle between full-screen board and full-screen leaderboard? Recommendation: sidebar always visible.

---

### 7.5 Offline & Connectivity Degradation

> ✅ DECIDED: Fail loudly with a clear, non-technical error message and a retry button. Never silent failure. Web app with no connection shows a clear offline state — no cached data displayed as if current. PWA (v2+): cache last-known state so display remains usable while reconnecting.

**Resilience requirements:**
- SignalR reconnect: automatic with visible "Reconnecting..." indicator — never a blank screen
- Transaction failure: clear user-visible error with retry — never silent failure
- Session recovery: phone sleep/wake must not require re-login during camp hours
- Projector recovery: `/display` must auto-resume correct board state on reconnect without manual reload

---

### 7.6 End-of-Camp Flow

> ✅ DECIDED: Winner declared at closing ceremony. All camper awards (Named, Big Stick, Branch) and any end-of-camp bonus stars must be applied first. Tyler then triggers Score Lock — all transactions frozen. Leaderboard at time of lock is the official result. Winning group celebration screen displays on projector.

**End-of-camp features to build:**
- Score lock — admin action freezing all transactions
- Winning group celebration screen on projector
- Final standings screen — clean read-only summary
- Transaction export — CSV download for Vicki's chapter records
- Archive/reset — preserve history, reset for next year

---

### 7.7 Device & Browser Support

| Target | Notes |
|---|---|
| iOS Safari 15+ | Highest-risk browser for Blazor Server WebSockets — test early and specifically |
| Android Chrome 90+ | Secondary volunteer device |
| Desktop Chrome/Edge | Tyler admin device and projector machine |
| Min staff UI width | 375px (iPhone SE) |
| Projector display | 1920x1080 minimum |

> **Priority:** Test SignalR on iOS Safari before the dry run. Most common Blazor Server failure point in production.

---

### 7.8 Error Handling & Observability

- Use Serilog for structured .NET logging — output to Railway's stdout log stream
- All unhandled exceptions must log with enough context to diagnose remotely
- Add `/health` endpoint — HTTP 200 confirming app and DB are reachable
- Transaction failures must surface a user-visible error — never silent
- Do NOT add external observability tooling (Datadog, Sentry) for v1 — Railway logs are sufficient

---

### 7.9 Accessibility Baseline

- Color contrast: WCAG AA (4.5:1) — some campers/volunteers may have visual impairments
- Touch targets: minimum 44x44px — staff tapping phones in outdoor lighting
- Group identity must not rely on color alone — pair with text label or icon
- Projector display: high contrast, 48px+ for key information
- No formal audit required for v1, but these rules must be followed during implementation

---

### 7.10 Glossary — Theme Names vs. System Names

Claude Code must use system names for all variables, routes, DB fields, and components. Display strings are loaded from Theme configuration — never hardcoded in logic.

| Display Name (Mario / Camp Theme) | System Name (Code / DB) |
|---|---|
| Coins | `CurrencyType.Primary` / `coin_total` |
| Stars | `CurrencyType.Prestige` / `star_total` |
| Block Hit | `ScriptedBlockHit` / `TriggerBlockHit()` |
| Board Space | `BoardSpace` |
| Bowser Space | `SpaceType.Penalty` |
| Star Space | `SpaceType.Prestige` |
| Mini-Game Space | `SpaceType.Challenge` |
| Blooper Bay | `LocationType.Pool` (theme display string) |
| Dr. Mario's Medic Station | `LocationType.MedHut` (theme display string) |
| Cabin names (Mushroom Manor, etc.) | `Group.CabinDisplayName` (from Theme config) |
| Group names (Mario's Mushroom Crew, etc.) | `Group.DisplayName` (from Theme config) |
| Evening Challenge / Minute to Win It | `ScriptedMiniGame` |
| Big Stick Award | `AwardType.BigStick` |
| Branch Award | `AwardType.Branch` |
| Named Camper Award | `AwardType.Named` |
| Schedule / Agenda | `ScheduleEvent` |
| Announcements | `Announcement` |
| Staff Directory | `StaffMember` |
| Camp Handbook / Info | `InfoPage` |
| Hub (nav label) | `/hub` route, `HubLayout` component |

---

### 7.11 Manual Test Checklist — Required Before v1.0.0-rc Tag

✅ **DECIDED:** Manual testing only for v1. The dry run is the integration test gate. Unit tests are a post-camp v1.1 consideration.

**Competition Core:**
- [ ] All groups can receive coins and stars independently with correct total reflection
- [ ] Negative coin transactions (deductions) reflect correctly in totals
- [ ] Voiding a transaction correctly reverses the group total
- [ ] Voided transactions appear in log but do not affect live scores
- [ ] Block hit animation plays on the projector display when triggered from admin tablet
- [ ] Token moves step-by-step to correct scripted destination on the projector
- [ ] Mini-game spinner plays and reveals correct scripted result on projector
- [ ] Leaderboard re-sorts correctly after any score change
- [ ] Display mode shows no admin controls and cannot perform transactions
- [ ] SignalR reconnects cleanly after a deliberate connection drop — no blank screen
- [ ] App survives a Railway cold start without errors
- [ ] 3-4 simultaneous users do not cause data conflicts or race conditions
- [ ] Phone sleep/wake does not require re-login during a simulated camp session
- [ ] Score lock prevents all new transactions after being enabled
- [ ] CSV transaction export downloads a complete and accurate log
- [ ] Winning group celebration screen displays correctly on projector after score lock

**Hub Features (add to checklist if v0.5.0 ships before dry run):**
- [ ] Schedule events persist across sessions and display correctly grouped by day
- [ ] Urgent announcement renders red banner on Dashboard and dismisses correctly
- [ ] Expired announcements auto-archive and no longer appear in active feed
- [ ] Staff directory tap-to-call works on iOS Safari and Android Chrome
- [ ] Info page markdown renders correctly including headers, lists, and bold
- [ ] Admin edits to info page body are reflected immediately without page reload
- [ ] Staff role cannot pin announcements, edit directory, or edit info pages (permission enforcement verified)

**PWA (add to checklist if v0.5.0 ships before dry run):**
- [ ] Android Chrome shows install prompt on production Railway URL (HTTPS verified)
- [ ] App installs to Android home screen and launches in standalone mode (no browser chrome)
- [ ] iOS Safari "Add to Home Screen" produces a working home screen icon with correct name and icon
- [ ] Installed app on iOS launches in standalone mode with correct status bar color
- [ ] Offline fallback page displays when device loses connection (not a blank screen or browser error)
- [ ] Service worker does not cache SignalR or API responses — only static shell assets
- [ ] Install tip banner appears on first login on both Android and iOS
- [ ] Install tip banner does not reappear after dismissal on the same device

---

## Part 8 — Beta Features: Camp Info Hub (Yapp Replacement)

**Status:** v0.5.0 shipped. v0.5.1 adds Sponsors and Incident Reports. Hub sub-navigation (Schedule / Announcements / Staff / Sponsors / Info / Incidents[Admin]) under a single **"📋 Hub"** main nav tab.

**Milestone:** `v0.5.0 — Camp Info Hub` | Target ~June 7 if capacity exists. If not complete by June 13, defer to post-camp v1.1.0.

---

### 8.1 Schedule / Agenda View

Replaces Yapp's schedule tab. Staff can view and edit the full week's agenda in one place.

**Data model:**
```
ScheduleEvent      — EventId, CampDay (date), StartTime, EndTime, Title, Description,
                     LocationDisplayName, EventType (enum: Activity|Meal|Travel|Free|Mandatory),
                     AppliesToAllGroups (bool), CreatedBy (UserId), UpdatedAt
ScheduleEventGroup — EventId, GroupId  (bridge; only populated when AppliesToAllGroups = false)
```

**UI behavior:**
- Timeline layout grouped by day; today's day expanded by default, others collapsed
- Event cards show time block, EventType badge (color-coded), location, group scope chips
- Any staff role can add, edit, delete events — no approval flow
- Empty state: "No events scheduled for this day — tap + to add one"

**Scope boundary:** Staff-only. No camper-facing view in beta. No recurring event support.

**System name:** `ScheduleEvent` / `IScheduleService` / `/hub/schedule`

---

### 8.2 Announcements

Replaces Yapp's news/announcements tab. Pull-only in beta — no push notifications (see §9.4 for push roadmap).

**Data model:**
```
Announcement — AnnouncementId, Title, Body (text), Priority (enum: Normal|Urgent),
               IsPinned (bool), AuthorId (UserId), CreatedAt, ExpiresAt (nullable),
               IsArchived (bool)
```

**UI behavior:**
- Feed sorted: pinned first → CreatedAt descending
- Urgent announcements render with a red left border and "🚨 URGENT" badge
- Most recently pinned item surfaces as a dismissible banner on the main Dashboard view
- Staff can post, pin/unpin, and archive — append-only (no delete)
- Admin only: pin/unpin, set Urgent priority. Staff: Normal priority posts only.
- `ExpiresAt`: if set and past, item auto-archives on read

**Scope boundary:** Pull-only — no push/SMS/email delivery in beta.

**System name:** `Announcement` / `IAnnouncementService` / `/hub/announcements`

---

### 8.3 Staff Directory

Replaces Yapp's directory tab.

**Data model:**
```
StaffMember — StaffMemberId, DisplayName, RoleTitle (free text), Phone (nullable),
              Email (nullable), AvatarEmoji (default 👤), IsVisible (bool),
              SortOrder (int), LinkedUserId (UserId, nullable)
```

`LinkedUserId` is nullable — a staff member entry can exist without a system login account.

**UI behavior:**
- Card grid (2 columns mobile, 3+ desktop)
- Phone renders as `tel:` tap-to-call link; email as `mailto:` link
- `IsVisible = false` hides card from all views
- Admin only: add/edit/delete/reorder, toggle visibility. Staff: read-only.
- No photo uploads in beta — emoji avatar only

**System name:** `StaffMember` / `IStaffDirectoryService` / `/hub/staff`

---

### 8.4 Info Pages (Camp Handbook)

Replaces Yapp's info section — rules, FAQs, policies, packing lists.

**Data model:**
```
InfoPage — PageId, Slug (unique), Title, Body (markdown), IconEmoji,
           SortOrder (int), UpdatedAt, UpdatedByUserId
```

**UI behavior:**
- Sidebar list of pages sorted by SortOrder; active page renders body as parsed markdown
- Markdown rendered via Markdig (.NET standard library)
- Admin only: edit body via plain `<textarea>`. UpdatedAt/UpdatedBy stamps on save.
- Staff: read-only
- Pages cannot be created or deleted in beta — slug list is fixed at seed time

**Predefined seed slugs for 2026:** `rules`, `faq`, `medical`  
(`schedule-overview` and `packing` removed in v0.5.1 — redundant with the Schedule tab and pre-camp only scope respectively. Existing DB records are not deleted.)

**Scope boundary:** Markdown body only — no file attachments, no embedded images in beta.

**System name:** `InfoPage` / `IInfoPageService` / `/hub/info`

---

### 8.5 Role Permission Matrix

| Feature | Admin | Staff | Volunteer |
|---|---|---|---|
| View Schedule | ✅ | ✅ | ✅ |
| Edit Schedule | ✅ | ✅ | ❌ |
| View Announcements | ✅ | ✅ | ✅ |
| Post Announcement (Normal) | ✅ | ✅ | ❌ |
| Post Urgent / Pin | ✅ | ❌ | ❌ |
| View Staff Directory | ✅ | ✅ | ✅ |
| Edit Staff Directory | ✅ | ❌ | ❌ |
| View Info Pages | ✅ | ✅ | ✅ |
| Edit Info Page Body | ✅ | ❌ | ❌ |
| View Sponsors | ✅ | ✅ | ✅ |
| Manage Sponsors | ✅ | ❌ | ❌ |
| Submit Incident Report | ✅ | ✅ | ✅ |
| View / Acknowledge Incidents | ✅ | ❌ | ❌ |

Projector display pages (`/board/display`, `/minigames/display`) are Admin-only — no Hub access.

---

### 8.6 Hub Migration Order

Implement in this order — simplest to most relational:

1. `InfoPage` — no foreign keys beyond `UpdatedByUserId`
2. `StaffMember` — optional `LinkedUserId` FK
3. `Announcement` — `AuthorId` FK to User
4. `ScheduleEvent` + `ScheduleEventGroup` — most relational, bridge table

Each is independently deployable as its own EF Core migration.

---

### 8.9 Progressive Web App (PWA) Support

**Goal:** Staff can install the app to their iPhone or Android home screen and launch it in standalone mode. Removes the "remember the URL" problem and gives the app a native feel during camp.

> This is an **installability and launch experience** layer, not an offline architecture. Blazor Server requires a live connection to function. The PWA layer does not change this.

#### 8.9.1 Capability Scope

| Capability | In Scope | Notes |
|---|---|---|
| Home screen install prompt | ✅ | iOS and Android |
| Standalone launch (no browser chrome) | ✅ | Full screen, no address bar |
| App icon on home screen | ✅ | Uses CCN logo asset |
| Splash screen on launch | ✅ | Configured via manifest |
| Offline fallback page | ✅ | Static "You're offline" page — not full offline mode |
| Last-known-good score snapshot | ✅ | Cached via localStorage on every successful load |
| Connection quality indicator | ✅ | Header indicator: green/yellow/red per SignalR state |
| Optimistic UI on transactions | ✅ | Update display before server confirms, reconcile on response |
| Background sync / offline transactions | ❌ | Out of scope — Blazor Server requires connection |
| Push notifications | ❌ | Deferred to v1.1 — see §9.4 |
| App store listing | ❌ | Not needed — direct install via browser |

#### 8.9.2 Implementation

**Web App Manifest** (`wwwroot/manifest.json`):
```json
{
  "name": "Camp Clot Not — Super Party '26",
  "short_name": "Clot Not",
  "description": "Staff dashboard for Super Clot Not Party 2026",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#F2ECD8",
  "theme_color": "#F2ECD8",
  "orientation": "portrait-primary",
  "icons": [
    { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "/icons/icon-512-maskable.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

**`_Host.cshtml` additions (Apple-specific — required in addition to manifest):**
```html
<link rel="manifest" href="/manifest.json" />
<meta name="theme-color" content="#3d0066" />
<meta name="apple-mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
<meta name="apple-mobile-web-app-title" content="Clot Not" />
<link rel="apple-touch-icon" href="/icons/icon-192.png" />
```

**Service worker** (`wwwroot/service-worker.js`): Network-first for Blazor navigation requests (falls back to `/offline.html` on failure). Cache-first for static shell assets only. No caching of SignalR or API responses. Cache name: `ccn-shell-v2`.

**Offline fallback** (`wwwroot/offline.html`): Static HTML, no Blazor dependency. Shows CCN logo, "You're offline" message, last-known score snapshot from localStorage, and a "Try again" reload button.

#### 8.9.3 Icon Assets

Generate from CCN logo PNG via [PWABuilder](https://www.pwabuilder.com) or [RealFaviconGenerator](https://realfavicongenerator.net):

| File | Size | Purpose |
|---|---|---|
| `icon-192.png` | 192×192 | Android home screen, manifest standard |
| `icon-512.png` | 512×512 | Android splash, high-res |
| `icon-512-maskable.png` | 512×512 | Android adaptive icon (logo centered in inner 80%) |

#### 8.9.4 Install Experience

**Android Chrome:** Automatic install banner once PWA criteria are met. Staff can also tap browser menu → "Install app." Railway HTTPS satisfies the requirement automatically.

**iOS Safari:** No automatic prompt — Apple does not support `beforeinstallprompt`. Staff must manually: Safari → Share → "Add to Home Screen." Include in-app guidance (§8.9.5).

#### 8.9.5 In-App Install Guidance

Dismissible one-time banner shown on first login per device:

- **Android:** Intercept `beforeinstallprompt` via JS interop. Banner: "📲 Install this app — tap Install for quick access during camp." Triggers native prompt. Dismissed state in `localStorage`.
- **iOS:** Detect iOS Safari via JS interop user agent check. Banner: "📲 Add to your home screen — tap Share → Add to Home Screen." Include Share icon inline. Dismissed state in `localStorage`.

Neither banner blocks UI. Both disappear permanently after one dismissal.

#### 8.9.6 Implementation Order Within v0.5.0

Implement PWA last, after all Hub features are functional. No logic dependencies — manifest, service worker, icons, install tip component. Estimated effort: ~4-6 hours once icon assets are generated.

---

### 8.10 Incident Reports (v0.5.1)

A floating "🚨 Report Incident" button appears on all Hub pages (rendered in `HubSubNav.razor`). Any logged-in user (Admin, Staff, Volunteer) can submit. Only Admin can view submitted reports.

**Form fields:** Date of Incident, Date Completed (auto-today), Persons Involved, Description (including lead-up and responses), Recommended Action. Submitter info auto-captured from auth claims — no signature fields.

**Admin view:** `/hub/incidents` — lists all reports newest-first. Each row: date, submitter name/role, persons involved (truncated), Acknowledge button, Print link. Acknowledged rows show who acknowledged and when.

**Print view:** `/hub/incidents/{id}/print` — uses `PrintLayout` (bare layout, no nav). Mirrors the Children's Harbor paper form: logo top-right, "INCIDENT REPORT FORM" centered title, fields in paper layout order, submitter footer. Print button calls `window.print()` and is hidden via `@media print`.

**System name:** `IncidentReport` / `IncidentReportService` / `/hub/incidents`

---

### 8.11 Sponsors (v0.5.1)

Admin manages a list of event sponsors (name, logo URL, optional website link, sort order). All users can view a Sponsors tab in the Hub.

**Display page:** `/hub/sponsors` — responsive grid of logo tiles. Each tile: logo (constrained 80px height), sponsor name below. If Website is set, the tile is a link. Visible to Admin, Staff, Volunteer.

**Admin CRUD:** `/admin/sponsors` — same table/form panel pattern as `/admin/locations`. Accessible from Admin desktop dropdown and mobile admin sheet.

**Logo storage:** External URL only — no file upload in v0.5.1.

**System name:** `Sponsor` / `SponsorService` / `/hub/sponsors`

---

### 8.12 Shared Dialog Pattern (planned v0.5.2)

All form modals use the same visual shell: `rgba(26,26,26,.65)` backdrop with `animation:fadeIn .2s ease`, centered `ccn-panel` with `animation:popIn .25s ease` and `box-shadow:8px 8px 0 var(--black)`. Currently duplicated across `LogTransactionDialog` and the Report Incident modal in `HubSubNav.razor`.

**Planned:** Extract a `CcnDialog.razor` wrapper component once a third dialog exists, so animation and backdrop styling only live in one place. Individual dialogs declare title and body only.

---

## Part 9 — Scale, Infrastructure Strategy & Long-Term Architecture

**Context:** The CCN app was initially scoped for Camp Clot Not staff (~10-15 concurrent users). This section addresses the forward path to chapter-event scale (150-300 concurrent members) and the infrastructure decisions required to get there without compromising quality or requiring an architectural rewrite.

**Cost benchmark:** The chapter currently pays ~$1,600/year for Yapp. The target for this platform is ≤$105/year at chapter scale while delivering comparable or superior functionality with full chapter ownership of the platform and data.

---

### 9.1 Guiding Infrastructure Principles

**Professional by default, not by accident.** Every decision should reflect what a competent engineering team would choose for a real production app. The goal is a platform the chapter can trust for years.

**Cost transparency over cost minimization.** The right call is sometimes spending $10/month instead of $0/month. Document the reason, document the cost, make the tradeoff deliberately. Never choose an inferior solution just because it's free.

**No single points of failure at event time.** Camp and chapter events have fixed windows where the app must work. Infrastructure that could fail silently or require manual intervention during an event is unacceptable regardless of cost.

**Additive complexity only.** Every infrastructure layer added must justify its presence. Never add a layer preemptively because it sounds professional.

---

### 9.2 Current Infrastructure Stack (Camp 2026)

| Layer | Service | Cost | Notes |
|---|---|---|---|
| App hosting | Railway hobby | ~$5/mo (event windows only) | Blazor Server + SignalR |
| Database | Railway PostgreSQL | Included | Internal only |
| TLS/HTTPS | Railway (automatic) | Included | No cert management needed |
| Domain | None (Railway default URL) | $0 | Acceptable for camp staff |
| Monitoring | Railway logs + /health | $0 | Serilog stdout |

**Estimated annual cost at camp scale:** $5-15 total.

---

### 9.3 Chapter-Scale Infrastructure Stack (Target)

Each layer below is independently adoptable. Implement before the first chapter event with 150+ members.

#### 9.3.1 Azure SignalR Service — Connection Scaling

**Problem:** Blazor Server holds one SignalR connection per active user in server memory. At 200 concurrent members Railway's single instance becomes the bottleneck, especially during spike moments.

**Solution:** Offload connection management to Azure SignalR Service. Railway app becomes stateless from a connection perspective. Zero changes to components, hub events, or business logic.

```csharp
builder.Services.AddSignalR()
    .AddAzureSignalR(Environment.GetEnvironmentVariable("AZURE_SIGNALR_CONNECTION"));
```

**Cost:**
| Tier | Concurrent Connections | Cost |
|---|---|---|
| Free | 20 | $0 |
| Standard | 1,000 | ~$0.0015/connection/day |

At 200 concurrent users for a full event day: ~$0.30. Across 6 chapter events per year: under $10.

**Verdict:** Implement before first chapter event. Non-negotiable at scale.

#### 9.3.2 Cloudflare — CDN, Domain & DDoS Protection

**Problem:** Railway serves every request including static assets. At peak load this wastes capacity on content that never changes. The Railway default URL looks unprofessional in a member-facing QR code.

**Solution:** Route traffic through Cloudflare. Static assets cached at edge. Railway only handles dynamic requests. A proper domain (e.g. `app.hbdachapter.org`) is presented to members.

**Free tier includes:** Global CDN, DDoS protection, SSL/TLS management, analytics, cache rules.

**Domain cost:** ~$12-15/year via Cloudflare Registrar (at-cost pricing).

**Verdict:** Implement when app goes chapter-facing.

#### 9.3.3 Response Caching — Read-Heavy Hub Endpoints

**Problem:** During peak spikes many members hit the same read-only endpoints simultaneously.

**Solution:** In-memory response caching on Hub feature endpoints. Explicit cache invalidation when admin posts a new announcement.

```csharp
// Appropriate cache durations per content type
Schedule:        60 seconds
Announcements:   15 seconds  // invalidate immediately on new post
Info pages:      300 seconds
Staff directory: 120 seconds
```

**Cost:** $0 — application-level caching using existing Railway instance memory.

**Verdict:** Implement alongside v0.5.0 Hub features.

#### 9.3.4 Railway Vertical Scaling — Event Window Strategy

**Problem:** Railway hobby instance has modest resources. Known large events have predictable load spikes.

**Solution:** Scale Railway instance up 24 hours before the event, back down after. Manual operation in the Railway dashboard — no code changes.

| Instance | RAM | Monthly Rate | 3-day Event Cost |
|---|---|---|---|
| Hobby | 512MB | $5 | ~$0.50 |
| Pro 1GB | 1GB | $20 | ~$2.00 |
| Pro 2GB | 2GB | $40 | ~$4.00 |

With Azure SignalR handling connections, Pro 2GB comfortably handles 300+ concurrent users.

**Verdict:** Establish as a documented pre-event runbook item (§9.7).

---

### 9.4 Push Notifications — Implementation Path

Push notifications are the single most impactful Yapp feature not yet in scope. This section captures the implementation path for v1.1 once member identity is ready.

**Delivery chain:**
```
Admin posts announcement (Blazor Server)
    ↓
AnnouncementService calls PushNotificationService
    ↓
ASP.NET Background Service reads stored push subscriptions from DB
    ↓
Sends via Web Push Protocol (WebPush NuGet package + VAPID keys)
    ↓
Browser Push Services (FCM for Android, APNs for iOS)
    ↓
Device receives notification — app does not need to be open
```

The Blazor Server frontend is not involved in delivery. The service worker in `wwwroot/service-worker.js` handles the incoming push event and displays the notification natively.

**New data model (when implemented):**
```
PushSubscription — SubscriptionId, UserId, Endpoint, P256DH, Auth,
                   Platform (enum: Android|iOS|Desktop), CreatedAt, IsActive
```

**iOS caveat:** Web Push on iOS requires iOS 16.4+ and the app must be installed as a PWA. Does not work in a Safari browser tab. Members who have installed the app receive push correctly.

**Cost:** WebPush NuGet package + Google FCM + Apple APNs = $0.

**Prerequisite:** Member identity system must exist before push subscriptions are meaningful. Push is a v1.1 feature.

**React vs. Blazor Server on push:** React/Next.js makes service worker wiring cleaner, but the backend sender (ASP.NET background service + WebPush NuGet) is identical either way. Push notifications are not a reason to change the frontend architecture.

---

### 9.5 Member Identity & Role Expansion (Future)

For chapter-event use at scale, the current three-role model (Admin/Staff/Display) needs expansion:

- **Member** role: read-only access to Hub features (Schedule, Announcements, Directory, Info). No transaction capability.
- Self-registration or admin-invited accounts
- Member-specific data: group assignment at a chapter event, personal schedule view
- QR code install flow landing page at `/welcome` with platform-specific install instructions

This is a post-camp v1.1 design task. The data model and service layer should be designed with this expansion in mind from the start — specifically, `LinkedUserId` on `StaffMember` and the `Role` enum on `User` should accommodate a `Member` value without schema changes.

---

### 9.6 Estimated Annual Cost at Chapter Scale

| Layer | Service | Annual Cost |
|---|---|---|
| App hosting | Railway Pro (event windows) + Hobby (baseline) | ~$80 |
| Database | Railway PostgreSQL | ~$0 (included) |
| SignalR scaling | Azure SignalR Service Standard | ~$10 |
| CDN + domain | Cloudflare + domain registration | ~$15 |
| Push notifications | WebPush + FCM + APNs | $0 |
| Monitoring | Railway logs + /health | $0 |
| **Total** | | **~$105/year** |

Against $1,600/year Yapp: equivalent or superior functionality at ~6% of the cost, with the chapter retaining full ownership of the platform, data, and roadmap.

---

### 9.7 Pre-Event Infrastructure Runbook (Chapter Events)

To be executed by Tyler or designated chapter IT lead before each chapter event.

**T-7 days:**
- [ ] Verify Railway staging deployment is current with main branch
- [ ] Confirm `AZURE_SIGNALR_CONNECTION` is set in Railway environment variables
- [ ] Seed event-specific data (schedule, announcements, scripted sequences if applicable)
- [ ] Verify `/health` endpoint returns 200 on staging

**T-24 hours:**
- [ ] Scale Railway instance to Pro 2GB via Railway dashboard
- [ ] Smoke test all Hub features on production URL
- [ ] Verify push notification delivery on one iOS and one Android device (when implemented)
- [ ] Confirm `/health` returns 200 on production
- [ ] Generate and distribute QR code pointing to production URL (or `/welcome` install page)

**T-1 hour:**
- [ ] All admin staff confirm app installed on their devices
- [ ] SignalR connection verified on projector display (if applicable)
- [ ] Railway logs open in a browser tab for live monitoring

**Post-event (T+24 hours):**
- [ ] Scale Railway instance back to Hobby tier
- [ ] Export transaction/data log CSV if applicable
- [ ] Archive event data per end-of-season flow

---

### 9.8 What This Stack Is Not

Explicit exclusions and their justifications:

**Not Kubernetes.** The app does not need container orchestration. Railway handles deployment and scaling. Adding Kubernetes adds operational complexity with no meaningful benefit at this scale.

**Not microservices.** The service/repository architecture inside the Blazor app is the correct level of separation. Splitting into separate deployed services adds network hops and distributed tracing requirements not justified until the app has multiple distinct client types.

**Not managed Redis.** In-memory response caching is sufficient for this read pattern. Redis adds cost and an external dependency for a problem that doesn't require it at this scale.

**Not a third-party notification service (Twilio, OneSignal, etc.).** The WebPush protocol handles push natively at no cost. Third-party services add per-message costs and vendor dependency for a capability that can be owned outright.

Each exclusion is a deliberate decision, not an oversight. Revisit only if a specific demonstrated need emerges.

---

*Camp Clot Not · Super Party 2026 · Requirements, Asset Spec & Architecture Guide · Tyler Blair*  
*Last updated: 2026-05-27 — v0.5.1: §8.10 Incident Reports, §8.11 Sponsors, §8.12 shared dialog pattern; §8.4 seed slugs updated; §8.5 permission matrix updated; data model updated; milestone added*
