# Password Reset & Invite Emails

**Date:** 2026-09-23
**Status:** Approved, pending implementation
**Driver:** Staff who forget their password are stuck. Today the only way back in is to ask an Admin
to type a new temporary password on `/admin/users`, or Tyler editing the database directly. The login
page's fallback is a `mailto:` link to Tyler. As the platform moves to chapter-scale events (Camp
Harvest, Annual Meeting) with more staff and volunteers, that doesn't scale. Tracked in issue #125
(the email-reset half; Turnstile is split out).

## Context

- **No email infrastructure exists.** There's no sender, no templates, and no config. CLAUDE.md's
  v1.1.0 list says "Forgot password via email (SendGrid free tier)", but that was never built. #125's
  notes recommend Resend.
- **Railway restricts outbound SMTP** on non-Pro plans, so the app sends through an HTTPS email API.
- **Auth today:** BCrypt password hashes on `User.PasswordHash`, cookie auth, and a login form that
  posts natively to `/account/login`, because a cookie can't be set from the SignalR circuit.
  `User.MustChangePassword` forces `/change-password` after an admin-set temporary password. The
  existing password rule is at least 8 characters, and the confirmation must match.
- **Admin flows today:** `/admin/users` creates accounts with a typed "Temporary Password", and "Reset
  PW" lets the Admin type a new one. Either way, the Admin has to hand the password over by text or in
  person.
- **No forwarded-headers middleware.** Behind Railway's proxy, `Request.Scheme` is `http` and
  `RemoteIpAddress` is the proxy's address. That affects both link building and any per-IP rate limit
  (see below).

## Design Decisions (settled with Tyler before writing)

| Question | Decision |
|---|---|
| Email provider | **Resend**, over its HTTPS API. Free tier is about 3,000 emails a month (100 a day), far more than staff password traffic needs. |
| Sender address | **Not decided yet.** The sender comes from config (`Email__From`), so it can start as Resend's test sender on staging and move to an HBDA domain before go-live without a code change. |
| Flows | **All three:** self-service "Forgot password?" from the login page, an Admin **"Email reset link"** on `/admin/users`, and a **welcome/invite email** when an Admin creates an account (no temporary password is ever shared). |
| Turnstile CAPTCHA (also in #125) | **Separate, later.** This PR is email and reset only. The reset form is protected by rate limiting. |

Decisions made during design (defaults, not asked):

| Topic | Decision |
|---|---|
| Token storage | A random 32-byte token goes in the link. Only its **SHA-256 hash** is stored, so a database leak can't be replayed as working links. |
| Lifetimes | Self-service reset: **1 hour**. Admin-sent reset: **24 hours**. Invite: **7 days**. |
| Single use | A token works once. Issuing a new token for a user revokes their other unused tokens, so only the newest link works. |
| Account enumeration | "Forgot password?" always shows the same message ("If an account exists for that email, we've sent a link"). The token is created and the email sent **in the background** after the response, so response time doesn't reveal whether the account exists. |
| Rate limit | At most **3 self-service reset emails per account per hour**, counted in the DB. Anything over that is silently dropped behind the same generic message. There's no per-IP limit, because without forwarded headers every request appears to come from Railway's proxy. |
| Inactive users | They get no email, and their tokens don't redeem. |
| Email not configured | When `Email__ResendApiKey` is unset, nothing is sent. The **Admin flows then show the link in the dialog to copy** and send some other way. The self-service flow logs the link **only in Development**, and otherwise logs a warning with no link. |
| Link base URL | `App__PublicBaseUrl` when set. Otherwise it's derived from the request, respecting `X-Forwarded-Proto`, or from the circuit's `NavigationManager.BaseUri` for Admin actions. |
| After a successful reset | The password is set, `MustChangePassword` is cleared, the user is signed in, and they're redirected to `/dashboard`. |

## Data Model

### New entity `PasswordResetToken`

```csharp
public class PasswordResetToken
{
    public Guid PasswordResetTokenId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = "";         // hex SHA-256 of the raw token; unique
    public PasswordTokenPurpose Purpose { get; set; }    // SelfReset | AdminReset | Invite
    public DateTime CreatedAt { get; set; }              // UTC
    public DateTime ExpiresAt { get; set; }              // UTC
    public DateTime? UsedAt { get; set; }                // UTC; set on redeem or revoke
    public Guid? CreatedByUserId { get; set; }           // Admin who sent it; null for self-service
    public User? CreatedByUser { get; set; }
}

public enum PasswordTokenPurpose { SelfReset = 0, AdminReset = 1, Invite = 2 }   // Data/Enums.cs
```

- A unique index on `TokenHash`, and an index on `(UserId, CreatedAt)` for the rate-limit count.
- Both `User` relationships are configured explicitly in `OnModelCreating` (CLAUDE.md pitfall #13,
  since there are two navigations to `User`) with `DeleteBehavior.Restrict`. Users are deactivated,
  never deleted.
- Migration: `AddPasswordResetTokens`.

## Services

### `IEmailSender` / `ResendEmailSender`

```csharp
public record EmailMessage(string To, string Subject, string Html, string Text);
public interface IEmailSender
{
    bool IsConfigured { get; }
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default);   // false on failure
}
```

`ResendEmailSender` is a typed `HttpClient` (`AddHttpClient`). It POSTs to
`https://api.resend.com/emails` with `Authorization: Bearer {Email:ResendApiKey}` and a body of
`{ from, to: [..], subject, html, text }`. A non-2xx response is logged with the response body, and the
method returns `false`. It never throws into the caller.

### `PasswordResetService`

```csharp
Task RequestSelfResetAsync(string email, string baseUrl);                  // background; silent on unknown/limited
Task<ResetLinkResult> SendAdminResetAsync(Guid userId, Guid adminId, string baseUrl);
Task<ResetLinkResult> SendInviteAsync(Guid userId, Guid adminId, string baseUrl);
Task<User?> ValidateTokenAsync(string rawToken);                           // for the reset page
Task<RedeemResult> RedeemAsync(string rawToken, string newPassword);       // Ok | Invalid | TooShort
```

`ResetLinkResult(bool Emailed, string Link)` carries the link so the Admin UI can show it for copying
when email isn't configured, or when the send failed.

The token is `RandomNumberGenerator.GetBytes(32)`, base64url-encoded. Redeeming looks up the token's
hash, then checks that it's unused, unexpired, and belongs to an active user. It BCrypt-hashes the new
password, clears `MustChangePassword`, and sets `UsedAt`, all in one save.

### Background sending

`ForgotPasswordQueue` is an unbounded `Channel<(string Email, string BaseUrl)>`, drained by a
`BackgroundService` that calls `RequestSelfResetAsync` inside its own DI scope. The HTTP endpoint only
enqueues the request and redirects.

### Email template

The template is a single `EmailTemplates.PasswordLink(...)` helper that builds the HTML and plain-text
versions. It uses inline styles only, because email clients strip `<style>` tags. The look is the
neutral neo-brutalist panel: cream background, white card with a thick black border, and a
yellow-and-black button. The text changes by purpose:

- **Reset:** "Reset your password". "Someone (hopefully you) asked to reset…". The link expires in 1
  hour (self-service) or 24 hours (Admin-sent).
- **Invite:** "You're invited to HBDA Events". "{Admin name} created an account for you… set your
  password". The link expires in 7 days.

Each email includes the plain link as text below the button, and a "didn't request this? ignore it"
line. It uses no theme colors, so it looks the same whichever event is active.

## Pages and Endpoints

| Route | Kind | Purpose |
|---|---|---|
| `/forgot-password` | Razor page, `LoginLayout`, anonymous | An email field, then a POST to `/account/forgot-password`. Shows the generic confirmation when `?sent=1` |
| `POST /account/forgot-password` | minimal API, anonymous | Enqueues the request and redirects to `/forgot-password?sent=1` |
| `/reset-password?token=…` | Razor page, `LoginLayout`, anonymous | Validates the token on load. If it's invalid or expired, shows "This link has expired or was already used" with a link to request a new one. Otherwise shows the user's first name ("Hi, Vicki"), New and Confirm password fields, and a POST to `/account/reset-password` |
| `POST /account/reset-password` | minimal API, anonymous | Redeems the token, signs the user in, and redirects to `/dashboard`. On failure it redirects back with `?error=tooshort`, `mismatch`, or `invalid` |

The login page's "Forgot your password? Contact Tyler" line becomes a **"Forgot your password?"** link
to `/forgot-password`.

`AuthService` gains `SignInAsync(HttpContext, Guid userId)`, a public wrapper over the existing private
`SignInUserAsync`, for the reset endpoint.

## `/admin/users` Changes

- **Create Account:** a "Password" choice between **Email an invite link** (the default when email is
  configured) and **Set a temporary password** (today's behaviour). With an invite, the password field
  is hidden. The account gets a random, never-shown password with `MustChangePassword = false`, and
  `SendInviteAsync` runs. If email isn't configured or the send fails, a small modal shows the link with
  a Copy button.
- **Reset PW dialog:** a new top section, **"Email a reset link"**, with a button that calls
  `SendAdminResetAsync`. It either shows "Sent to vicki@…" or displays the link to copy. The existing
  "Or set a temporary password" fields stay below.
- Modals follow the existing `fadeIn`/`popIn` pattern (pitfall #12).

## Configuration

| Variable | Required | Example |
|---|---|---|
| `Email__ResendApiKey` | to actually send | `re_…` |
| `Email__From` | with the key | `HBDA Events <onboarding@resend.dev>` (staging) / `HBDA Events <no-reply@your-domain>` |
| `App__PublicBaseUrl` | optional | `https://camp-clot-not-staging.up.railway.app` |

## Explicitly Out of Scope

- Cloudflare Turnstile (split out of #125).
- Invalidating a user's other signed-in sessions after a reset. The cookie auth has no server-side
  session or security stamp. This is worth doing later alongside a security-stamp claim.
- Email for guests (`GuestAttendee`). Guests don't have passwords.
- Other transactional email (announcements, schedule changes). The `IEmailSender` seam makes these
  easy later.
- Forwarded-headers middleware and per-IP rate limiting.
- An email change-verification flow.

## Testing

No automated test suite exists for this project. Verification is `dotnet build` with no new warnings,
plus a manual walkthrough against a local Postgres, with email unconfigured, so links are logged in
Development or shown to the Admin:

- Forgot password with a real email, an unknown email, and an inactive user's email. All three show
  the same page, and a link is logged only for the real one. The 4th request within an hour logs
  nothing.
- Redeem the link: the new password works, and the old one doesn't. Redeem again: "expired or already
  used". An expired token (set `ExpiresAt` in the past) is rejected. A too-short password or a
  mismatch shows the error.
- Requesting twice means only the newest link works.
- As Admin: the "Email reset link" dialog shows a copyable link. The invite-create flow creates the
  account and shows the link. The invitee sets a password and lands on the dashboard without being
  forced to change it again.
- A user whose temporary password was set by an Admin (`MustChangePassword`) and who then resets by
  email isn't forced to change it again.
- With a Resend key (staging): a real email arrives, the button works, and the plain-text part is
  present.
