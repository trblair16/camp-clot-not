using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CampClotNot.Services.Email;

/// Sends through Resend's HTTPS API (https://resend.com/docs/api-reference/emails/send-email).
/// HTTPS rather than SMTP because Railway restricts outbound SMTP on non-Pro plans.
public class ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> logger) : IEmailSender
{
    private string? ApiKey => config["Email:ResendApiKey"];
    private string? From => config["Email:From"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(From);

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Email not configured (Email:ResendApiKey / Email:From); not sending \"{Subject}\".", message.Subject);
            return false;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "emails")
            {
                Content = JsonContent.Create(new
                {
                    from = From,
                    to = new[] { message.To },
                    subject = message.Subject,
                    html = message.Html,
                    text = message.Text
                })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

            using var res = await http.SendAsync(req, ct);
            if (res.IsSuccessStatusCode) return true;

            var body = await res.Content.ReadAsStringAsync(ct);
            logger.LogError("Resend rejected \"{Subject}\": {Status} {Body}", message.Subject, (int)res.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sending \"{Subject}\" via Resend failed.", message.Subject);
            return false;
        }
    }
}
