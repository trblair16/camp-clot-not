# Camp Clot Not — Super Party 2026
## Software Requirements & Animation/Asset Specification
**Prepared by Tyler Blair | April 2026 | Claude Code Handoff Document**

> This document is the primary reference for Claude Code. All pending decisions are marked ⚠️ PENDING and resolved decisions are marked ✅ DECIDED. After the Monday planning meeting, update this file directly to resolve pending items and commit as a chore issue close.

---

## Document Purpose

This document serves as the primary requirements and design specification for the Camp Clot Not web application. It covers functional requirements, data model, UI/UX behavior, animation specifications, asset guidance, infrastructure, versioning standards, and a legacy schema analysis from the 2021 capstone.

A working React prototype mockup exists in the project files (ccn-mockup-v2.jsx) and serves as the primary visual reference. The mockup captures structure and logic; the Blazor implementation should use proper assets and animations described in Part 2.

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

> ✅ DECIDED: No Flask middleware in v1. Blazor Server talks directly to PostgreSQL via EF Core using a clean service/repository layer. Flask or .NET Minimal API extracted in v2 when a second client exists.

> ✅ DECIDED: PostgreSQL on Railway — not SQL Server/Azure. Eliminates cloud hosting cost and billing surprise risk.

### 1.2 User Roles & Permissions

| Role | Permissions |
|---|---|
| Admin (Tyler / Katelyn / Vicki) | Full access: pre-load board, configure themes, award coins/stars, void any transaction, manage groups, trigger block hit, manage user accounts |
| Staff / Volunteer | Award coins and stars to groups, add optional note, view dashboard and board. Cannot void others' transactions or access admin config. |
| Display (Projector) | Read-only: board, leaderboard, current scores, block hit animation. No transaction capability. |

> ⚠️ PENDING: Should display/projector mode require a login, or be a PIN-protected URL (/display?pin=XXXX)? Recommendation: PIN-protected URL — no full account needed for the projector.

### 1.3 Theme System

The application supports a swappable theme layer so the same competition core can be re-skinned each year. Theming controls:
- Group/team names, colors, and token art
- Location names (pool, med hut, dining hall, cabins)
- Currency names and icons (Coins + Stars this year)
- Board space labels and iconography
- Overall color palette and header branding

For 2026, the active theme is Super Mario Party. All Mario-specific naming is defined in the theme layer, not hardcoded in logic. See Part 7.10 for the full glossary of display names vs. system names.

### 1.4 Groups

Competition is tracked at the group level only. Individual camper point tracking has been deliberately removed to reduce complexity.

Each group has:
- Name, short name, color, character/token asset
- Coin total (computed from non-voided coin transactions)
- Star total (computed from non-voided star transactions)
- Current board position (space index)
- Cabin assignment (display reference)

> ⚠️ PENDING: How many groups for 2026? Mario Party naturally suits 4 teams. Cabin groupings and board token design depend on this number.

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

> ⚠️ PENDING: Stars via staff judgment only, or can groups also spend coins to purchase stars? Recommendation: staff awards stars directly. Avoids coin-spending workflow burden on volunteers.

> ⚠️ PENDING: Coin/star weighting for final standings? e.g. stars-first, tiebreak on coins. Affects leaderboard sort logic.

### 1.6 Transaction System

Every coin and star movement is a transaction. Append-only — nothing deleted, only voided.

Fields: ID, GroupId, CurrencyType (coins/stars), Amount (positive or negative), Note (optional), LoggedBy, CreatedAt, VoidedAt (nullable), VoidedBy (nullable)

- Any staff member can post a transaction
- Admins can void any transaction — reverses the amount and marks the record voided
- Voided transactions stay visible in the log (audit trail) but are visually distinguished
- No approval queue — post immediately, correct via void

> ✅ DECIDED: No approval/request workflow. Keeps volunteer burden minimal. Admins correct via void after the fact.

### 1.7 Board Game

A visual game board displayed on the projector during gatherings.

- Winding snake/rectangular path shape — see ccn-mockup-v2.jsx for reference layout
- Spaces pre-designed by admin before camp to match the week's schedule
- **Space type is determined by the Activity linked to the space — there is no SpaceType enum.**
  `BoardSpace.ActivityId` → `Activity.ActivityTypeId` → `ActivityType.CategoryId` → `ActivityTypeCategory.SystemName`
  The category SystemName drives space rendering behavior (icon, color, effect).
- Each group has a token sitting on their current board position (`GroupBoardPos`)
- Board positions persist in DB
- SVG-based rendering with proper icon assets (not emoji)

> ✅ DECIDED: No SpaceType enum. Replaced by ActivityType + ActivityTypeCategory system. See schema redesign spec.

> ⚠️ PENDING: How many board spaces? Mockup uses 20. Should match camp day/activity count. Confirm with Katelyn/Vicki. Determines how many Activity rows to seed.

> ⚠️ PENDING: Should the board/leaderboard be visible to campers all week on the projector, or only during specific gathering moments?

### 1.8 Block Hit Mechanic

The theatrical centerpiece of each group gathering.

- Pre-scripted by admin (Tyler) before camp — each group has a predetermined destination for each day
- The number shown is calculated from current position to destination (accurate to scripted result, appears random)
- Admin triggers the animation from a tablet during the gathering
- Block hit is an overlay/modal on the Board page — not a separate page
- After number reveal, token animates step-by-step across the board to destination space
- Admin can update scripted destinations on the fly if schedule changes

> ✅ DECIDED: Pre-scripted, not truly random. Ensures balanced outcomes and staff control.

> ✅ DECIDED: Block hit animation plays on the projector display via SignalR broadcast, triggered from admin tablet. Architecturally significant — plan SignalR hub message types before building this feature. Suggested hub events: BlockHitTriggered, BlockHitNumberRevealed, TokenMoveStep, TokenMoveDone, ScoresUpdated.

### 1.9 Mini-Games / Evening Challenge Picker

Dedicated page for Minute to Win It and group evening challenges.

- Full list of challenges visible on screen at all times
- Pointing hand icon cycles through the list, slowing to a stop on the result
- Challenge list pre-configured by admin; result is pre-scripted (same philosophy as block hit)
- Designed for projector display during evening gatherings
- After reveal, staff can log coins/stars directly from this page

### 1.10 Attendance Tracking

> ✅ DECIDED: Deferred to v2. Build competition system fully for 2026 camp first. Add Attendee/EventAttendance tables in a post-camp sprint.

---

## Part 2 — Animation & Asset Specification

> The React mockup uses CSS animations and emoji as structural placeholders. This section describes what each animated moment should look and feel like in the production Blazor app.

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

### 2.2 Block Hit Animation Sequence

**Phase 1 — Idle:** Block floats with gentle CSS translateY loop. Sparkle/shimmer particles. Pulsing shadow.

**Phase 2 — Hit Trigger:** Block squishes down (scaleY compress) then springs upward. Coin/star pops from top. Flash to white/yellow. Number roll begins immediately.

**Phase 3 — Number Roll:** Large number cycles rapidly (slot-machine flip animation). Starts fast, decelerates over ~2 seconds. Block color shifts yellow → red as it slows.

**Phase 4 — Number Reveal:** Hard stop — no easing. Block shakes, flashes red/gold. Number scales up (popIn: 0.6 → 1.1 → 1.0). Confetti burst. Text: "[Group name] moves X spaces!"

**Phase 5 — Token Movement:** Token lifts off current space. Travels space-by-space along pre-calculated SVG path coordinates. Brief hop + 300ms pause on each intermediate space. Flash on Star/Bowser spaces when passing. Final landing: larger bounce + space glow + floating icons matching space type.

Implementation: pre-calculate SVG coordinates per space. Use JS-stepped setTimeout chain to move token through each waypoint. CSS transition handles the smooth movement between coordinates.

**CRITICAL: The full animation sequence plays on the projector (/display route) via SignalR broadcast. Tyler's tablet is the trigger only.**

### 2.3 Mini-Game Spinner

- Full list of challenges visible at all times as styled cards/tiles
- Hand icon appears on left side of list
- On trigger: hand moves down the list highlighting each item as it passes
- Highlighted item: background flashes, text scales up, brief glow
- Movement starts fast, decelerates (ease-out cubic) over ~3 seconds
- Non-highlighted items dim slightly during spin
- Hand stops pointing at final item — item stays highlighted
- Result state: selected item expands/zooms, confetti burst, "Tonight's Challenge!" header

### 2.4 Dashboard & Leaderboard

> ✅ DECIDED (2026-05-07): Reference images from Mario Party Superstars confirm the correct layout pattern. See §2.4.1 for the target design. Current implementation (grid of cards) is a known deviation — tracked for v0.3.1 or later.

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

### 2.5 Board Visual Design

- Winding snake/rectangular path rendered as thick rounded-corner ribbon
- Each space: distinct rounded shape in its type color with SVG icon
- Space numbers shown small for admin reference
- Spaces with tokens: subtle glow or highlight
- Tokens: circular character badge art, drop shadow, gentle idle bob
- Multiple groups on same space: tokens offset slightly

### 2.6 Projector / Display Mode

- Board fills ~70% of screen; leaderboard sidebar ~30%
- Font sizes scaled for 1080p at 10-15 feet: min 24px body, 48px+ for scores
- Reduced UI chrome — no transaction buttons, no admin controls
- Auto-refresh via SignalR real-time push
- Dark background theme
- Graceful reconnect — no blank screen on connection drop

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

> Use as a starting reference. Refine as implementation progresses. Computed totals (coins, stars) should be derived from the Transaction log, not stored directly.

```
Theme           — ThemeId, Name, Year, CurrencyName1, CurrencyName2, IsActive
Group           — GroupId, ThemeId, DisplayName, ShortName, Color, TokenAssetPath, CabinDisplayName
BoardSpace      — SpaceId, ThemeId, SpaceIndex, SpaceType (enum), Label, IconAssetPath, XPos, YPos
GroupBoardPos   — GroupId, SpaceIndex, UpdatedAt
Transaction     — TxId, GroupId, CurrencyType (enum), Amount, Note, LoggedBy, CreatedAt, VoidedAt?, VoidedBy?
ScriptedBlockHit— ScriptId, GroupId, CampDay, DestinationSpaceIndex, IsTriggered
ScriptedMiniGame— ScriptId, CampDay, ActivityLabel, IsTriggered
User            — UserId, DisplayName, Email, PasswordHash, Role (enum: Admin/Staff/Display), IsActive
CamperAward     — AwardId, GroupId, RecipientName, AwardType (enum: Named/BigStick/Branch), BonusStars, AwardedAt
CampSeason      — SeasonId, ThemeId, Year, IsLocked, LockedAt?, LockedBy?
```

> ⚠️ PENDING: Attendance tables — confirmed deferred to v2.

---

## Part 4 — Open Decisions Summary

All items below need resolution at or immediately after the Monday planning meeting. Create each as a `blocked` GitHub issue. Update to `ready` after the meeting and assign to the appropriate milestone.

| Decision | Blocks |
|---|---|
| How many groups for 2026? | Group data model, board token design, space count |
| Stars: staff judgment only, or coin exchange too? | Transaction UI, currency rules |
| Coin/star weighting for final standings? | Leaderboard sort logic |
| Board visible all week or only at gatherings? | Display mode requirements, auto-refresh |
| How many board spaces? | Board layout, scripted hit configuration |
| Display mode: login or PIN-protected URL? | Auth/routing for projector view |
| How are staff accounts created? | Auth flow, pre-camp setup |
| Session timeout behavior? | ASP.NET session configuration |
| Offline/connectivity failure behavior? | Error handling strategy |
| How is the winner officially declared? | End-of-camp flow, score lock |

---

## Part 5 — Infrastructure & Deployment

### 5.1 Hosting — Railway.app

> ✅ DECIDED: Host on Railway.app — not Azure. Azure caused billing surprises in 2021 and difficult support experiences. Railway is simpler and usage-based.

- Automatic HTTPS/TLS termination — no certificate or binding configuration needed
- Free public URL: yourapp.up.railway.app — no domain purchase required
- Estimated cost for 2-week camp window: $2-4, likely within free $5/month credit
- WebSocket support built in — required for Blazor Server SignalR
- Two Railway services: Blazor app (public URL) + PostgreSQL (internal only)
- Staging environment mirrors production for periodic deploy verification

### 5.2 Database — PostgreSQL

> ✅ DECIDED: PostgreSQL on Railway — not SQL Server.

- Schema design, querying, and relational modeling concepts are identical to SQL Server
- EF Core with Npgsql provider abstracts the engine — connection string is the only change
- Install PostgreSQL locally via postgresql.org installer (Windows) or Docker Desktop
- Use DBeaver (free, Windows) as local DB GUI — similar feel to SSMS, connects to PostgreSQL
- EF Core migrations handle all schema creation and updates

### 5.3 Application Architecture

> ✅ DECIDED: v1 — Blazor Server + EF Core directly. Flask middleware deferred to v2.

```
Blazor Server UI components
        ↓
Service layer (business logic — this IS the logical API)
        ↓
Repository layer (EF Core + Npgsql)
        ↓
PostgreSQL (Railway in prod, local in dev)
```

v2 extraction path: wrap the service layer behind HTTP (Flask or .NET Minimal API) when a second client needs it. The service layer code does not change — only an HTTP routing wrapper is added.

### 5.4 Critical Configuration — Claude Code Must Implement

**PORT Environment Variable:**
```csharp
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "5000"}");
```
Railway assigns a dynamic port via the PORT env var. Never hardcode a port. This is the most common cause of Blazor apps working locally but failing on Railway.

**HTTPS — Let Railway Handle It:**
- Do NOT configure HTTPS inside the Blazor app for production
- Railway terminates TLS externally — the app runs HTTP internally
- Disable or environment-gate ForceHttpsRedirection (dev only)
- This avoids the IIS/Kestrel binding mismatch experienced in the prior work Blazor project

**Environment-Specific Config:**
- All secrets and env-specific values from environment variables — never hardcoded
- Required env vars: DATABASE_URL (Railway injects automatically), JWT/auth secret, any API keys
- appsettings.Development.json for local dev; Railway dashboard for staging/prod
- Developer exception pages: enabled in Development, disabled in Production

**SignalR / WebSocket Resilience:**
- Railway supports WebSockets natively — verify during dry run
- Implement graceful reconnect UI: "Reconnecting..." indicator, never a blank screen
- Test by deliberately killing and restarting the Railway service while the app is open
- The block hit animation must survive a reconnect without corrupting board state

**Never commit secrets:**
- .env must be in .gitignore from the very first commit
- The 2021 repo has a committed .env file — this pattern is explicitly prohibited in 2026

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
| MAJOR | Breaking architectural change — e.g. 2.0.0 when Flask API extracted and system becomes multi-client |
| MINOR | New feature shipped — e.g. 1.1.0 when attendance tracking added post-camp |
| PATCH | Bug fix or small non-breaking change — e.g. 1.0.1 for SignalR reconnect fix found during dry run |

Camp 2026 ships as v1.0.0. Release candidates tagged v1.0.0-rc.1 etc. during dry run period.

> ✅ DECIDED: SemVer — not calendar-based versioning. Single customer, no implementation-specific build variants.

### 6.2 Branching Strategy

```
main        — always deployable, protected, Railway production deploys here
dev         — integration branch, Railway staging deploys here
feature/N-name   — new feature work branched off dev
fix/N-name       — bug fix branched off dev
chore/N-name     — config, deps, refactoring
```

**Branch Protection Rules — Configure in GitHub Repo Settings at Project Start:**
- main: direct pushes blocked — all changes must come through a PR
- main: force pushes blocked — history cannot be rewritten
- Violations are blocked at the Git level, not just a convention

**PR & Merge Flow:**
1. Create a GitHub issue
2. Branch off dev using naming convention, referencing issue number
3. Work and commit frequently with meaningful messages
4. Open PR into dev — body includes "Closes #N" to auto-close issue on merge
5. Self-review PR before merging
6. At milestone boundaries: PR dev into main and tag the release

### 6.3 Tags & Releases

Tags are immutable — they permanently point to that exact commit. Every past version is always recoverable.

```bash
git tag v1.0.0 && git push origin v1.0.0
```

- Create a GitHub Release from the tag with release notes
- Rolling back production = point Railway deployment at last known good tag
- Reverting locally: `git checkout v0.3.0`

### 6.4 Issues & Labels

| Label | Meaning |
|---|---|
| feature | New user-facing functionality |
| bug | Something broken or behaving incorrectly |
| chore | Config, deps, refactoring — no user-facing change |
| blocked | Waiting on a pending decision — cannot proceed |
| ready | Decision made, cleared to implement |
| camp-critical | Must be complete before camp — cannot slip |
| post-camp | Desirable but not blocking camp delivery |

All pending decisions from Part 4 should be created as `blocked` issues immediately. Update to `ready` after Monday's meeting.

### 6.5 Milestones & Delivery Schedule

Camp is June 20-25, 2026. Milestones function like sprint code-cut targets.

| Target | Milestone | Scope |
|---|---|---|
| ~May 4 | v0.1.0 — Foundation | Repo + Railway setup, PostgreSQL + EF Core, auth, Group/Transaction CRUD, branch protection |
| ~May 14 | v0.2.0 — Competition Core | Leaderboard, coin/star system, transaction log, void flow, staff dashboard, role-based access |
| ~May 28 | v0.3.0 — Board & Block Hit | SVG board, pre-scripted block hit, step-by-step token movement animation |
| ~June 7 | v0.4.0 — Mini-Games & Display | Evening spinner with hand animation, projector display mode, admin config screens |
| ~June 7-8 | v1.0.0-rc — Dry Run | Full dress rehearsal. Staff test on real devices. Projector verified. Issues logged. |
| ~June 13 | v1.0.0 — Camp Ready | Dry run issues resolved. Production seeded. Staff briefed. One week buffer. |
| June 20 | 🏕️ CAMP | Super Clot Not Party '26 is live |

> Monday meeting decisions directly unblock v0.1.0. The data model cannot be finalized until group count, currency rules, and board space count are confirmed.

### 6.6 REQUIREMENTS.md Token Efficiency

This file IS the REQUIREMENTS.md. Reference it as @REQUIREMENTS.md in all Claude Code sessions. Do not re-upload the docx. After Monday's meeting, update pending items directly in this file and commit as a chore issue close.

---

## Part 7 — Gaps, Open Items & Legacy Schema Analysis

### 7.1 Legacy Schema Analysis

The 2021 hbda_tracking schema tracked individual attendee points across events. 2026 shifts to group-level only.

| 2021 Table | 2026 Status |
|---|---|
| event | RETAINED — Maps to Event/Theme concept. type field is a useful pattern. |
| group | RETAINED & REDESIGNED — total_points splits into coin_total/star_total computed from Transaction log, not stored directly. |
| attendee | DEFERRED TO V2 — Simple structure, restore when attendance tracking is added. |
| attendee_points | REPLACED — Individual scoring bridge table. Replaced by group-level Transaction table. |
| pointlog | REPLACED & IMPROVED — Direct ancestor of Transaction table. Adds currency_type, voided_at/voided_by (replaces status flag), removes attendee_id, adds camp day reference. |
| user | RETAINED & EXTENDED — Core structure solid. Password storage must upgrade to ASP.NET Identity or BCrypt. |
| access | SIMPLIFIED — Flatten to Role enum (Admin/Staff/Display) on User table rather than separate lookup. |

> ✅ DECIDED: Do not copy the 2021 schema into EF Core migrations. Use as conceptual reference only. Build models fresh.

**CRITICAL — .env file:** The 2021 repo has a .env file committed to version control. Add .env to .gitignore from the very first commit in 2026. All secrets go in Railway environment variables only.

### 7.2 Authentication & Session Management

> ⚠️ PENDING: How are staff accounts created? Recommendation: Admin pre-creates all accounts before camp with role assignment. Individual accounts provide audit trail per volunteer.

> ⚠️ PENDING: Session timeout? Recommendation: 24-hour session during camp. Default ASP.NET timeout is too short — configure explicitly.

> ⚠️ PENDING: Projector display auth — login or PIN URL? Recommendation: /display?pin=XXXX

**Do NOT carry forward from 2021:**
- Weak/plain password hashing — use ASP.NET Identity or BCrypt
- Secrets in startup scripts or committed .env files
- CORS configured for open access — lock to Railway app domain only

### 7.3 Admin Setup & Data Seeding Flow

> ⚠️ PENDING: How does Tyler load pre-camp config? Recommendation: seed script for stable config (groups, board layout) + admin UI for scripted sequences (needs last-minute adjustment capability).

**Required pre-camp admin workflows — must be built:**
- Create/edit groups (name, color, token, cabin)
- Create/arrange board spaces (type, position, label, icon)
- Load scripted block hit sequence (per group, per day: destination space index)
- Load scripted mini-game sequence (per day: activity label)
- Create user accounts with role assignment
- Assign camper awards to groups as bonus stars before closing ceremony
- Score lock — freeze transactions before closing ceremony
- Archive/reset — preserve history, reset scores for next year

### 7.4 Projector Display Mode — Full Spec

**Layout & Behavior:**
- Route: /display (or /display?pin=XXXX)
- Board: ~70% of screen. Leaderboard sidebar: ~30%
- No transaction controls, no admin UI — read-only
- Font sizes for 1080p at 10-15 feet: min 24px body, 48px+ scores
- Real-time push via SignalR — not polling
- Dark background theme

**Block Hit on Projector — ARCHITECTURALLY SIGNIFICANT:**
- Animation MUST play on /display via SignalR broadcast
- Tyler's tablet is the trigger device only
- Requires SignalR hub broadcasting to all connected clients
- Plan hub message types before building: BlockHitTriggered, BlockHitNumberRevealed, TokenMoveStep, TokenMoveDone, ScoresUpdated
- Cannot be easily retrofitted — design the hub with these events from day one

> ✅ DECIDED: Block hit animation plays on projector via SignalR broadcast triggered from admin tablet.

> ⚠️ PENDING: Should leaderboard sidebar always be visible, or should display auto-cycle between full-screen board and full-screen leaderboard? Recommendation: sidebar always visible.

### 7.5 Offline & Connectivity Degradation

> ⚠️ PENDING: What happens when a volunteer loses connection mid-transaction? Recommendation for v1: fail loudly with a clear non-technical error and a retry button. Document as a known limitation. Offline sync queue is out of scope.

**Resilience requirements:**
- SignalR reconnect: automatic with visible "Reconnecting..." indicator — never a blank screen
- Transaction failure: clear user-visible error with retry — never silent failure
- Session recovery: phone sleep/wake must not require re-login during camp hours
- Projector recovery: /display must auto-resume correct board state on reconnect without manual reload

### 7.6 End-of-Camp Flow

> ⚠️ PENDING: How is the winner officially declared? Recommendation: Tyler performs a final review pass, then triggers Score Lock before the ceremony. Leaderboard at time of lock is the official result.

**End-of-camp features to build:**
- Score lock — admin action freezing all transactions
- Winning group celebration screen on projector
- Final standings screen — clean read-only summary
- Transaction export — CSV download for Vicki's chapter records
- Archive/reset — preserve history, reset for next year

### 7.7 Device & Browser Support

| Target | Notes |
|---|---|
| iOS Safari 15+ | Highest-risk browser for Blazor Server WebSockets — test early and specifically |
| Android Chrome 90+ | Secondary volunteer device |
| Desktop Chrome/Edge | Tyler admin device and projector machine |
| Min staff UI width | 375px (iPhone SE) |
| Projector display | 1920x1080 minimum |

**Priority: Test SignalR on iOS Safari before the dry run. Most common Blazor Server failure point in production.**

### 7.8 Error Handling & Observability

- Use Serilog for structured .NET logging — output to Railway's stdout log stream
- All unhandled exceptions must log with enough context to diagnose remotely
- Add /health endpoint — HTTP 200 confirming app and DB are reachable
- Transaction failures must surface a user-visible error — never silent
- Do NOT add external observability tooling (Datadog, Sentry) for v1 — Railway logs are sufficient

### 7.9 Accessibility Baseline

- Color contrast: WCAG AA (4.5:1) — some campers/volunteers may have visual impairments
- Touch targets: minimum 44x44px — staff tapping phones in outdoor lighting
- Group identity must not rely on color alone — pair with text label or icon
- Projector display: high contrast, 48px+ for key information
- No formal audit required for v1, but these rules must be followed during implementation

### 7.10 Glossary — Theme Names vs. System Names

Claude Code must use system names for all variables, routes, DB fields, and components. Display strings are loaded from Theme configuration — never hardcoded in logic.

| Display Name (Mario / Camp Theme) | System Name (Code / DB) |
|---|---|
| Coins | CurrencyType.Primary / coin_total |
| Stars | CurrencyType.Prestige / star_total |
| Block Hit | ScriptedBlockHit / TriggerBlockHit() |
| Board Space | BoardSpace |
| Bowser Space | SpaceType.Penalty |
| Star Space | SpaceType.Prestige |
| Mini-Game Space | SpaceType.Challenge |
| Blooper Bay | LocationType.Pool (theme display string) |
| Dr. Mario's Medic Station | LocationType.MedHut (theme display string) |
| Cabin names (Mushroom Manor, etc.) | Group.CabinDisplayName (from Theme config) |
| Group names (Mario's Mushroom Crew, etc.) | Group.DisplayName (from Theme config) |
| Evening Challenge / Minute to Win It | ScriptedMiniGame |
| Big Stick Award | AwardType.BigStick |
| Branch Award | AwardType.Branch |
| Named Camper Award | AwardType.Named |

### 7.11 Manual Test Checklist — Required Before v1.0.0-rc Tag

> ✅ DECIDED: Manual testing only for v1. The dry run is the integration test gate. Unit tests are a post-camp v1.1 consideration.

All items below must pass before the v1.0.0-rc tag is applied:

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

---

*Camp Clot Not · Super Party 2026 · Requirements & Asset Spec · Tyler Blair*
