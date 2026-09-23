namespace CampClotNot.Services.Email;

public record EmailMessage(string To, string Subject, string Html, string Text);

public interface IEmailSender
{
    /// False when Email:ResendApiKey / Email:From aren't set — callers fall back to showing
    /// (Admin) or dev-logging (self-service) the link instead of sending.
    bool IsConfigured { get; }

    /// Never throws; returns false (and logs) on any failure.
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default);
}
