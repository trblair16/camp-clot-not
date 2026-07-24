# v1.1.0 Sub-project 5: Guest QR/Event-Code Access Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an attendee scan a QR code or type a code from a flyer, land on read-only `/hub/schedule` + `/hub/announcements` with no login, and have that access expire automatically after the event — per `docs/superpowers/specs/2026-07-11-v110-mens-retreat-enablement-design.md` §5.

**Architecture:** Guests get signed in under the app's **existing** cookie scheme (`CookieAuthenticationDefaults.AuthenticationScheme`), not a second scheme — see Global Constraints for why this deviates from the design doc's literal "second cookie scheme" wording. A guest's `ClaimsPrincipal` carries an `EventId` claim instead of a `Role` claim. A new `HubGuestAccess` authorization policy accepts either a staff role or that claim. `Event.GuestCode` (nullable, admin-set) is the join code; `/join/{code}` and `/join` (manual entry) both flow through a new `GuestAccessService` that validates the code and signs in. `Admin/Events.razor` gets a Guest Code field and a "Generate QR" action backed by the `QRCoder` package. `MainLayout` swaps in a minimal `GuestNav` component instead of the full `AppNav` when the principal is a guest.

**Tech Stack:** Blazor Server (.NET 8), EF Core/Npgsql, ASP.NET Core cookie auth, `QRCoder` (new package).

## Global Constraints

- **Single cookie scheme, not two.** The design doc says "issues a second cookie authentication scheme (`GuestScheme`)." That doesn't work reliably in Blazor Server: `AddServerSideBlazor()`'s `AuthenticationStateProvider` captures `HttpContext.User` once, at the HTTP request that establishes the SignalR circuit, using the app's **default** authentication scheme only (`AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)` in `Program.cs` — there is only one default). A cookie issued under a second, non-default scheme name would not surface in `AuthorizeRouteView`/`AuthorizeView` inside the circuit — `[Authorize(AuthenticationSchemes = "...")]` re-authentication is an ASP.NET Core *endpoint-routing* middleware behavior, and Blazor Server's own component-tree routing doesn't go through that pipeline per-page. Sign guests in under the same default scheme instead, distinguished by claims (no `Role` claim, has an `EventId` claim). This still satisfies "a guest and a staff member can be authenticated simultaneously in different sessions" — that always meant different browsers/devices, which naturally have independent cookies regardless of scheme count.
- **Announcements are not event-scoped today** (`AnnouncementService.GetFeedAsync()` has no `EventId` filter anywhere — confirmed by reading the service; this is true for staff too, not something this plan introduces). A Men's Retreat guest will see the same global announcement feed as everyone else, including any historical CCN 2026 posts. That's a pre-existing platform gap, not a regression — do not attempt to fix it in this plan.
- **Schedule IS event-scoped**, and a guest's `EventId` claim can differ from whichever event is currently marked active (e.g. if an Admin flips the active event after the guest joined, before their cookie expires). `Schedule.razor` must resolve the event to display from the guest's claim when present, falling back to `ActiveEventService` only for staff. Do not just call `ActiveEventSvc.GetActiveEventIdAsync()` unconditionally on that page.
- **Guest code matching is case-insensitive.** Store `Event.GuestCode` uppercased (`.Trim().ToUpperInvariant()`) when the admin saves it, and normalize the same way before comparing in `GuestAccessService.ValidateCodeAsync` — Postgres `text` equality is case-sensitive by default and there's no reason to make attendees type a flyer code exactly-cased.
- **Guest sign-in must happen via a real HTTP response, not a Blazor circuit.** This codebase already established that pattern for staff login (native `<form method="post" action="/account/login">` in `Login.razor`, handled by a minimal API endpoint in `Program.cs`) because cookies can't be set from inside a SignalR circuit. `/join` and `/join/{code}` follow the exact same shape.
- No automated test suite exists for this project — verification is `dotnet build` succeeding plus a manual walkthrough (final task).
- Never use PowerShell for source file text edits in this repo — always use the Edit tool.
- Always use synchronous `DbFactory.CreateDbContext()`, never `CreateDbContextAsync()`.
- Follow existing primary-constructor DI service style (see `ActiveEventService`, `CapabilityService`) for `GuestAccessService`.

---

### Task 1: Add `Event.GuestCode` column

**Files:**
- Modify: `CampClotNot/Data/Entities/Event.cs`
- Modify: `CampClotNot/Data/AppDbContext.cs`
- Create: EF migration (via CLI, see step below)

**Interfaces:**
- Produces: `Event.GuestCode` (`string?`) — consumed by `GuestAccessService` (Task 2) and `Admin/Events.razor` (Task 5).

- [ ] **Step 1: Add the property**

In `CampClotNot/Data/Entities/Event.cs`, after the `IsActive` property (line 11):

```csharp
    public bool IsActive { get; set; }
    public string? GuestCode { get; set; }
```

- [ ] **Step 2: Add a unique index**

In `CampClotNot/Data/AppDbContext.cs`, right after the existing `InfoPage` unique-index block (after line 124, `.IsUnique();`):

```csharp
        // Event: unique guest join code (nullable — Postgres allows multiple NULLs under a unique index)
        modelBuilder.Entity<Event>()
            .HasIndex(e => e.GuestCode)
            .IsUnique();
```

- [ ] **Step 3: Generate the migration**

Run from `CampClotNot/`:

```bash
dotnet ef migrations add AddEventGuestCode --project CampClotNot --startup-project CampClotNot
```

Expected: a new `Migrations/<timestamp>_AddEventGuestCode.cs` adding a nullable `text` column `GuestCode` to `Events` plus a unique index, and `AppDbContextModelSnapshot.cs` updated.

- [ ] **Step 4: Apply locally and verify build**

```bash
dotnet ef database update --project CampClotNot --startup-project CampClotNot
dotnet build
```

Expected: both succeed with no errors.

- [ ] **Step 5: Commit**

```bash
git add CampClotNot/Data/Entities/Event.cs CampClotNot/Data/AppDbContext.cs CampClotNot/Migrations/
git commit -m "feat: add Event.GuestCode column for guest join codes"
```

---

### Task 2: `GuestAccessService` — code validation, sign-in, QR generation

**Files:**
- Create: `CampClotNot/Services/GuestAccessService.cs`
- Modify: `CampClotNot/CampClotNot.csproj`

**Interfaces:**
- Consumes: `Event.GuestCode` (Task 1), `IDbContextFactory<AppDbContext>`.
- Produces: `GuestClaimTypes.EventId` (`const string`) — consumed by Task 3 (policy), Task 6 (`MainLayout`), Task 7 (`Schedule.razor`/`Announcements.razor`).
  `GuestAccessService.ValidateCodeAsync(string? code) -> Task<Event?>`
  `GuestAccessService.SignInGuestAsync(HttpContext, Event) -> Task`
  `GuestAccessService.GetGuestEventId(ClaimsPrincipal) -> Guid?`
  `GuestAccessService.GenerateJoinQrPng(string joinUrl) -> byte[]`

- [ ] **Step 1: Add the QRCoder package**

In `CampClotNot/CampClotNot.csproj`, inside the existing `<ItemGroup>`, after the `Npgsql.EntityFrameworkCore.PostgreSQL` line:

```xml
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
    <PackageReference Include="QRCoder" Version="1.6.0" />
```

- [ ] **Step 2: Restore and verify the package resolves**

```bash
dotnet restore
```

Expected: `Restored ... CampClotNot.csproj` with no errors.

- [ ] **Step 3: Write `GuestAccessService`**

Create `CampClotNot/Services/GuestAccessService.cs`:

```csharp
using System.Security.Claims;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace CampClotNot.Services;

public static class GuestClaimTypes
{
    public const string EventId = "ccn:guestEventId";
}

public class GuestAccessService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<Event?> ValidateCodeAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var normalized = code.Trim().ToUpperInvariant();

        using var db = factory.CreateDbContext();
        var ev = await db.Events.FirstOrDefaultAsync(e => e.GuestCode == normalized);
        if (ev is null) return null;

        var accessExpires = ev.ExpDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        if (DateTime.UtcNow > accessExpires) return null;

        return ev;
    }

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

    public byte[] GenerateJoinQrPng(string joinUrl)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(joinUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(20);
    }
}
```

Note: use `PngByteQRCode`, not QRCoder's `QRCode` class — the latter depends on `System.Drawing.Common`, which is Windows-only in modern .NET and will fail at runtime on Railway's Linux container. `PngByteQRCode` has no such dependency.

- [ ] **Step 4: Build**

```bash
dotnet build
```

Expected: succeeds.

- [ ] **Step 5: Commit**

```bash
git add CampClotNot/Services/GuestAccessService.cs CampClotNot/CampClotNot.csproj
git commit -m "feat: add GuestAccessService for guest code validation, sign-in, and QR generation"
```

---

### Task 3: `HubGuestAccess` authorization policy + DI registration

**Files:**
- Modify: `CampClotNot/Program.cs`

**Interfaces:**
- Consumes: `GuestClaimTypes.EventId` (Task 2).
- Produces: policy name `"HubGuestAccess"` — consumed by `Schedule.razor` and `Announcements.razor` (Task 7). `GuestAccessService` registered in DI — consumed by Task 4 and Task 5's endpoints.

- [ ] **Step 1: Register `GuestAccessService`**

In `CampClotNot/Program.cs`, in the Services block (after line 77, `builder.Services.AddScoped<BowserEventService>();`):

```csharp
    builder.Services.AddScoped<BowserEventService>();
    builder.Services.AddScoped<GuestAccessService>();
```

- [ ] **Step 2: Add the policy**

Replace line 52:

```csharp
    builder.Services.AddAuthorization();
```

with:

```csharp
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("HubGuestAccess", policy => policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Staff") ||
            ctx.User.IsInRole("Volunteer") || ctx.User.IsInRole("MedicalStaff") ||
            ctx.User.HasClaim(c => c.Type == GuestClaimTypes.EventId)));
    });
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: succeeds. (`GuestClaimTypes` resolves via the existing `using CampClotNot.Services;` already present in `Program.cs`.)

- [ ] **Step 4: Commit**

```bash
git add CampClotNot/Program.cs
git commit -m "feat: add HubGuestAccess authorization policy"
```

---

### Task 4: `/join` manual entry + `/join/{code}` + `/account/join`

**Files:**
- Create: `CampClotNot/Pages/Join.razor`
- Modify: `CampClotNot/Program.cs`

**Interfaces:**
- Consumes: `GuestAccessService.ValidateCodeAsync`, `SignInGuestAsync` (Task 2/3).

- [ ] **Step 1: Create the manual-entry page**

Create `CampClotNot/Pages/Join.razor`, mirroring `Login.razor`'s native-POST pattern exactly:

```razor
@page "/join"
@layout LoginLayout

<PageTitle>Join Event — Camp Clot Not</PageTitle>

<img src="/img/ccn-logo-2026.webp?v=2" alt="Camp Clot Not" fetchpriority="high"
     style="position:fixed;bottom:calc(50% + 195px);left:50%;transform:translateX(-50%);width:clamp(120px,25vw,180px);height:auto;pointer-events:none" />

<div class="ccn-panel"
     style="position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);width:min(340px,calc(100vw - 32px));padding:20px">

    <div style="font-family:'Fredoka One',cursive;font-size:20px;color:var(--black);text-align:center;margin-bottom:16px">
        Enter Event Code
    </div>

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

- [ ] **Step 2: Add the join endpoints**

In `CampClotNot/Program.cs`, after the existing `/logout` endpoint (after line 223, the closing `});` for `app.MapGet("/logout", ...)`):

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

    app.MapGet("/join/{code}", async (string code, HttpContext ctx, GuestAccessService guestSvc) =>
    {
        var ev = await guestSvc.ValidateCodeAsync(code);
        if (ev is null) return Results.Redirect("/join?error=true");
        await guestSvc.SignInGuestAsync(ctx, ev);
        return Results.Redirect("/hub/schedule");
    }).AllowAnonymous();
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add CampClotNot/Pages/Join.razor CampClotNot/Program.cs
git commit -m "feat: add /join manual entry and /join/{code} guest sign-in endpoints"
```

---

### Task 5: Admin UI — Guest Code field + Generate QR action

**Files:**
- Modify: `CampClotNot/Pages/Admin/Events.razor`
- Modify: `CampClotNot/Program.cs`

**Interfaces:**
- Consumes: `Event.GuestCode` (Task 1), `GuestAccessService.GenerateJoinQrPng` (Task 2).

- [ ] **Step 1: Add the Guest Code field to the form**

In `CampClotNot/Pages/Admin/Events.razor`, after the "Active event" block (after line 90, the closing `</div>` for the Active-event checkbox row) and before the "Capabilities" block (line 92):

```razor
            <div style="margin-bottom:16px">
                <div style="font-size:10px;font-weight:900;letter-spacing:1.5px;text-transform:uppercase;color:var(--text-light);margin-bottom:5px">Guest Code</div>
                <input type="text" @bind="_fGuestCode" placeholder="e.g. MENSRETREAT26"
                       style="width:100%;padding:9px 12px;background:var(--bg-base);border:3px solid var(--black);border-radius:7px;font-size:14px;font-family:'Nunito',sans-serif;font-weight:600;color:var(--text-dark);box-shadow:2px 2px 0 var(--black);outline:none;box-sizing:border-box;text-transform:uppercase" />
                <div style="font-size:11px;color:var(--text-light);margin-top:4px">Attendees scan a QR code or type this to reach a read-only Schedule/Announcements view. Leave blank to disable guest access for this event.</div>
                @if (_editingId != Guid.Empty && !string.IsNullOrWhiteSpace(_fGuestCode))
                {
                    <a class="ccn-btn" style="display:inline-block;margin-top:8px;font-size:11px;padding:6px 10px;text-decoration:none"
                       href="/admin/events/@_editingId/guest-qr" target="_blank">Generate QR</a>
                }
            </div>
```

- [ ] **Step 2: Wire the field into load/save/reset**

In the `@code` block, add the field near `_fIsActive` (after line 200):

```csharp
    private bool _fIsActive;
    private string _fGuestCode = "";
```

In `StartEdit` (after line 237, `_fIsActive = ev.IsActive;`):

```csharp
        _fIsActive = ev.IsActive;
        _fGuestCode = ev.GuestCode ?? "";
```

In `ResetForm` (after line 249, `_fIsActive = false;`):

```csharp
        _fIsActive = false;
        _fGuestCode = "";
```

In `SaveAsync`, in the create branch (after line 337, `IsActive = _fIsActive` inside the `new Event { ... }` initializer — add a trailing comma to that line first, then add the new line):

```csharp
                IsActive = _fIsActive,
                GuestCode = string.IsNullOrWhiteSpace(_fGuestCode) ? null : _fGuestCode.Trim().ToUpperInvariant()
```

In the edit branch (after line 350, `existing.IsActive = _fIsActive;`):

```csharp
                existing.IsActive = _fIsActive;
                existing.GuestCode = string.IsNullOrWhiteSpace(_fGuestCode) ? null : _fGuestCode.Trim().ToUpperInvariant();
```

- [ ] **Step 3: Add duplicate-code validation**

In `SaveAsync`, right after the existing end-date check (after line 300, the closing `}` for the `if (_fEndDate < _fStartDate)` block) and before `using var db = DbFactory.CreateDbContext();` (line 302), the `db` variable is declared right after — so insert the check just after that `using` line instead:

```csharp
        using var db = DbFactory.CreateDbContext();

        if (!string.IsNullOrWhiteSpace(_fGuestCode))
        {
            var normalized = _fGuestCode.Trim().ToUpperInvariant();
            var codeTaken = await db.Events.AnyAsync(e => e.GuestCode == normalized && e.EventId != _editingId);
            if (codeTaken)
            {
                _saveError = "That guest code is already used by another event.";
                return;
            }
        }
```

- [ ] **Step 4: Add the QR image endpoint**

In `CampClotNot/Program.cs`, after the `/location-image/{id:guid}` endpoint (after line 247, its closing `.AllowAnonymous();`):

```csharp
    app.MapGet("/admin/events/{id:guid}/guest-qr", async (Guid id, HttpRequest req, IDbContextFactory<AppDbContext> factory, GuestAccessService guestSvc) =>
    {
        using var db = factory.CreateDbContext();
        var ev = await db.Events.FindAsync(id);
        if (ev?.GuestCode is null) return Results.NotFound();

        var joinUrl = $"{req.Scheme}://{req.Host}/join/{Uri.EscapeDataString(ev.GuestCode)}";
        var png = guestSvc.GenerateJoinQrPng(joinUrl);
        return Results.File(png, "image/png");
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));
```

- [ ] **Step 5: Build**

```bash
dotnet build
```

Expected: succeeds.

- [ ] **Step 6: Commit**

```bash
git add CampClotNot/Pages/Admin/Events.razor CampClotNot/Program.cs
git commit -m "feat: add Guest Code field and Generate QR action to Admin Events"
```

---

### Task 6: `GuestNav` component + `MainLayout` guest-mode branch

**Files:**
- Create: `CampClotNot/Shared/GuestNav.razor`
- Modify: `CampClotNot/Shared/MainLayout.razor`

**Interfaces:**
- Consumes: `GuestAccessService.GetGuestEventId` (Task 2).

- [ ] **Step 1: Create the minimal guest nav**

Create `CampClotNot/Shared/GuestNav.razor`:

```razor
@inject NavigationManager Nav

<div style="display:flex;align-items:center;justify-content:space-between;padding:14px 18px;background:var(--black);border-bottom:3px solid var(--black)">
    <img src="/img/ccn-logo-nav.webp" alt="Camp Clot Not" style="height:34px;width:auto;object-fit:contain" />
    <div style="display:flex;gap:6px">
        <a href="/hub/schedule" style="padding:8px 14px;border-radius:8px;font-family:'Fredoka One',cursive;font-size:13px;text-decoration:none;color:@(IsActive("/hub/schedule") ? "#1A1A1A" : "#F5C800");background:@(IsActive("/hub/schedule") ? "#F5C800" : "transparent")">Schedule</a>
        <a href="/hub/announcements" style="padding:8px 14px;border-radius:8px;font-family:'Fredoka One',cursive;font-size:13px;text-decoration:none;color:@(IsActive("/hub/announcements") ? "#1A1A1A" : "#F5C800");background:@(IsActive("/hub/announcements") ? "#F5C800" : "transparent")">Announcements</a>
    </div>
</div>

@code {
    private bool IsActive(string href) =>
        ("/" + Nav.ToBaseRelativePath(Nav.Uri)).StartsWith(href, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Branch `MainLayout` on guest status**

In `CampClotNot/Shared/MainLayout.razor`, add the injection (after line 5, `@inject IJSRuntime JS`):

```razor
@inject GuestAccessService GuestSvc
```

Replace line 14:

```razor
<AppNav OnLeaderboardClick="@(() => _showProjector = true)" />
```

with:

```razor
@if (_isGuest)
{
    <GuestNav />
}
else
{
    <AppNav OnLeaderboardClick="@(() => _showProjector = true)" />
}
```

In the `@code` block, add a field (after line 34, `private bool _showProjector;`):

```csharp
    private bool _isGuest;
```

In `OnAfterRenderAsync`, right after `var state = await AuthState.GetAuthenticationStateAsync();` (line 40), before the `mustChangePassword` check:

```csharp
        var state = await AuthState.GetAuthenticationStateAsync();
        _isGuest = GuestSvc.GetGuestEventId(state.User).HasValue;
        if (_isGuest)
        {
            StateHasChanged();
            return;
        }
        if (state.User.HasClaim("mustChangePassword", "true"))
```

(The early `return` skips the `mustChangePassword` redirect and the push-notification banner entirely for guests — push notifications are explicitly out of scope for anonymous guests per the design spec, and `mustChangePassword` doesn't apply to a passwordless guest session.)

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add CampClotNot/Shared/GuestNav.razor CampClotNot/Shared/MainLayout.razor
git commit -m "feat: add minimal GuestNav and guest-mode branch in MainLayout"
```

---

### Task 7: Guest support on `Schedule.razor` and `Announcements.razor`

**Files:**
- Modify: `CampClotNot/Pages/Hub/Schedule.razor`
- Modify: `CampClotNot/Pages/Hub/Announcements.razor`

**Interfaces:**
- Consumes: `GuestAccessService.GetGuestEventId` (Task 2), policy `"HubGuestAccess"` (Task 3).

- [ ] **Step 1: Switch `Schedule.razor`'s authorization and inject the service**

Replace line 2:

```razor
@attribute [Authorize(Roles = "Admin,Staff,Volunteer,MedicalStaff")]
```

with:

```razor
@attribute [Authorize(Policy = "HubGuestAccess")]
```

Add an injection (after line 10, `@inject ActiveEventService ActiveEventSvc`):

```razor
@inject GuestAccessService GuestSvc
```

- [ ] **Step 2: Resolve the effective event id from the guest claim, falling back to the active event**

In `OnInitializedAsync`, replace lines 668-677:

```csharp
        var authState = await Auth.GetAuthenticationStateAsync();
        _canEdit        = authState.User.IsInRole("Admin");
        _isAdminOrStaff = authState.User.IsInRole("Admin") || authState.User.IsInRole("Staff");
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);

        _eventId = await ActiveEventSvc.GetActiveEventIdAsync();

        // Run all independent reference-data queries in parallel
        using var dbGroups = DbFactory.CreateDbContext();
        var campEventTask  = ActiveEventSvc.GetActiveEventAsync();
```

with:

```csharp
        var authState = await Auth.GetAuthenticationStateAsync();
        _canEdit        = authState.User.IsInRole("Admin");
        _isAdminOrStaff = authState.User.IsInRole("Admin") || authState.User.IsInRole("Staff");
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);

        var guestEventId = GuestSvc.GetGuestEventId(authState.User);
        _isGuest = guestEventId.HasValue;
        _eventId = guestEventId ?? await ActiveEventSvc.GetActiveEventIdAsync();

        // Run all independent reference-data queries in parallel
        using var dbGroups = DbFactory.CreateDbContext();
        var campEventTask  = _isGuest
            ? dbGroups.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == _eventId)
            : ActiveEventSvc.GetActiveEventAsync();
```

Add the field near `_canEdit` (after line 630, `private bool _canEdit;`):

```csharp
    private bool _canEdit;
    private bool _isGuest;
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: succeeds.

- [ ] **Step 4: Switch `Announcements.razor`'s authorization and inject the service**

Replace line 2:

```razor
@attribute [Authorize(Roles = "Admin,Staff,Volunteer,MedicalStaff")]
```

with:

```razor
@attribute [Authorize(Policy = "HubGuestAccess")]
```

Add an injection (after line 5, `@inject IJSRuntime JS`):

```razor
@inject GuestAccessService GuestSvc
```

- [ ] **Step 5: Compute `_isGuest` and suppress `HubSubNav`**

In `OnInitializedAsync`, replace lines 145-148:

```csharp
        var authState = await Auth.GetAuthenticationStateAsync();
        _isAdmin  = authState.User.IsInRole("Admin");
        _canPost  = authState.User.IsInRole("Admin") || authState.User.IsInRole("Staff");
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);
```

with:

```csharp
        var authState = await Auth.GetAuthenticationStateAsync();
        _isAdmin  = authState.User.IsInRole("Admin");
        _canPost  = authState.User.IsInRole("Admin") || authState.User.IsInRole("Staff");
        _isGuest  = GuestSvc.GetGuestEventId(authState.User).HasValue;
        Guid.TryParse(authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out _userId);
```

Add the field near `_isAdmin` (after line 132, `private bool _isAdmin;`):

```csharp
    private bool _isAdmin;
    private bool _isGuest;
```

Replace line 17:

```razor
<HubSubNav />
```

with:

```razor
@if (!_isGuest)
{
    <HubSubNav />
}
```

- [ ] **Step 6: Hide the reaction UI for guests**

Wrap the reaction row (lines 75-110, the `<div style="display:flex;gap:4px;margin-top:8px;...">...</div>` block containing the reaction buttons and add-reaction picker) in a guest check. Change the opening tag on line 75:

```razor
                            <div style="display:flex;gap:4px;margin-top:8px;flex-wrap:wrap;align-items:center">
```

to:

```razor
                            @if (!_isGuest)
                            {
                            <div style="display:flex;gap:4px;margin-top:8px;flex-wrap:wrap;align-items:center">
```

and after its closing `</div>` (line 110), add:

```razor
                            }
```

- [ ] **Step 7: Build**

```bash
dotnet build
```

Expected: succeeds.

- [ ] **Step 8: Commit**

```bash
git add CampClotNot/Pages/Hub/Schedule.razor CampClotNot/Pages/Hub/Announcements.razor
git commit -m "feat: guest support on Schedule and Announcements — policy auth, event-scoped resolution, hide staff affordances"
```

---

### Task 8: Manual end-to-end verification

No automated tests exist for this project — this task is the acceptance gate. Run against local dev first, then staging.

- [ ] **Step 1: Local build and run**

```bash
dotnet build
dotnet run --project CampClotNot
```

Expected: app starts without exceptions, `/health` returns `{"status":"ok","db":"connected"}`.

- [ ] **Step 2: Set a guest code locally**

Log in as `tyler@hbda.local` / `DevAdmin1!`, go to `/admin/events`, edit an event, set Guest Code to `TESTCODE`, save. Confirm the "Generate QR" link appears and opens a PNG in a new tab that visibly encodes a `/join/TESTCODE` URL (scan it with a phone camera to confirm, or decode it with any QR reader).

- [ ] **Step 3: Manual entry flow**

In a private/incognito browser window, go to `/join`, type `testcode` (lowercase, to confirm case-insensitive matching), submit. Confirm: redirected to `/hub/schedule`, no login prompt, `GuestNav` (black bar, just Schedule/Announcements links) shown instead of the full staff nav.

- [ ] **Step 4: Direct QR-link flow**

In another private window, navigate directly to `/join/TESTCODE`. Confirm: immediate redirect to `/hub/schedule` with no intermediate page.

- [ ] **Step 5: Guest sees read-only schedule and announcements**

As the guest session: confirm `/hub/schedule` shows the event's schedule with no Edit buttons or Add Event button. Confirm `/hub/announcements` shows the feed with no "New Announcement" form, no Pin/Archive buttons, and no reaction buttons/add-reaction picker. Confirm navigating to `/hub/staff` or `/admin/events` directly redirects to `/login` (guest has no access beyond the two allowed pages).

- [ ] **Step 6: Invalid code handling**

Go to `/join`, submit a garbage code. Confirm redirect back to `/join?error=true` with the "That code isn't valid or has expired" message, no exception, no guest session created.

- [ ] **Step 7: Staff sessions unaffected**

Log in as staff/admin in a separate window. Confirm `/hub/schedule` and `/hub/announcements` behave exactly as before (full `AppNav`, edit buttons, reactions, posting all present).

- [ ] **Step 8: Clean up the test code, then repeat against staging**

Locally: edit the event again, clear the Guest Code field, save. Confirm `/join/TESTCODE` now redirects to `/join?error=true`.

Then repeat steps 2-7 against `https://web-staging-staging-6852.up.railway.app` using the real Men's Retreat event (`EventId dd37b662-8920-4dc4-8233-22856654b330`) and a real code (e.g. `MENSRETREAT26`) — push the branch to `dev` first so staging picks it up (staging auto-deploys from `dev` on every push).

- [ ] **Step 9: Final commit if any fixups were needed during verification**

```bash
git add -A
git commit -m "fix: address issues found during guest access manual verification"
```

(Skip this step if no fixups were needed.)
