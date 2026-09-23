# Password Reset & Invite Emails Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Staff can reset a forgotten password from the login page by email, Admins can email a reset link, and new accounts get an invite link instead of a shared temporary password. The spec is `docs/superpowers/specs/2026-09-23-password-reset-email-design.md` (issue #125, email half).

**Architecture:** A new `PasswordResetToken` table stores hashed single-use tokens. `IEmailSender` (the Resend HTTPS API) sends mail and degrades to "show or log the link" when it isn't configured. `PasswordResetService` issues, validates, and redeems tokens. A channel-backed `BackgroundService` sends self-service emails after the HTTP response, so timing doesn't reveal which accounts exist. There are two new anonymous pages (`/forgot-password`, `/reset-password`) and two form-POST endpoints. `/admin/users` gets invite-on-create and "Email reset link".

**Tech Stack:** Blazor Server (.NET 8), EF Core/Npgsql, ASP.NET Core cookie auth, minimal APIs, `IHttpClientFactory`, `System.Threading.Channels`.

## Global Constraints

- **Only token hashes are stored** (SHA-256 hex). Raw tokens appear only in the emailed or shown link, and in Development logs when email is unconfigured.
- **Lifetimes:** SelfReset 1 h, AdminReset 24 h, Invite 7 days. **Single use.** Issuing a new token revokes the user's other unused tokens.
- **The self-service endpoint always gives the same response** and does its work in the background. **Rate limit:** 3 SelfReset tokens per user per rolling hour.
- **Password rule** matches `/account/change-password`: at least 8 characters, and the confirmation must match.
- No automated test suite exists. Verification is `dotnet build` with no new warnings (confirm the baseline first), plus the manual walkthrough in the PR description.
- Synchronous `CreateDbContext()` only (pitfall #14). Explicit FKs for both `User` navigations (pitfall #13). Modals use `fadeIn`/`popIn` (pitfall #12). Single-quoted `@onclick` attributes when the lambda contains `$"..."` (pitfall #15).
- Scope is limited to the spec: no Turnstile, no session invalidation, no guest email.
- Branch `feature/125-forgot-password-email` off `dev`. The PR targets `dev`.

### Environment (cloud sessions)

The same setup as `2026-09-23-attendance-tracking.md`: `dotnet-sdk-8.0` and `dotnet-ef`. Migrations apply at startup. Running the app locally needs a **real** VAPID key pair (P-256), not the placeholder, or push-related pages crash.

---

### Task 1: Data model and migration

**Files:** `Data/Enums.cs`, `Data/Entities/PasswordResetToken.cs` (new), `Data/AppDbContext.cs`, migration `AddPasswordResetTokens`

- [ ] Add `PasswordTokenPurpose { SelfReset = 0, AdminReset = 1, Invite = 2 }`.
- [ ] Create the entity (see the spec).
- [ ] Add `DbSet<PasswordResetToken> PasswordResetTokens`. In `OnModelCreating`: `HasIndex(TokenHash).IsUnique()`, `HasIndex(UserId, CreatedAt)`, `HasOne(User).WithMany().HasForeignKey(UserId).OnDelete(Restrict)`, `HasOne(CreatedByUser).WithMany().HasForeignKey(CreatedByUserId).OnDelete(Restrict)`.
- [ ] Generate the migration. Read it and confirm there's one new table, two FKs, two indexes, and no shadow columns (`UserId1`).
- [ ] Build. Commit: `feat: PasswordResetToken table`

### Task 2: Email sender and templates

**Files:** `Services/Email/IEmailSender.cs`, `Services/Email/ResendEmailSender.cs`, `Services/Email/EmailTemplates.cs` (new), `Program.cs`

- [ ] `EmailMessage`, `IEmailSender` (see the spec). `ResendEmailSender(HttpClient http, IConfiguration config, ILogger<...>)`: `IsConfigured` means both `Email:ResendApiKey` and `Email:From` are set. `SendAsync` POSTs JSON, logs non-2xx responses with the body, catches exceptions, and returns a bool.
- [ ] `EmailTemplates.PasswordLink(purpose, firstName, link, expiresText, invitedBy?)` returns `(Subject, Html, Text)`. All HTML output is encoded (`WebUtility.HtmlEncode`) and styled inline.
- [ ] Register with `builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(c => { c.BaseAddress = new("https://api.resend.com/"); c.Timeout = TimeSpan.FromSeconds(15); });`
- [ ] Build. Commit: `feat: Resend email sender and password email template`

### Task 3: `PasswordResetService`, background queue, and endpoints

**Files:** `Services/PasswordResetService.cs`, `Services/ForgotPasswordQueue.cs` (new), `Services/AuthService.cs`, `Program.cs`

- [ ] The service follows the spec's signatures. Helpers: `NewToken()` returns `(raw, hash)`. `IssueAsync(db, user, purpose, createdBy)` revokes unused tokens by setting `UsedAt = now` where `UsedAt == null`, then adds the new token. `BuildLink(baseUrl, raw)` returns `{base}/reset-password?token={raw}`. `RequestSelfResetAsync`: normalize the email to lower case, look up an active user, then check the rate limit (count SelfReset tokens with `CreatedAt > now - 1h`, at most 3). If email isn't configured, log the link in Development only.
- [ ] `ForgotPasswordQueue`: a singleton `Channel<ForgotPasswordRequest>`. `ForgotPasswordWorker : BackgroundService` reads it, creates a scope, calls the service, and catches and logs any exception.
- [ ] `AuthService.SignInAsync(HttpContext, Guid userId)` loads the user with `UserRole` and calls the private `SignInUserAsync(..., mustChangePassword: false, ...)`.
- [ ] `PublicBaseUrl.From(HttpRequest, IConfiguration)`: uses `App:PublicBaseUrl`, otherwise `X-Forwarded-Proto` (or `Request.Scheme`) plus `://` and `Request.Host`.
- [ ] Endpoints: `POST /account/forgot-password` enqueues and redirects to `/forgot-password?sent=1`. `POST /account/reset-password` redeems; on `Ok` it signs in and redirects to `/dashboard`, otherwise it redirects back to `/reset-password?token=…&error=…`. Both `.AllowAnonymous()`. Anti-forgery isn't enabled for the existing login form POST, so match that.
- [ ] Register `AddScoped<PasswordResetService>()`, `AddSingleton<ForgotPasswordQueue>()`, `AddHostedService<ForgotPasswordWorker>()`.
- [ ] Build. Commit: `feat: Token issue/redeem service, background forgot-password worker, endpoints`

### Task 4: Pages

**Files:** `Pages/ForgotPassword.razor`, `Pages/ResetPassword.razor` (new), `Pages/Login.razor`

- [ ] Both new pages use `@layout LoginLayout`, `[AllowAnonymous]`, and the same fixed-center card as Login (pitfalls #3/#4), with a native `<form method="post">`.
- [ ] `ResetPassword` reads `token` and `error` from the query and calls `ValidateTokenAsync` in `OnInitializedAsync`. An invalid token shows the expired message plus a link to `/forgot-password`. A valid token shows "Hi, {FirstName}", a hidden token field, and the two password fields.
- [ ] Login: replace the "Contact Tyler" line with a "Forgot your password?" link to `/forgot-password`.
- [ ] Build. Commit: `feat: Forgot and reset password pages`

### Task 5: `/admin/users` (invite on create, email reset link)

**Files:** `Pages/Admin/Users.razor`, `Services/AuthService.cs` (let `CreateUserAsync` return the created user; it already does)

- [ ] Inject `PasswordResetService`, `IEmailSender`, `NavigationManager`, `AuthenticationStateProvider` (for the Admin's user ID).
- [ ] Create form: a radio for "Email an invite link" or "Set a temporary password" (default is invite when `IsConfigured`, otherwise temporary). With invite, the password field is hidden and the account gets a random 24-byte password with `mustChangePassword: false`. Then `SendInviteAsync`. If it wasn't emailed, open the "Share this link" modal.
- [ ] Reset dialog: an "Email a reset link" section at the top with a button, a status line, and the link box plus a Copy button (`navigator.clipboard.writeText` through `IJSRuntime`) when it wasn't emailed.
- [ ] Build. Commit: `feat: Invite and reset-link emails from /admin/users`

### Task 6: Verify, document, PR

- [ ] Run against a local Postgres with email unconfigured, and walk through the spec's Testing list with Playwright.
- [ ] Mark the spec implemented, add implementation notes, and add the env vars to the PR description.
- [ ] Push and open a PR into `dev` ("Part of #125"). Suggest a follow-up issue for Turnstile.
