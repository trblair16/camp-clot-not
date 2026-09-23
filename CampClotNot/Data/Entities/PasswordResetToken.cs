namespace CampClotNot.Data.Entities;

/// A single-use link for setting a password: self-service "forgot password", an Admin-sent
/// reset, or a new-account invite. Only the SHA-256 hash of the token is stored.
public class PasswordResetToken
{
    public Guid PasswordResetTokenId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public PasswordTokenPurpose Purpose { get; set; }
    public DateTime CreatedAt { get; set; }          // UTC
    public DateTime ExpiresAt { get; set; }          // UTC
    public DateTime? UsedAt { get; set; }            // UTC; set when redeemed or revoked by a newer token
    public Guid? CreatedByUserId { get; set; }       // Admin who sent it; null for self-service
    public User? CreatedByUser { get; set; }
}
