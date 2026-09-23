using System.Net;
using CampClotNot.Data;

namespace CampClotNot.Services.Email;

/// Transactional email bodies. Inline styles only — most clients strip <style>. Deliberately
/// not themed per event, so the email looks the same whichever event is active.
public static class EmailTemplates
{
    private const string AppName = "HBDA Events";

    public static EmailMessage PasswordLink(
        string to, PasswordTokenPurpose purpose, string firstName, string link, string expiresIn, string? invitedBy)
    {
        var isInvite = purpose == PasswordTokenPurpose.Invite;
        var subject  = isInvite ? $"You're invited to {AppName}" : $"Reset your {AppName} password";
        var heading  = isInvite ? $"Welcome to {AppName}!" : "Reset your password";
        var intro = isInvite
            ? $"{(invitedBy is null ? "An administrator" : invitedBy)} created an account for you. Choose a password to sign in."
            : "Someone (hopefully you) asked to reset your password. Choose a new one below.";
        var button   = isInvite ? "Set my password" : "Reset password";
        var footer   = isInvite
            ? "If you weren't expecting this, you can ignore this email."
            : "If you didn't ask for this, you can ignore this email — your password won't change.";

        string H(string s) => WebUtility.HtmlEncode(s);
        var greeting = string.IsNullOrWhiteSpace(firstName) ? "Hi," : $"Hi {firstName},";

        var html = $"""
            <div style="background:#F2ECD8;padding:32px 16px;font-family:Arial,Helvetica,sans-serif;color:#1A1A1A">
              <div style="max-width:480px;margin:0 auto;background:#FFFFFF;border:3px solid #1A1A1A;border-radius:12px;box-shadow:4px 4px 0 #1A1A1A;padding:28px 24px">
                <div style="font-size:12px;font-weight:bold;letter-spacing:2px;text-transform:uppercase;color:#8A7D6A;margin-bottom:8px">{H(AppName)}</div>
                <h1 style="font-size:22px;margin:0 0 16px">{H(heading)}</h1>
                <p style="font-size:15px;line-height:1.5;margin:0 0 8px">{H(greeting)}</p>
                <p style="font-size:15px;line-height:1.5;margin:0 0 24px">{H(intro)}</p>
                <p style="margin:0 0 24px">
                  <a href="{H(link)}" style="display:inline-block;background:#F5C800;color:#1A1A1A;text-decoration:none;font-weight:bold;font-size:16px;padding:12px 22px;border:3px solid #1A1A1A;border-radius:8px">{H(button)}</a>
                </p>
                <p style="font-size:13px;line-height:1.5;color:#4A4035;margin:0 0 8px">This link expires in {H(expiresIn)} and can only be used once. If the button doesn't work, copy this address into your browser:</p>
                <p style="font-size:12px;line-height:1.4;word-break:break-all;margin:0 0 20px"><a href="{H(link)}" style="color:#1a5fa8">{H(link)}</a></p>
                <p style="font-size:12px;color:#8A7D6A;margin:0">{H(footer)}</p>
              </div>
            </div>
            """;

        var text = $"""
            {greeting}

            {intro}

            {button}: {link}

            This link expires in {expiresIn} and can only be used once.
            {footer}

            — {AppName}
            """;

        return new EmailMessage(to, subject, html, text);
    }
}
