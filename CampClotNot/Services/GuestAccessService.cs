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
