# Schema Redesign — HBDA Chapter Platform
**Date:** 2026-04-28  
**Status:** Approved  
**Scope:** Replaces the original CCN-only scaffold schema with a generalized chapter-platform foundation

---

## Context

The original scaffold was scoped to Camp Clot Not 2026 only. The board (April 2026) approved extending the platform to replace the chapter's Yapp subscription (~$1,600/year), covering all chapter events. This redesign builds that foundation while keeping v1 delivery focused on CCN camp (June 20-25, 2026).

**Design principle:** Get the schema right now. Build only CCN features for v1. The schema investment is small; the cost of migrating production data later is not.

---

## Full Schema

### Core Event Structure

```
EventType  — EventTypeId, Name, Description, SystemName
             Rows: CCN, WomensRetreat, AnnualMeeting, MensRetreat, FamilyCamp, ChristmasLuncheon

Event      — EventId, EventTypeId, ThemeId, Name, EffDate, ExpDate, IsActive
             One row per event instance (CCN 2026, CCN 2027, etc.)
             No overlapping active dates for the same EventType (enforced by app logic)

Theme      — ThemeId, Name, Year, Description, LogoAssetPath, ColorPalette, FontConfig
             Cosmetic/display config only — fonts, colors, logo, asset paths
             No competition data hangs off ThemeId directly
```

### Capability System

```
Capability       — CapabilityId, Name, Description, SystemName
                   Rows: BoardGame, CoinShop, MiniGameSpinner, Announcements, Itinerary

EventCapability  — EventCapabilityId, EventId, CapabilityId
                   Per-instance capability assignment
                   Seeded from EventType defaults when admin creates a new Event
                   Admin can toggle capabilities on/off before confirming
```

**Key decision:** `EventCapability` joins to `Event` (not `EventType`) so individual instances can deviate from type defaults (e.g., CCN 2027 without the board game). The EventType provides seed defaults only — not a hard constraint.

### Activity System

```
ActivityTypeCategory  — CategoryId, Name, Description, SystemName
                        e.g. MinuteToWinIt, WaterActivities, Arts, Games, Discussion

ActivityType          — ActivityTypeId, CategoryId, Name, Description, SystemName
                        e.g. CoinGrabRelay, BowserDodgeBattle, CanoeObstacle (under MinuteToWinIt)
                             ArtsAndCrafts, Presentation, Relay (under their categories)

Activity              — ActivityId, EventId, ActivityTypeId, Name, Description
                        Specific activity instances scoped to an Event
```

`ActivityTypeCategory.SystemName` is how code filters activities for feature-specific UI — e.g. the mini-game spinner loads only activities whose type's category is `MinuteToWinIt`.

### Competition / CCN (BoardGame capability)

```
CurrencyType      — CurrencyTypeId, Name, Description, SystemName, Icon
                    Rows: Primary (Coins), Prestige (Stars)

Group             — GroupId, EventId, Name, ShortName, Color, TokenAssetPath, CabinDisplayName

Transaction       — TxId, GroupId, CurrencyTypeId, Amount, Note, LoggedBy,
                    CreatedAt, VoidedAt?, VoidedBy?
                    Append-only — nothing deleted, only voided

BoardSpace        — SpaceId, EventId, ActivityId, SpaceIndex, XPos, YPos, IconAssetPath
                    Space IS the activity — no separate SpaceType mechanic

GroupBoardPos     — GroupId, SpaceIndex, UpdatedAt

ScriptedBlockHit  — ScriptId, GroupId, EventId, CampDay, DestinationSpaceIndex, IsTriggered

ScriptedMiniGame  — ScriptId, EventId, CampDay, ActivityId, IsTriggered
```

### Awards

```
AwardType   — AwardTypeId, Name, Description, SystemName
              Rows: Named, BigStick, Branch

CamperAward — AwardId, GroupId, EventId, RecipientName, AwardTypeId, BonusStars, AwardedAt
```

### Auth / RBAC

```
UserRole              — UserRoleId, Name, Description, SystemName
                        Rows: Admin, Staff, Display

Authority             — AuthorityId, Name, Description, SystemName
                        Rows: LogTransaction, VoidTransaction, TriggerBlockHit,
                              TriggerScoreLock, ManageUsers, ManageGroups,
                              ManageBoard, ManageShop, ViewDisplay, AccessAdminPanel

UserRoleAuthorityLink — UserRoleId, AuthorityId
                        Default authorities for each role

UserAuthorityLink     — UserId, AuthorityId
                        Per-user authority additions only (no denials in v1)
                        Global scope (not event-scoped in v1)

User                  — UserId, UserRoleId, FirstName, LastName,
                        Email, PasswordHash, IsActive
```

**Auth evaluation at login:**
1. Load role's authorities from `UserRoleAuthorityLink`
2. Load user's individual additions from `UserAuthorityLink`
3. Merge and store in session claims — zero DB cost on subsequent checks

---

## Column Naming Convention

- `Name` — short system-friendly label (`"MinuteToWinIt"`, `"BoardGame"`)
- `Description` — human-readable full string (`"Minute to Win It challenges"`)
- `SystemName` — stable code identifier that C# enums map to (present on all reference/catalog tables)

---

## C# Enum Strategy

Keep enums for code visibility. Each reference table with a `SystemName` column has a corresponding C# enum whose values map to `SystemName` strings. No split between "base" and "implementation" enums — single set per domain.

Example:
```csharp
public enum Capability { BoardGame, CoinShop, MiniGameSpinner, Announcements, Itinerary }
public enum Authority  { LogTransaction, VoidTransaction, TriggerBlockHit, ... }
public enum CurrencyType { Primary, Prestige }
public enum AwardType    { Named, BigStick, Branch }
```

---

## What Was Removed

| Original | Replaced By |
|---|---|
| `CampSeason` | `Event` + `EventType` |
| `SpaceType` enum | `ActivityType` + `ActivityTypeCategory` tables |
| `UserRole` enum | `UserRole` table + RBAC system |
| Generic lookup/descriptor pattern | Specific well-named tables per domain |

---

## V1 Implementation Scope

The schema is built in full. Only CCN features are implemented in v1 application code:
- `EventType` seeded with CCN only
- `Capability` rows seeded; CCN event seeded with all capabilities
- `ActivityTypeCategory` seeded with CCN categories (MinuteToWinIt, WaterActivities, etc.)
- No feature-gating UI (CCN always has all capabilities — conditional rendering deferred to v2)
- No Women's Retreat / Annual Meeting pages

Post-camp v2: add remaining EventType rows, build feature-gated rendering, add event-specific pages.

---

## Open Items

- [ ] Finalize CCN 2026 activity list with Katelyn/Vicki (determines ActivityType seed data)
- [ ] Final group count (4-6) determines Group seed data
- [ ] Auth approach: BCrypt/cookie (current scaffold) vs Auth0 — security review pending
