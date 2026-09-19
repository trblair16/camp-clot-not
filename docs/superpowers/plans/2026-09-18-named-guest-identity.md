# Named Guest Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fully-anonymous Men's Retreat guest cookie with a persistent `GuestAttendee` identity keyed by sanitized first/last name, so a guest can be recognized across repeat visits and across future events — per `docs/superpowers/specs/2026-09-18-named-guest-identity-design.md`.

**Architecture:** Two new entities — `GuestAttendee` (the person, matched by normalized name) and `GuestEventVisit` (a join row recording which events a guest has joined). `GuestAccessService` gains lookup/create/CRUD methods following `SponsorService`'s exact shape. `/join` and `/account/join` capture first/last name alongside the existing event code in one combined form. The guest cookie gains a `GuestAttendeeId` claim and a real display name (replacing the hardcoded `"Guest"` literal). A new `/admin/guests` page (mirroring `/admin/sponsors`) lets Vicki/Amanda view, rename, and delete guest records.

**Tech Stack:** Blazor Server (.NET 8), EF Core/Npgsql, ASP.NET Core cookie auth.

## Global Constraints

- **Name matching is exact-after-normalization only** — trim, collapse internal whitespace to single spaces, lowercase. No fuzzy/typo tolerance. Two different people who share a normalized name merge into one `GuestAttendee` record — this is an accepted product decision, not a bug to guard against.
- **Display name is set once, at creation, and never overwritten by a later join** with the same normalized name (only an explicit admin rename changes it). This avoids casing flip-flopping between visits.
- **`GuestEventVisit` upserts, never duplicates** — unique index on `(GuestAttendeeId, EventId)`. A repeat join for the same event updates `LastSeenAt` on the existing row.
- No automated test suite exists for this project — verification is `dotnet build` succeeding plus a manual walkthrough (final task). This matches every prior sub-project plan in `docs/superpowers/plans/`.
- Never use PowerShell for source file text edits in this repo — always use the Edit tool (CLAUDE.md pitfall #1: PowerShell's `Get-Content` corrupts UTF-8 emoji via CP1252).
- Always use synchronous `DbFactory.CreateDbContext()`, never `CreateDbContextAsync()` (CLAUDE.md pitfall #14).
- Follow existing primary-constructor DI service style (see `GuestAccessService`, `SponsorService`) — do not convert to constructor-body DI.
- Any FK relationship must get an explicit `HasOne().WithMany().HasForeignKey()` in `AppDbContext.OnModelCreating` — this codebase has been bitten twice by EF Core silently creating shadow FK properties that default to `Guid.Empty` on insert (CLAUDE.md pitfall #13, the root cause of the v0.5.4 schedule-save crash).
- This plan is scoped to identity + join tracking only. Do NOT add push notifications, email/phone capture, attendance/check-in tracking, or a "full member" tier — those are separate sub-projects per the design doc.
- Work happens on branch `feature/304-named-guest-identity`, in worktree `.worktrees/304-named-guest-identity/`. Commit there; this branch merges to `dev` via PR, not directly.

---

### Task 1: Add `GuestAttendee` and `GuestEventVisit` entities + migration

**Files:**
- Create: `CampClotNot/Data/Entities/GuestAttendee.cs`
- Create: `CampClotNot/Data/Entities/GuestEventVisit.cs`
- Modify: `CampClotNot/Data/AppDbContext.cs`
- Create: EF migration (via CLI, see step below)

**Interfaces:**
- Produces: `GuestAttendee` (`GuestAttendeeId`, `FirstName`, `LastName`, `NormalizedFirstName`, `NormalizedLastName`, `CreatedAt`, `Visits` collection) and `GuestEventVisit` (`GuestEventVisitId`, `GuestAttendeeId`, `EventId`, `FirstJoinedAt`, `LastSeenAt`, `GuestAttendee`/`Event` navigations) — consumed by `GuestAccessService` (Task 2).

- [ ] **Step 1: Create the `GuestAttendee` entity**

Create `CampClotNot/Data/Entities/GuestAttendee.cs`:

```csharp
namespace CampClotNot.Data.Entities;

public class GuestAttendee
{
    public Guid GuestAttendeeId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string NormalizedFirstName { get; set; } = "";
    public string NormalizedLastName { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public ICollection<GuestEventVisit> Visits { get; set; } = new List<GuestEventVisit>();
}
```

- [ ] **Step 2: Create the `GuestEventVisit` entity**

Create `CampClotNot/Data/Entities/GuestEventVisit.cs`:

```csharp
namespace CampClotNot.Data.Entities;

public class GuestEventVisit
{
    public Guid GuestEventVisitId { get; set; }
    public Guid GuestAttendeeId { get; set; }
    public GuestAttendee GuestAttendee { get; set; } = null!;
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public DateTime FirstJoinedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
```

- [ ] **Step 3: Register the DbSets**

In `CampClotNot/Data/AppDbContext.cs`, after line 57 (`public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();`):

```csharp
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    // Guest identity
    public DbSet<GuestAttendee> GuestAttendees => Set<GuestAttendee>();
    public DbSet<GuestEventVisit> GuestEventVisits => Set<GuestEventVisit>();
```

- [ ] **Step 4: Configure keys, FKs, and unique indexes**

In `CampClotNot/Data/AppDbContext.cs`, at the end of `OnModelCreating`, right before the closing `}` of the method (after the `CampDocument` block that ends around line 160):

```csharp
        modelBuilder.Entity<CampDocument>()
            .HasOne(d => d.UploadedBy)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId);

        // GuestAttendee: unique on normalized name pair (the cross-event matching key)
        modelBuilder.Entity<GuestAttendee>()
            .HasIndex(g => new { g.NormalizedFirstName, g.NormalizedLastName })
            .IsUnique();

        // GuestEventVisit: composite unique (one row per guest per event) + explicit FKs
        modelBuilder.Entity<GuestEventVisit>()
            .HasIndex(v => new { v.GuestAttendeeId, v.EventId })
            .IsUnique();
        modelBuilder.Entity<GuestEventVisit>()
            .HasOne(v => v.GuestAttendee)
            .WithMany(g => g.Visits)
            .HasForeignKey(v => v.GuestAttendeeId);
        modelBuilder.Entity<GuestEventVisit>()
            .HasOne(v => v.Event)
            .WithMany()
            .HasForeignKey(v => v.EventId);
    }
}
```

(Only the new block is added — the existing `CampDocument` lines and the method's closing braces stay where they are; this shows the new code in context so you can see exactly where it goes.)

- [ ] **Step 5: Generate the migration**

Run from the repository root (`camp-clot-not/`):

```bash
dotnet ef migrations add AddGuestAttendee --project CampClotNot --startup-project CampClotNot
```

Expected: a new `Migrations/<timestamp>_AddGuestAttendee.cs` creating `GuestAttendees` and `GuestEventVisits` tables with the two unique indexes and cascade-delete FK from `GuestEventVisits.GuestAttendeeId` to `GuestAttendees`, plus `AppDbContextModelSnapshot.cs` updated.

- [ ] **Step 6: Apply locally and verify build**

```bash
dotnet ef database update --project CampClotNot --startup-project CampClotNot
dotnet build CampClotNot
```

Expected: both succeed with no errors.

- [ ] **Step 7: Commit**

```bash
git add CampClotNot/Data/Entities/GuestAttendee.cs CampClotNot/Data/Entities/GuestEventVisit.cs CampClotNot/Data/AppDbContext.cs CampClotNot/Migrations/
git commit -m "feat: add GuestAttendee and GuestEventVisit entities"
```

---

### Task 2: Extend `GuestAccessService` with identity lookup, visit tracking, and admin CRUD

**Files:**
- Modify: `CampClotNot/Services/GuestAccessService.cs`

**Interfaces:**
- Consumes: `GuestAttendee`, `GuestEventVisit` (Task 1).
- Produces: `GuestAccessService.GetOrCreateGuestAsync(string firstName, string lastName) : Task<GuestAttendee>`, `RecordVisitAsync(Guid guestAttendeeId, Guid eventId) : Task`, `SignInGuestAsync(HttpContext, Event, GuestAttendee) : Task` (signature changed — now takes the guest), `GetGuestAttendeeId(ClaimsPrincipal) : Guid?`, `GetAllGuestsWithVisitsAsync() : Task<List<GuestAttendee>>`, `UpdateGuestNameAsync(Guid, string, string) : Task`, `DeleteGuestAsync(Guid) : Task` — consumed by `Program.cs` (Task 3) and `Pages/Admin/Guests.razor` (Task 6).

- [ ] **Step 1: Add the `GuestAttendeeId` claim type**

In `CampClotNot/Services/GuestAccessService.cs`, find:

```csharp
public static class GuestClaimTypes
{
    public const string EventId = "ccn:guestEventId";
}
```

Change to:

```csharp
public static class GuestClaimTypes
{
    public const string EventId = "ccn:guestEventId";
    public const string GuestAttendeeId = "ccn:guestAttendeeId";
}
```

- [ ] **Step 2: Add the `using` for regex-based normalization**

At the top of the file, change:

```csharp
using System.Security.Claims;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QRCoder;
```

to:

```csharp
using System.Security.Claims;
using System.Text.RegularExpressions;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QRCoder;
```

- [ ] **Step 3: Replace `SignInGuestAsync` and add the new methods**

Find:

```csharp
    public async Task SignInGuestAsync(HttpContext httpContext, Event ev)
    {
        var claims = new List<Claim>
        {
            new(GuestClaimTypes.EventId, ev.EventId.ToString()),
            new(ClaimTypes.Name, "Guest")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var expiresUtc = new DateTimeOffset(ev.ExpDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = expiresUtc });
    }

    public Guid? GetGuestEventId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(GuestClaimTypes.EventId)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
```

Replace with:

```csharp
    public async Task SignInGuestAsync(HttpContext httpContext, Event ev, GuestAttendee guest)
    {
        var claims = new List<Claim>
        {
            new(GuestClaimTypes.EventId, ev.EventId.ToString()),
            new(GuestClaimTypes.GuestAttendeeId, guest.GuestAttendeeId.ToString()),
            new(ClaimTypes.Name, guest.FirstName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var expiresUtc = new DateTimeOffset(ev.ExpDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = expiresUtc });
    }

    public Guid? GetGuestEventId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(GuestClaimTypes.EventId)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public Guid? GetGuestAttendeeId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(GuestClaimTypes.GuestAttendeeId)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static string Normalize(string s) =>
        Regex.Replace(s.Trim(), @"\s+", " ").ToLowerInvariant();

    public async Task<GuestAttendee> GetOrCreateGuestAsync(string firstName, string lastName)
    {
        var normFirst = Normalize(firstName);
        var normLast = Normalize(lastName);

        using var db = factory.CreateDbContext();
        var existing = await db.GuestAttendees.FirstOrDefaultAsync(g =>
            g.NormalizedFirstName == normFirst && g.NormalizedLastName == normLast);
        if (existing is not null) return existing;

        var guest = new GuestAttendee
        {
            GuestAttendeeId = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            NormalizedFirstName = normFirst,
            NormalizedLastName = normLast,
            CreatedAt = DateTime.UtcNow
        };
        db.GuestAttendees.Add(guest);
        await db.SaveChangesAsync();
        return guest;
    }

    public async Task RecordVisitAsync(Guid guestAttendeeId, Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var now = DateTime.UtcNow;
        var visit = await db.GuestEventVisits.FirstOrDefaultAsync(v =>
            v.GuestAttendeeId == guestAttendeeId && v.EventId == eventId);

        if (visit is null)
        {
            db.GuestEventVisits.Add(new GuestEventVisit
            {
                GuestEventVisitId = Guid.NewGuid(),
                GuestAttendeeId = guestAttendeeId,
                EventId = eventId,
                FirstJoinedAt = now,
                LastSeenAt = now
            });
        }
        else
        {
            visit.LastSeenAt = now;
        }
        await db.SaveChangesAsync();
    }

    public async Task<List<GuestAttendee>> GetAllGuestsWithVisitsAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.GuestAttendees
            .Include(g => g.Visits)
            .ThenInclude(v => v.Event)
            .OrderBy(g => g.LastName)
            .ThenBy(g => g.FirstName)
            .ToListAsync();
    }

    public async Task UpdateGuestNameAsync(Guid guestAttendeeId, string firstName, string lastName)
    {
        using var db = factory.CreateDbContext();
        var guest = await db.GuestAttendees.FindAsync(guestAttendeeId);
        if (guest is null) return;

        guest.FirstName = firstName.Trim();
        guest.LastName = lastName.Trim();
        guest.NormalizedFirstName = Normalize(firstName);
        guest.NormalizedLastName = Normalize(lastName);
        await db.SaveChangesAsync();
    }

    public async Task DeleteGuestAsync(Guid guestAttendeeId)
    {
        using var db = factory.CreateDbContext();
        var guest = await db.GuestAttendees.FindAsync(guestAttendeeId);
        if (guest is null) return;
        db.GuestAttendees.Remove(guest);
        await db.SaveChangesAsync();
    }
```

- [ ] **Step 4: Verify build**

```bash
dotnet build CampClotNot
```

Expected: succeeds with no errors. (It will currently fail to build the whole solution because `Program.cs`'s existing call to `SignInGuestAsync(ctx, ev)` no longer matches the new 3-argument signature — that's expected and fixed in Task 3. If you want a green build at this exact checkpoint, temporarily confirm only that `GuestAccessService.cs` itself has no syntax errors by inspection; the full-project build check happens at the end of Task 3.)

- [ ] **Step 5: Commit**

```bash
git add CampClotNot/Services/GuestAccessService.cs
git commit -m "feat: add guest identity lookup, visit tracking, and admin CRUD to GuestAccessService"
```

---

### Task 3: Wire the combined join form (code + name) through `/account/join`

**Files:**
- Modify: `CampClotNot/Program.cs`
- Modify: `CampClotNot/Pages/Join.razor`

**Interfaces:**
- Consumes: `GuestAccessService.ValidateCodeAsync`, `GetOrCreateGuestAsync`, `RecordVisitAsync`, `SignInGuestAsync(ctx, ev, guest)` (Task 2).

- [ ] **Step 1: Update the `/account/join` handler**

In `CampClotNot/Program.cs`, find:

```csharp
    app.MapPost("/account/join", async (HttpContext ctx, GuestAccessService guestSvc) =>
    {
        var form = await ctx.Request.ReadFormAsync();
        var code = form["code"].ToString();
        var ev = await guestSvc.ValidateCodeAsync(code);
        if (ev is null) return Results.Redirect("/join?error=true");
        await guestSvc.SignInGuestAsync(ctx, ev);
        return Results.Redirect("/hub/schedule");
    }).AllowAnonymous();
```

Replace with:

```csharp
    app.MapPost("/account/join", async (HttpContext ctx, GuestAccessService guestSvc) =>
    {
        var form      = await ctx.Request.ReadFormAsync();
        var code      = form["code"].ToString();
        var firstName = form["firstName"].ToString();
        var lastName  = form["lastName"].ToString();

        var ev = await guestSvc.ValidateCodeAsync(code);
        if (ev is null) return Results.Redirect("/join?error=true");

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return Results.Redirect("/join?error=name");

        var guest = await guestSvc.GetOrCreateGuestAsync(firstName, lastName);
        await guestSvc.RecordVisitAsync(guest.GuestAttendeeId, ev.EventId);
        await guestSvc.SignInGuestAsync(ctx, ev, guest);
        return Results.Redirect("/hub/schedule");
    }).AllowAnonymous();
```

- [ ] **Step 2: Add name fields and a name-error state to `Join.razor`**

In `CampClotNot/Pages/Join.razor`, find:

```razor
    @if (Error)
    {
        <div style="background:#FEE2E2;border:2px solid #E83030;border-radius:8px;padding:8px 12px;margin-bottom:12px;font-size:13px;font-weight:700;color:#C0392B">
            ⚠️ That code isn't valid or has expired.
        </div>
    }

    <form method="post" action="/account/join" style="display:flex;flex-direction:column;gap:12px">
        <div>
            <label style="font-family:'Fredoka One',cursive;font-size:12px;color:var(--text-mid);display:block;margin-bottom:4px">Event Code</label>
            <input type="text" name="code" placeholder="e.g. MENSRETREAT26"
                   required autocapitalize="characters" autocomplete="off"
                   style="width:100%;padding:10px 12px;border:3px solid var(--black);border-radius:8px;font-size:14px;font-family:'Nunito',sans-serif;box-sizing:border-box;background:var(--bg-base);color:var(--black);outline:none;box-shadow:2px 2px 0 var(--black);text-transform:uppercase" />
        </div>
        <button type="submit" class="ccn-btn"
                style="width:100%;padding:11px;background:var(--color-primary);font-size:16px;border:3px solid var(--black);box-shadow:4px 4px 0 var(--black)">
            Join
        </button>
    </form>
</div>

@code {
    [Parameter]
    [SupplyParameterFromQuery(Name = "error")]
    public bool Error { get; set; }
}
```

Replace with:

```razor
    @if (Error == "true")
    {
        <div style="background:#FEE2E2;border:2px solid #E83030;border-radius:8px;padding:8px 12px;margin-bottom:12px;font-size:13px;font-weight:700;color:#C0392B">
            ⚠️ That code isn't valid or has expired.
        </div>
    }
    else if (Error == "name")
    {
        <div style="background:#FEE2E2;border:2px solid #E83030;border-radius:8px;padding:8px 12px;margin-bottom:12px;font-size:13px;font-weight:700;color:#C0392B">
            ⚠️ Please enter both your first and last name.
        </div>
    }

    <form method="post" action="/account/join" style="display:flex;flex-direction:column;gap:12px">
        <div>
            <label style="font-family:'Fredoka One',cursive;font-size:12px;color:var(--text-mid);display:block;margin-bottom:4px">Event Code</label>
            <input type="text" name="code" placeholder="e.g. MENSRETREAT26"
                   required autocapitalize="characters" autocomplete="off"
                   style="width:100%;padding:10px 12px;border:3px solid var(--black);border-radius:8px;font-size:14px;font-family:'Nunito',sans-serif;box-sizing:border-box;background:var(--bg-base);color:var(--black);outline:none;box-shadow:2px 2px 0 var(--black);text-transform:uppercase" />
        </div>
        <div>
            <label style="font-family:'Fredoka One',cursive;font-size:12px;color:var(--text-mid);display:block;margin-bottom:4px">First Name</label>
            <input type="text" name="firstName" placeholder="e.g. Sarah"
                   required autocomplete="given-name"
                   style="width:100%;padding:10px 12px;border:3px solid var(--black);border-radius:8px;font-size:14px;font-family:'Nunito',sans-serif;box-sizing:border-box;background:var(--bg-base);color:var(--black);outline:none;box-shadow:2px 2px 0 var(--black)" />
        </div>
        <div>
            <label style="font-family:'Fredoka One',cursive;font-size:12px;color:var(--text-mid);display:block;margin-bottom:4px">Last Name</label>
            <input type="text" name="lastName" placeholder="e.g. Johnson"
                   required autocomplete="family-name"
                   style="width:100%;padding:10px 12px;border:3px solid var(--black);border-radius:8px;font-size:14px;font-family:'Nunito',sans-serif;box-sizing:border-box;background:var(--bg-base);color:var(--black);outline:none;box-shadow:2px 2px 0 var(--black)" />
        </div>
        <button type="submit" class="ccn-btn"
                style="width:100%;padding:11px;background:var(--color-primary);font-size:16px;border:3px solid var(--black);box-shadow:4px 4px 0 var(--black)">
            Join
        </button>
    </form>
</div>

@code {
    [Parameter]
    [SupplyParameterFromQuery(Name = "error")]
    public string? Error { get; set; }
}
```

(Note: `Error` changes type from `bool` to `string?` — the query string values are now `"true"`/`"name"` instead of presence/absence of the flag. Both redirect sites in `Program.cs`, `/join?error=true` and `/join?error=name`, already match this.)

- [ ] **Step 3: Verify build**

```bash
dotnet build CampClotNot
```

Expected: succeeds with no errors. This is the first checkpoint where the full project builds clean again after Task 2's signature change.

- [ ] **Step 4: Commit**

```bash
git add CampClotNot/Program.cs CampClotNot/Pages/Join.razor
git commit -m "feat: capture guest name on join, link to persistent GuestAttendee"
```

---

### Task 4: Show the guest's name in `GuestNav`

**CORRECTED 2026-09-19:** `GuestNav.razor` was redesigned by an already-merged PR (mobile/desktop responsive header split) after this plan's original Task 4 text was written. The steps below reflect the file's actual current structure — the file now has separate `nav-mobile-header`, `nav-desktop-header`, and `nav-bottom-bar` sections, a `ThemeService`-driven logo, and an `ActiveStyle` helper. If you are an implementer reading this, trust these corrected steps, not any earlier draft you may have seen.

**Files:**
- Modify: `CampClotNot/Shared/GuestNav.razor`

**Interfaces:**
- Consumes: `AuthenticationStateProvider` (standard Blazor DI, already used elsewhere e.g. `ChangePassword.razor`).

- [ ] **Step 1: Inject auth state**

In `CampClotNot/Shared/GuestNav.razor`, find:

```razor
@inject NavigationManager Nav
@inject ThemeService ThemeSvc
```

Replace with:

```razor
@inject NavigationManager Nav
@inject ThemeService ThemeSvc
@inject AuthenticationStateProvider Auth
```

- [ ] **Step 2: Add the greeting to the mobile header**

Find:

```razor
@* ── MOBILE HEADER ──────────────────────────────────────────────── *@
<div class="nav-mobile-header">
    <div style="display:flex;flex-direction:column;align-items:center;gap:2px">
        <img src="@(ThemeSvc.LogoAssetPath ?? "/img/ccn-logo-nav.webp")" alt="@ThemeSvc.Active.AppTitle" style="height:36px;width:auto;object-fit:contain" />
        <span style="font-family:var(--font-display);font-weight:700;font-size:10px;color:var(--color-primary);letter-spacing:2px;text-transform:uppercase;line-height:1;text-align:center">@ThemeSvc.Active.AppSubtitle</span>
    </div>
</div>
```

Replace with:

```razor
@* ── MOBILE HEADER ──────────────────────────────────────────────── *@
<div class="nav-mobile-header">
    <div style="display:flex;flex-direction:column;align-items:center;gap:2px">
        <img src="@(ThemeSvc.LogoAssetPath ?? "/img/ccn-logo-nav.webp")" alt="@ThemeSvc.Active.AppTitle" style="height:36px;width:auto;object-fit:contain" />
        <span style="font-family:var(--font-display);font-weight:700;font-size:10px;color:var(--color-primary);letter-spacing:2px;text-transform:uppercase;line-height:1;text-align:center">@ThemeSvc.Active.AppSubtitle</span>
        @if (!string.IsNullOrWhiteSpace(_guestFirstName))
        {
            <span style="font-family:'Fredoka One',cursive;font-size:11px;color:var(--color-primary)">Hi, @_guestFirstName!</span>
        }
    </div>
</div>
```

- [ ] **Step 3: Add the greeting to the desktop header**

Find:

```razor
@* ── DESKTOP HEADER ─────────────────────────────────────────────── *@
<div class="nav-desktop-header">
    <div style="display:flex;flex-direction:column;align-items:center;gap:2px">
        <img src="@(ThemeSvc.LogoAssetPath ?? "/img/ccn-logo-nav.webp")" alt="@ThemeSvc.Active.AppTitle" style="height:42px;width:auto;object-fit:contain" />
        <span style="font-family:var(--font-display);font-weight:700;font-size:11px;color:var(--color-primary);letter-spacing:2px;text-transform:uppercase;line-height:1;text-align:center">@ThemeSvc.Active.AppSubtitle</span>
    </div>
```

Replace with:

```razor
@* ── DESKTOP HEADER ─────────────────────────────────────────────── *@
<div class="nav-desktop-header">
    <div style="display:flex;flex-direction:column;align-items:center;gap:2px">
        <img src="@(ThemeSvc.LogoAssetPath ?? "/img/ccn-logo-nav.webp")" alt="@ThemeSvc.Active.AppTitle" style="height:42px;width:auto;object-fit:contain" />
        <span style="font-family:var(--font-display);font-weight:700;font-size:11px;color:var(--color-primary);letter-spacing:2px;text-transform:uppercase;line-height:1;text-align:center">@ThemeSvc.Active.AppSubtitle</span>
        @if (!string.IsNullOrWhiteSpace(_guestFirstName))
        {
            <span style="font-family:'Fredoka One',cursive;font-size:12px;color:var(--color-primary)">Hi, @_guestFirstName!</span>
        }
    </div>
```

(Only the opening portion of the desktop header block is shown/replaced — the nav-links `<div>` that follows it, and everything after, stays untouched.)

- [ ] **Step 4: Add the code-behind for reading the guest's name**

Find:

```razor
@code {
    protected override async Task OnInitializedAsync()
    {
        await ThemeSvc.LoadAsync();
    }
```

Replace with:

```razor
@code {
    private string? _guestFirstName;

    protected override async Task OnInitializedAsync()
    {
        await ThemeSvc.LoadAsync();
        var state = await Auth.GetAuthenticationStateAsync();
        _guestFirstName = state.User.Identity?.Name;
    }
```

(The rest of the `@code` block — `IsActive` and `ActiveStyle` — stays unchanged.)

- [ ] **Step 5: Verify build**

```bash
dotnet build CampClotNot
```

Expected: succeeds with no errors.

- [ ] **Step 6: Commit**

```bash
git add CampClotNot/Shared/GuestNav.razor
git commit -m "feat: show guest's first name in GuestNav"
```

---

### Task 5: Admin guest management page

**Files:**
- Create: `CampClotNot/Pages/Admin/Guests.razor`
- Modify: `CampClotNot/Shared/AppNav.razor`

**Interfaces:**
- Consumes: `GuestAccessService.GetAllGuestsWithVisitsAsync`, `UpdateGuestNameAsync`, `DeleteGuestAsync` (Task 2).

- [ ] **Step 1: Create the admin page**

Create `CampClotNot/Pages/Admin/Guests.razor`:

```razor
@page "/admin/guests"
@attribute [Authorize(Roles = "Admin")]
@inject GuestAccessService GuestSvc
@inject ISnackbar Snackbar
@inject IJSRuntime JS

<PageTitle>Guests — Camp Clot Not</PageTitle>

<div style="padding: 0 16px 100px">
    <h2 style="font-family:var(--font-display);font-weight:700;font-size:22px;margin:0 0 20px"><img src="/img/icons/person.svg?v=3" style="width:20px;height:20px;vertical-align:middle;margin-right:6px;opacity:.8" alt="" />Guests</h2>

    <div style="display:flex;gap:20px;align-items:flex-start;flex-wrap:wrap">

        <div style="background:var(--panel-bg);border:var(--panel-border);border-radius:10px;box-shadow:4px 4px 0 var(--border-color);padding:20px;flex:0 0 320px;min-width:260px">
            <h3 style="font-family:var(--font-display);font-weight:700;font-size:16px;margin:0 0 16px">@(_editingId == Guid.Empty ? "Rename a Guest" : "Edit Guest")</h3>

            @if (_editingId == Guid.Empty)
            {
                <p style="font-size:13px;color:var(--text-mid);margin:0">Click "Edit" on a guest in the table to rename or delete them. Guests are created automatically when they join an event — there's no manual "add" here.</p>
            }
            else
            {
                <div style="margin-bottom:14px">
                    <div style="font-size:10px;font-weight:900;letter-spacing:1.5px;text-transform:uppercase;color:var(--text-light);margin-bottom:5px">First Name</div>
                    <input type="text" @bind="_fFirstName"
                           style="width:100%;padding:9px 12px;background:var(--bg-base);border:var(--panel-border);border-radius:7px;font-size:14px;font-family:'Nunito',sans-serif;font-weight:600;color:var(--text-dark);box-shadow:2px 2px 0 var(--border-color);outline:none;box-sizing:border-box" />
                </div>
                <div style="margin-bottom:16px">
                    <div style="font-size:10px;font-weight:900;letter-spacing:1.5px;text-transform:uppercase;color:var(--text-light);margin-bottom:5px">Last Name</div>
                    <input type="text" @bind="_fLastName"
                           style="width:100%;padding:9px 12px;background:var(--bg-base);border:var(--panel-border);border-radius:7px;font-size:14px;font-family:'Nunito',sans-serif;font-weight:600;color:var(--text-dark);box-shadow:2px 2px 0 var(--border-color);outline:none;box-sizing:border-box" />
                </div>

                @if (!string.IsNullOrWhiteSpace(_saveError))
                {
                    <div style="color:#D12B2B;font-size:12px;margin-bottom:10px">@_saveError</div>
                }

                <div style="display:flex;gap:8px">
                    <button class="ccn-btn" @onclick="SaveAsync">Save Changes</button>
                    <button class="ccn-btn" style="background:transparent" @onclick="CancelEdit">Cancel</button>
                </div>
            }
        </div>

        <div style="flex:1;min-width:240px">
            @if (_loading)
            {
                <MudProgressCircular Indeterminate="true" />
            }
            else if (!_guests.Any())
            {
                <p style="color:var(--text-light);font-family:var(--font-display);font-weight:700">No guests have joined an event yet.</p>
            }
            else
            {
                <div style="border:var(--panel-border);border-radius:10px;box-shadow:4px 4px 0 var(--border-color);overflow-x:auto">
                    <table style="width:100%;border-collapse:collapse;min-width:640px">
                        <thead>
                            <tr style="background:var(--black)">
                                <th style="padding:10px 14px;color:white;font-family:var(--font-display);font-weight:700;font-size:12px;text-align:left">Name</th>
                                <th style="padding:10px 14px;color:white;font-family:var(--font-display);font-weight:700;font-size:12px;text-align:left">Events Joined</th>
                                <th style="padding:10px 14px;color:white;font-family:var(--font-display);font-weight:700;font-size:12px;text-align:left">Created</th>
                                <th style="padding:10px 14px;color:white;font-family:var(--font-display);font-weight:700;font-size:12px;text-align:left">Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var g in _guests)
                            {
                                <tr style="border-bottom:2px solid #E8E0CC">
                                    <td style="padding:10px 14px;font-size:13px;font-weight:700">@g.FirstName @g.LastName</td>
                                    <td style="padding:10px 14px;font-size:12px;color:var(--text-mid)">
                                        @if (g.Visits.Any())
                                        {
                                            @string.Join(", ", g.Visits.Select(v => v.Event.Name))
                                        }
                                        else { <span>—</span> }
                                    </td>
                                    <td style="padding:10px 14px;font-size:12px;color:var(--text-mid)">@g.CreatedAt.ToString("MMM d, yyyy")</td>
                                    <td style="padding:8px 10px">
                                        <div style="display:flex;gap:4px">
                                            <button class="ccn-btn" style="font-size:11px;padding:4px 10px" @onclick="() => StartEdit(g)">Edit</button>
                                            <button class="ccn-btn" style="font-size:11px;padding:4px 10px;background:#D12B2B;color:white" @onclick="() => DeleteAsync(g)">Del</button>
                                        </div>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </div>

    </div>
</div>

@code {
    private List<GuestAttendee> _guests = [];
    private bool _loading = true;
    private Guid _editingId;
    private string _fFirstName = "";
    private string _fLastName = "";
    private string _saveError = "";

    protected override async Task OnInitializedAsync()
    {
        await Reload();
        _loading = false;
    }

    private async Task Reload()
    {
        _guests = await GuestSvc.GetAllGuestsWithVisitsAsync();
    }

    private void StartEdit(GuestAttendee g)
    {
        _editingId = g.GuestAttendeeId;
        _fFirstName = g.FirstName;
        _fLastName = g.LastName;
        _saveError = "";
    }

    private void CancelEdit()
    {
        _editingId = Guid.Empty;
        _fFirstName = "";
        _fLastName = "";
        _saveError = "";
    }

    private async Task SaveAsync()
    {
        _saveError = "";
        if (string.IsNullOrWhiteSpace(_fFirstName) || string.IsNullOrWhiteSpace(_fLastName))
        {
            _saveError = "First and last name are both required.";
            return;
        }
        await GuestSvc.UpdateGuestNameAsync(_editingId, _fFirstName, _fLastName);
        Snackbar.Add("Guest updated.", Severity.Success);
        CancelEdit();
        await Reload();
    }

    private async Task DeleteAsync(GuestAttendee g)
    {
        if (!await JS.InvokeAsync<bool>("confirm", $"Delete {g.FirstName} {g.LastName}? This removes their join history for all events.")) return;
        await GuestSvc.DeleteGuestAsync(g.GuestAttendeeId);
        Snackbar.Add("Guest deleted.", Severity.Info);
        await Reload();
    }
}
```

- [ ] **Step 2: Add the nav link (desktop dropdown)**

In `CampClotNot/Shared/AppNav.razor`, find:

```razor
                                <div class="nav-admin-section">People</div>
                                <a href="/admin/users"               @onclick="() => _adminDropdownOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Users</a>
                                <a href="/admin/groups"              @onclick="() => _adminDropdownOpen = false"><img src="/img/icons/people.svg?v=3" alt="" /> Groups</a>
                                <a href="/admin/staff"               @onclick="() => _adminDropdownOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Staff</a>
```

Replace with:

```razor
                                <div class="nav-admin-section">People</div>
                                <a href="/admin/users"               @onclick="() => _adminDropdownOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Users</a>
                                <a href="/admin/groups"              @onclick="() => _adminDropdownOpen = false"><img src="/img/icons/people.svg?v=3" alt="" /> Groups</a>
                                <a href="/admin/staff"               @onclick="() => _adminDropdownOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Staff</a>
                                <a href="/admin/guests"              @onclick="() => _adminDropdownOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Guests</a>
```

- [ ] **Step 3: Add the nav link (mobile sheet)**

In `CampClotNot/Shared/AppNav.razor`, find:

```razor
        <div class="nav-admin-sheet-section">People</div>
        <a href="/admin/users"               @onclick="() => _adminSheetOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Users</a>
        <a href="/admin/groups"              @onclick="() => _adminSheetOpen = false"><img src="/img/icons/people.svg?v=3" alt="" /> Groups</a>
        <a href="/admin/staff"               @onclick="() => _adminSheetOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Staff</a>
```

Replace with:

```razor
        <div class="nav-admin-sheet-section">People</div>
        <a href="/admin/users"               @onclick="() => _adminSheetOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Users</a>
        <a href="/admin/groups"              @onclick="() => _adminSheetOpen = false"><img src="/img/icons/people.svg?v=3" alt="" /> Groups</a>
        <a href="/admin/staff"               @onclick="() => _adminSheetOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Staff</a>
        <a href="/admin/guests"              @onclick="() => _adminSheetOpen = false"><img src="/img/icons/person.svg?v=3" alt="" /> Guests</a>
```

- [ ] **Step 4: Verify build**

```bash
dotnet build CampClotNot
```

Expected: succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add CampClotNot/Pages/Admin/Guests.razor CampClotNot/Shared/AppNav.razor
git commit -m "feat: add /admin/guests page for viewing, renaming, and deleting guest records"
```

---

### Task 6: Manual end-to-end verification

**Files:** None (verification only).

- [ ] **Step 1: Apply the migration to your local dev DB if not already done**

```bash
dotnet ef database update --project CampClotNot --startup-project CampClotNot
```

- [ ] **Step 2: Run the app locally**

```bash
dotnet run --project CampClotNot
```

- [ ] **Step 3: Set a guest code on an event (if one isn't already set)**

Log in as Admin, go to `/admin/events`, edit an event, set a Guest Code (e.g. `TESTCODE`), save.

- [ ] **Step 4: Join as a new guest**

Open a private/incognito browser window, navigate to `/join`, enter the code plus a first/last name (e.g. "Taylor" / "Reed"). Submit.

Expected: redirected to `/hub/schedule`, `GuestNav` shows "Hi, Taylor!".

- [ ] **Step 5: Verify re-join is idempotent**

Go back to `/join` in the same browser session, submit the same code and name again.

Expected: no error, lands on `/hub/schedule` again. In the admin `/admin/guests` page (Step 7 below), confirm there is still only one "Taylor Reed" row, not two.

- [ ] **Step 6: Verify cross-event name matching**

If a second event with a different guest code exists (or create one), join it with the same name ("Taylor" / "Reed") in a fresh private window.

Expected: succeeds; in `/admin/guests`, "Taylor Reed" now shows both event names under "Events Joined."

- [ ] **Step 7: Verify the admin guest page**

As Admin, go to `/admin/guests`. Confirm "Taylor Reed" appears with the correct joined-events list and created date. Click Edit, change the last name to "Reeds", Save — confirm the table updates. Click Delete, confirm the row disappears.

- [ ] **Step 8: Verify normalization**

Join a fresh event as "  taylor  " / "REED" (extra spaces, mixed case). Confirm in `/admin/guests` this matches (or creates, if you deleted the prior record in Step 7) a guest displayed with sensible casing, not literally "  taylor  " with double spaces.

- [ ] **Step 9: Push the branch and open a PR**

```bash
git push -u origin feature/304-named-guest-identity
gh pr create --base dev --title "Named guest identity" --body "Implements docs/superpowers/specs/2026-09-18-named-guest-identity-design.md. Closes #304."
```
