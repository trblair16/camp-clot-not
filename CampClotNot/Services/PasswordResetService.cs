using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using CampClotNot.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public record ResetLinkResult(bool Emailed, string Link);

public enum RedeemResult { Ok, Invalid, TooShort }

/// Issues, validates, and redeems single-use password links (forgot password, Admin-sent
/// reset, new-account invite). Only a SHA-256 hash of each token is stored.
public class PasswordResetService(
    IDbContextFactory<AppDbContext> factory,
    IEmailSender email,
    IWebHostEnvironment env,
    ILogger<PasswordResetService> logger)
{
    public const int MinPasswordLength = 8;
    private const int SelfResetsPerHour = 3;

    private static TimeSpan Lifetime(PasswordTokenPurpose p) => p switch
    {
        PasswordTokenPurpose.SelfReset  => TimeSpan.FromHours(1),
        PasswordTokenPurpose.AdminReset => TimeSpan.FromHours(24),
        _                               => TimeSpan.FromDays(7),
    };

    private static string ExpiresText(PasswordTokenPurpose p) => p switch
    {
        PasswordTokenPurpose.SelfReset  => "1 hour",
        PasswordTokenPurpose.AdminReset => "24 hours",
        _                               => "7 days",
    };

    /// Self-service. Silent for unknown/inactive emails and past the rate limit, so the caller
    /// (and its timing — this runs in the background) never reveals whether an account exists.
    public async Task RequestSelfResetAsync(string emailAddress, string baseUrl)
    {
        var normalized = emailAddress.Trim().ToLowerInvariant();
        if (normalized.Length == 0) return;

        using var db = factory.CreateDbContext();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized && u.IsActive && u.CanSignIn);
        if (user is null) return;

        var since = DateTime.UtcNow.AddHours(-1);
        var recent = await db.PasswordResetTokens.CountAsync(t =>
            t.UserId == user.UserId && t.Purpose == PasswordTokenPurpose.SelfReset && t.CreatedAt > since);
        if (recent >= SelfResetsPerHour)
        {
            logger.LogWarning("Password reset rate limit hit for user {UserId}.", user.UserId);
            return;
        }

        var raw = await IssueAsync(db, user.UserId, PasswordTokenPurpose.SelfReset, createdBy: null);
        var link = BuildLink(baseUrl, raw);
        if (!email.IsConfigured)
        {
            // Never log working links outside Development.
            if (env.IsDevelopment()) logger.LogInformation("[dev] Password reset link for {Email}: {Link}", normalized, link);
            else logger.LogWarning("Password reset requested but email isn't configured; no email sent.");
            return;
        }
        await email.SendAsync(EmailTemplates.PasswordLink(user.Email, PasswordTokenPurpose.SelfReset,
            user.FirstName, link, ExpiresText(PasswordTokenPurpose.SelfReset), invitedBy: null));
    }

    public Task<ResetLinkResult> SendAdminResetAsync(Guid userId, Guid adminId, string baseUrl) =>
        SendFromAdminAsync(userId, adminId, baseUrl, PasswordTokenPurpose.AdminReset);

    public Task<ResetLinkResult> SendInviteAsync(Guid userId, Guid adminId, string baseUrl) =>
        SendFromAdminAsync(userId, adminId, baseUrl, PasswordTokenPurpose.Invite);

    private async Task<ResetLinkResult> SendFromAdminAsync(Guid userId, Guid adminId, string baseUrl, PasswordTokenPurpose purpose)
    {
        using var db = factory.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.UserId == userId);
        var admin = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == adminId);

        var raw = await IssueAsync(db, userId, purpose, adminId);
        var link = BuildLink(baseUrl, raw);
        if (!email.IsConfigured) return new ResetLinkResult(false, link);

        var invitedBy = admin is null ? null : $"{admin.FirstName} {admin.LastName}".Trim();
        var sent = await email.SendAsync(EmailTemplates.PasswordLink(user.Email, purpose, user.FirstName, link,
            ExpiresText(purpose), invitedBy));
        return new ResetLinkResult(sent, link);
    }

    /// The user the link belongs to, or null when it's unknown, used, expired, or the user is inactive.
    public async Task<User?> ValidateTokenAsync(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        using var db = factory.CreateDbContext();
        var token = await FindUsableAsync(db, rawToken);
        return token?.User;
    }

    public async Task<(RedeemResult Result, Guid? UserId)> RedeemAsync(string? rawToken, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return (RedeemResult.Invalid, null);
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength) return (RedeemResult.TooShort, null);

        using var db = factory.CreateDbContext();
        var token = await FindUsableAsync(db, rawToken);
        if (token is null) return (RedeemResult.Invalid, null);

        token.UsedAt = DateTime.UtcNow;
        token.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        token.User.MustChangePassword = false;
        await db.SaveChangesAsync();
        return (RedeemResult.Ok, token.UserId);
    }

    private static async Task<PasswordResetToken?> FindUsableAsync(AppDbContext db, string rawToken)
    {
        var hash = Hash(rawToken);
        var now = DateTime.UtcNow;
        return await db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now && t.User.IsActive && t.User.CanSignIn);
    }

    // Revokes the user's other unused links so only the newest one works.
    private static async Task<string> IssueAsync(AppDbContext db, Guid userId, PasswordTokenPurpose purpose, Guid? createdBy)
    {
        var now = DateTime.UtcNow;
        var open = await db.PasswordResetTokens.Where(t => t.UserId == userId && t.UsedAt == null).ToListAsync();
        foreach (var t in open) t.UsedAt = now;

        var raw = Base64Url(RandomNumberGenerator.GetBytes(32));
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            PasswordResetTokenId = Guid.NewGuid(),
            UserId          = userId,
            TokenHash       = Hash(raw),
            Purpose         = purpose,
            CreatedAt       = now,
            ExpiresAt       = now + Lifetime(purpose),
            CreatedByUserId = createdBy
        });
        await db.SaveChangesAsync();
        return raw;
    }

    private static string BuildLink(string baseUrl, string raw) =>
        $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(raw)}";

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// Random password for invited accounts — never shown; the invitee sets their own.
    public static string RandomPassword() => Base64Url(RandomNumberGenerator.GetBytes(24));
}

public record ForgotPasswordRequest(string Email, string BaseUrl);

/// Self-service reset requests are handled after the HTTP response so response time doesn't
/// reveal whether an account exists.
public class ForgotPasswordQueue
{
    private readonly Channel<ForgotPasswordRequest> _channel = Channel.CreateUnbounded<ForgotPasswordRequest>();
    public void Enqueue(ForgotPasswordRequest request) => _channel.Writer.TryWrite(request);
    public IAsyncEnumerable<ForgotPasswordRequest> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public class ForgotPasswordWorker(ForgotPasswordQueue queue, IServiceScopeFactory scopes, ILogger<ForgotPasswordWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var req in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PasswordResetService>()
                    .RequestSelfResetAsync(req.Email, req.BaseUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handling a forgot-password request failed.");
            }
        }
    }
}

public static class PublicBaseUrl
{
    /// App:PublicBaseUrl when set; otherwise the request's host, honouring X-Forwarded-Proto
    /// (Railway terminates HTTPS, so Request.Scheme alone is "http").
    public static string From(HttpRequest req, IConfiguration config)
    {
        var configured = config["App:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');
        var proto = req.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? req.Scheme;
        return $"{proto}://{req.Host}";
    }

    /// For Admin actions inside the Blazor circuit (no HttpContext): the browser's own base URI.
    public static string From(string navigationBaseUri, IConfiguration config)
    {
        var configured = config["App:PublicBaseUrl"];
        return string.IsNullOrWhiteSpace(configured) ? navigationBaseUri.TrimEnd('/') : configured.TrimEnd('/');
    }
}
