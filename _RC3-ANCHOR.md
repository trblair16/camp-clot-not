# RC3 Session Anchor — June 16 2026
> Temporary scratch file. Move to vault or delete after camp.

---

## Branch / Workflow
- No branch or issue created yet for RC3 — must open GitHub issue first, then `feature/N-v100rc3-description`
- **NEVER merge locally — always through PRs**
- All work so far is uncommitted edits to `Schedule.razor` on `dev`

---

## What Is Done (this session)

### Schedule.razor — all uncommitted, on dev
- **Title-first display:** `ev.Title` always shows bold; linked Activity name shows as small italic caption underneath. Fixed in both mobile rows and desktop table. Previously activity name was displacing the title as the primary label.
- **Type badge colors:** Added explicit cases for `Meeting` (#566573 slate), `Task` (#D35400 burnt orange), `Presentation` (#16A085 teal). Purple (`#8E44AD`) is now only for `Activity`. Fallback changed to neutral `#607D8B`. Eliminates the "everything looks purple" problem.
- **Location chip word wrap:** Replaced `flex-shrink:0` with `overflow-wrap:break-word;word-break:break-word` on all location/LocationOther chips. Long names like "SPLASH STAR SPRINGS" no longer overflow off-screen on mobile.
- **Desktop table header:** Renamed "Activity" column → "Event" (it shows the schedule item Title, not the linked Activity entity).

---

## What Is NOT Done Yet (RC3 scope)

### 1. Copy-to-Day Feature (`/admin/schedule`)
**What:** Button on each schedule item row in the admin table. Opens a tweaked version of the existing schedule dialog pre-filled with that item's data, letting Vicki pick a new day/time and tweak details before saving as a new item. No new DB table — just a copy path through the existing `ScheduleService.UpsertAsync`.

**Why:** Vicki has daily repeating items (Pledge of Allegiance, Apply Sunscreen, etc.) that she entered manually each day. This removes that friction and also redirects the "template" instinct she had that led to misusing Schedule Item Types.

**Scope:** Admin-only. Button in `/admin/schedule` table row. Reuse existing form modal with a "Copying from: [Title]" header and day/time pre-selected to next day. Save calls the same Upsert with a new GUID.

---

### 2. Activity Entities for Vicki's Specific Types
**What:** Create proper Activity records (via admin or seed) for: Boating, Swimming, Lake Day. These are currently being misused as Schedule Item Types. Swim Test stays as a Task-type schedule item (one-time only, no activity needed).

**Why:** With the title-first display fix, Vicki can now put "Lake Day" as the Title and link "Boating" or "Swimming" as the Activity. This is the correct model.

**How:** Either Vicki creates them at `/admin/activities`, or add to seed. The `Activities` table already has `LocationId` FK so they can be linked to Lakitu Lagoon / Noki Bay House / Splash Star Springs once those links are confirmed.

**Note on Vicki's current types to redirect (constructively, not urgently):**
- "Lake Day - Boating" → Title: "Lake Day", Activity: Boating, Type: Activity
- "Swimming" → Title: [whatever the session is], Activity: Swimming, Type: Activity
- "Power-Up Station" → stays as title/type — it IS the event name (snack wagon, no location image)
- "Swim Test" → Title: "Swim Test", Type: Task (one-time, no activity link needed)

---

### 3. Board State Fix
**Status: BLOCKED — waiting on Tyler to finish activity-location mapping in prod.**

**Current prod state (queried this session):**
- 14 locations, ALL have images: ACORN PLAINS, DK'S TREE HOUSE, DR. MARIO'S MEDIC STATION, FOREST OF ILLUSION, KOOPA COVE THEATER, LAKITU LAGOON, MR. L'S BUNKER, NOKI BAY HOUSE, RACE TRAK, SHROOM HQ, SPAGHETTI STADIUM, SPLASH STAR SPRINGS, VANILLA DOME, YOSHI'S YURT
- 5 MinuteToWinIt board spaces (2, 6, 10, 13, 17) are **GONE** from prod — cascaded when `Mini-Game` and `Mushroom Kingdom Trivia Showdown` placeholder activities were deleted via admin
- Only 15 of 20 board spaces currently exist in prod
- 4 real activities exist with auto-generated GUIDs (not stable seed GUIDs): Archery, Arts and Crafts (→Yoshi's Yurt), Boating (→Noki Bay House), Swimming (→Splash Star Springs)
- `Yoshi Egg Rescue Relay` exists with its stable seed GUID

**What the fix requires:**
1. Tyler finishes linking activities to locations in prod via `/admin/activities`
2. Local DB password needed to sync prod location images to local dev
3. Seed update: stable GUIDs for new real activities + their LocationIds; re-add the 5 missing board spaces pointing to real activities instead of placeholders
4. Board tiles already have rendering code in place — `BoardComponent.razor` shows location image when `space.LocationId` is set. The chain works. Just needs the data.

**Design decision pending:** Board spaces should show REAL camp activities (the locations with illustrations) as their content, not generic game mechanics (Coin Bonus, Bowser). Star spaces (BoardPrestige) stay. Start stays. The "morning routing" concept — a group lands on a space and it tells them where to go for their first activity.

---

### 4. PWA Nav Bar Height Jump
**Status: Unresolved across multiple previous agents.**

**Symptom:** Bottom nav bar jumps taller on non-dashboard pages vs the dashboard page.

**History:** Multiple CSS fix attempts have failed or made it worse. Previous agents guessed at CSS without reading the component.

**Approach for this session:**
- Read `AppNav.razor` fresh with no assumptions
- Diagnose exactly WHY height differs between pages with sub-menu items vs pages without
- Tyler's suggested escape hatch: normalize all nav items to use the sub-menu pattern, with a hidden/pre-selected ghost item for items that have no sub-menu — if that's what gives consistent height, just do it
- Do NOT try random CSS — diagnose first

---

### 5. Performance / Schedule Tab Slowdown
**Symptom:** App slows noticeably over time without deployments, especially on mobile/PWA. Schedule day tabs become progressively slower and less responsive the more you click them.

**Previous diagnosis:** "Railway lag" — not accepted.

**What to investigate:**
- Blazor circuit memory accumulation — does `OnInitializedAsync` create DbContexts or services that aren't disposed?
- `System.Timers.Timer` instances on Board.razor and BoardDisplay.razor — are they properly disposed when navigating away?
- SignalR hub connections (`HubConnection`) — are they being disposed on navigation? Each hub page `implements IAsyncDisposable` but verify
- Schedule page specifically: `LoadEvents()` is called on init and after saves. Does it re-query everything each tab switch? (It does — `_eventsByDay.GetValueOrDefault(_selectedDay.Value, [])` is computed from cached dict, tab switch doesn't re-query. That part is fine.)
- Check if Blazor enhanced navigation is enabled and whether it's causing component lifecycle issues
- Railway free tier: check if the service is being throttled or if the container is hitting memory limits after running for a while

---

### 6. SVG Group Icon Prompts for ChatGPT
**What:** Current group SVG logos look like colorless outlines of the old emoji icons. Need to create prompts for ChatGPT to generate polished, colored Mario-themed SVG icons.

**Groups:**
- Blue Shell Bandits — color #2980B9 (blue)
- Mushroom Militia — color #27AE60 (green)
- Luma Legends — color #8E44AD (purple)
- (Mini Marios / Group1 exists in seed but removed from active groups)

**Style target:** Bold, colored, Mario Party aesthetic. Not outlines. Should look like real team logos.

**ChatGPT prompts (ask GPT-4o for SVG code output, square viewBox="0 0 100 100"):**

---
**Blue Shell Bandits prompt:**
```
Create an SVG icon (viewBox="0 0 100 100", no width/height attributes) for a kids' camp team called "Blue Shell Bandits." Style it like a Mario Kart blue shell — the iconic spiky blue koopa shell with yellow spines. Use bold, solid fills (not outlines): shell body #2980B9 (medium blue), darkened highlight/shadow areas #1A5E8A, spines/tip #F5C800 (yellow), a white glint spot on the upper shell. The overall shape should read immediately as the Mario Kart "blue shell" weapon. Black border/outline, 2px equivalent stroke. Centered in the square. No text. Output only the SVG code.
```
---
**Mushroom Militia prompt:**
```
Create an SVG icon (viewBox="0 0 100 100", no width/height attributes) for a kids' camp team called "Mushroom Militia." Style it like a Super Mario mushroom power-up — the classic cap-and-stem mushroom shape. Use bold, solid fills: cap #27AE60 (green), white polka dots on the cap, stem/face area #FFFDF0 (cream), simple dot eyes and a small smile. The mushroom should look chunky, bold, and friendly in Mario Party style — not a nature mushroom. Black border/outline. Centered in the square. No text. Output only the SVG code.
```
---
**Luma Legends prompt:**
```
Create an SVG icon (viewBox="0 0 100 100", no width/height attributes) for a kids' camp team called "Luma Legends." Style it like a Luma star companion from Super Mario Galaxy — the small round star-body creature with a star/point shape. Use bold, solid fills: body #8E44AD (purple), small dot eyes in white/black, tiny star highlight glints in white, a slightly darker purple #6C3483 for depth/shadow areas. The shape should read as a glowing round star-being, not a flat geometric star. Black border/outline. Centered in the square. No text. Output only the SVG code.
```
---
**After getting SVG code from ChatGPT:**
Replace files in `CampClotNot/wwwroot/img/groups/`: `blue-shell-bandits.svg`, `mushroom-militia.svg`, `luma-legends.svg`

---

### 7. Reconnect UX (from RC1 plan)
**STATUS: ALREADY SHIPPED** — Confirmed present in `_Layout.cshtml`:
- Custom `#components-reconnect-modal` with three state banners (yellow reconnecting, red failed+countdown, red rejected+auto-reload)
- `blazor.server.js` with `autostart="false"` + exponential backoff config
- `visibilitychange` listener for PWA background/foreground
- No `<ConnectionIndicator />` in `AppNav.razor`

Nothing to do here for RC3.

---

## This Session — What Got Done (June 16 2026, overnight run)

**Branch:** `feature/156-v100rc3-schedule-polish-copy-to-day` — pushed to remote ✓
**Issue:** #156

### Committed
1. **Schedule.razor display polish** — title-first, badge colors, chip word wrap, desktop header rename
2. **Copy-to-day feature** — admin schedule copy button (teal), pre-fills form with next day, reuses UpsertAsync, no new table
3. **Admin schedule badge colors** — synced to match hub schedule (same `ItemTypeBadgeColor` function)
4. **PWA nav height fix** — converted `<a>` tabs to `<button>` elements (uniform element type), added `-webkit-appearance:none`, locked `.nav-bottom-bar` to explicit `height: calc(60px + env(safe-area-inset-bottom))`

### Investigated (no code change)
- **Performance/schedule tab slowdown:** All timer/hub/DbContext disposal is correct. Timers use Stop+Dispose+null. HubConnections have DisposeAsync. DbContexts are all scoped with `using var`. Schedule page doesn't re-query on tab switch. **Most likely root cause: Railway free tier container hitting memory limits after extended uptime** — not a code bug.
- **Reconnect UX:** Already fully shipped in a prior RC. Nothing to do.

### Blocked
- **Board state fix** — still waiting on Tyler to finish activity-location mapping in prod.

---

## Key Session Decisions
- Staying on **Sonnet** (not Opus) for this session — preserve budget for camp hotfixes
- Railway prod password will be changed after this session — do not save it anywhere
- Local DB password unknown — need Tyler to provide before syncing prod data locally
- The temp Npgsql query tool is at `C:\Users\TRBla\AppData\Local\Temp\ccn-query\` if needed again

---

## Reminder: Before Saying "GO" or Starting Work
1. Create GitHub issue first
2. Create `feature/N-v100rc3-description` branch off dev
3. Commit the Schedule.razor changes as the first commit on that branch
4. Then build remaining features on that branch
