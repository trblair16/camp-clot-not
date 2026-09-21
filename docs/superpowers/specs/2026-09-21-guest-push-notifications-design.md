# Guest Push Notifications

**Date:** 2026-09-21
**Status:** Approved, pending implementation plan
**Driver:** Camp Harvest 2026 is coming up fast. Tyler wants this delivered before then. This is
sub-project 2 of a 4-part roadmap (guest identity → guest push notifications → attendance
tracking → admin event configurability), high priority.

## Context

Push notifications already exist in this app, but only for staff: `PushSubscription` (`Data/Entities/PushSubscription.cs`)
stores `Endpoint`/`P256dh`/`Auth`/`UserId?`, VAPID keys are configured, and `PushNotificationService`
exposes `SubscribeAsync`, `UnsubscribeAsync`, `SendToRolesAsync`, and `SendToAllAsync`. The only
place push is actually triggered today is `AnnouncementService.PostAsync` — when a staff member
posts an announcement, it pushes to either specific roles or everyone with a subscription. There
is no "schedule change" push trigger for anyone yet, staff included.

The staff-facing subscribe flow lives in `MainLayout.razor`: on first render, if the signed-in
principal is authenticated and the browser supports push, a dismissible top banner ("Enable
notifications to get camp announcements") appears and calls `ccnPush.subscribe()`
(`wwwroot/js/push-notifications.js`) on tap. Critically, `MainLayout.razor:55` — 
`if (!firstRender || _isGuest) return;` — currently skips this entire block for guests. That is
the only thing blocking guests from the existing mechanism; everything else (the JS helper, the
`/api/vapid-public-key` and `/api/push/subscribe` endpoints, the banner UI) already works for any
authenticated principal, and guests are authenticated under the same cookie scheme as staff
(established in sub-project 1 — named guest identity), just distinguished by a `GuestAttendeeId`
claim instead of a `NameIdentifier` claim.

`Announcement` (`Data/Entities/Announcement.cs`) is already event-scoped (`EventId`), which makes
targeting guests straightforward: a guest should only be pushed announcements for events they
actually joined, tracked via `GuestEventVisit` (sub-project 1).

## Roadmap (for traceability)

1. Named guest identity — done (#304, merged to `dev`)
2. **Guest push notifications** (this doc) — high priority, must ship before Camp Harvest
3. Attendance tracking
4. Admin event configurability (theme UI, event duplication)

## Scope

Extend the *existing* announcement-push mechanism to guests. Do not invent new push trigger
points (e.g. schedule-change pushes) — those don't exist for staff either today, and adding them
is a larger, separate piece of work than "extend guests to what already exists."

## Data Model

`PushSubscription` gains one nullable column:

```csharp
public Guid? GuestAttendeeId { get; set; }
```

A plain scalar column, no navigation property — matching how `UserId` is already modeled on this
exact entity (no EF relationship configured for it either). A subscription row belongs to either
a staff `UserId` or a guest `GuestAttendeeId`, never both, determined by which claim is present
on the request that subscribed it. No DB-level exclusivity constraint — the app's auth model
already makes a request either a staff session or a guest session, never both.

## Subscribe Endpoint

`/api/push/subscribe` (`Program.cs`) currently derives `userId` from `ClaimTypes.NameIdentifier`
only. It gains a second lookup via `GuestAccessService.GetGuestAttendeeId(ctx.User)` (already
exists from sub-project 1) and passes whichever of the two is non-null into
`PushNotificationService.SubscribeAsync` (which itself needs a new `Guid? guestAttendeeId`
parameter alongside its existing `Guid? userId`).

## Enabling the Prompt for Guests

`MainLayout.razor`'s `OnAfterRenderAsync` currently reads:

```csharp
if (!firstRender || _isGuest) return;
var state = await AuthState.GetAuthenticationStateAsync();
if (state.User.HasClaim("mustChangePassword", "true"))
    Nav.NavigateTo("/change-password", forceLoad: true);

if (state.User.Identity?.IsAuthenticated == true)
{
    // ... push-subscribe banner logic
}
```

Change to only guard the `mustChangePassword` redirect on `!_isGuest` (guests never carry that
claim anyway, so this is a no-op cleanup, not a behavior change for them), and let the push block
run unconditionally for any authenticated principal:

```csharp
if (!firstRender) return;
var state = await AuthState.GetAuthenticationStateAsync();
if (!_isGuest && state.User.HasClaim("mustChangePassword", "true"))
    Nav.NavigateTo("/change-password", forceLoad: true);

if (state.User.Identity?.IsAuthenticated == true)
{
    // ... unchanged — now also reaches guests
}
```

No new banner, no new UI, no new JS. The existing "Enable notifications to get camp
announcements" banner and `EnablePush()` handler simply start reaching guests, since guests are
already `Identity.IsAuthenticated == true` under the shared cookie scheme.

## Sending Guest Pushes

New method on `PushNotificationService`:

```csharp
public async Task SendToGuestsForEventAsync(Guid eventId, string title, string body, string? url = null)
```

Queries `PushSubscriptions` where `GuestAttendeeId` is non-null AND that `GuestAttendeeId` has a
matching `GuestEventVisit` row for the given `eventId` — guests only receive pushes for events
they actually joined, never globally. Delivery reuses the existing private `DeliverAsync` helper
(same stale-subscription cleanup on 404/410 that `SendToRolesAsync`/`SendToAllAsync` already do).

`AnnouncementService.PostAsync` already has `eventId` in scope at the point it triggers push —
it gains one more call to `SendToGuestsForEventAsync(eventId, pushTitle, pushBody, "/hub/announcements")`
inside the same try/catch as the existing staff push call, so a guest-push failure can't break
the staff-push path or vice versa (matches the existing swallow-and-continue behavior of that
whole block).

## Explicitly Out of Scope

- New push trigger points (schedule-change pushes, etc.) — not requested, and doesn't exist for
  staff either.
- Pruning/expiring guest push subscriptions after an event ends. A returning guest's subscription
  keeps working for future events with zero re-prompting, since `ccnPush.isSubscribed()` is a
  browser-level check (does this browser have an active push subscription at all), not tied to
  which event or `GuestAttendeeId` it was originally created under. This is a deliberate,
  low-cost side benefit of not building anything extra here.
- Any change to `SendToAllAsync`'s existing lack of event-scoping (a staff member could already
  receive a push for an announcement on an event they're not working — pre-existing gap, not
  introduced or worsened by this work, not fixed here either).
- A DB-level constraint enforcing `UserId`/`GuestAttendeeId` mutual exclusivity on
  `PushSubscription` — not needed given the app's auth model already prevents a single request
  from being both a staff and guest session.

## Testing

No automated test suite exists for this project (established project-wide convention) —
verification is `dotnet build` succeeding plus a manual walkthrough: subscribe as a guest on a
fresh join, post an announcement as staff for that guest's event, confirm the guest's browser
receives the push; confirm a guest who joined a *different* event does NOT receive it; confirm
existing staff push behavior (`SendToRolesAsync`/`SendToAllAsync`) is unaffected.
