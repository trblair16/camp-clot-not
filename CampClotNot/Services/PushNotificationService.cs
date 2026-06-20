using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CampClotNot.Data;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using DbPushSub = CampClotNot.Data.Entities.PushSubscription;

namespace CampClotNot.Services;

public class PushNotificationService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly PushServiceClient _pushClient;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        IDbContextFactory<AppDbContext> factory,
        IConfiguration config,
        ILogger<PushNotificationService> logger)
    {
        _factory = factory;
        _logger = logger;

        var vapidPublic = config["Vapid:PublicKey"]!;
        var vapidPrivate = config["Vapid:PrivateKey"]!;
        var vapidSubject = config["Vapid:Subject"] ?? "mailto:trblair16@gmail.com";

        _pushClient = new PushServiceClient();
        _pushClient.DefaultAuthentication = new VapidAuthentication(vapidPublic, vapidPrivate)
        {
            Subject = vapidSubject
        };
    }

    public async Task SubscribeAsync(string endpoint, string p256dh, string auth, Guid? userId)
    {
        using var db = _factory.CreateDbContext();
        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint);

        if (existing is not null)
        {
            existing.P256dh = p256dh;
            existing.Auth = auth;
            existing.UserId = userId;
        }
        else
        {
            db.PushSubscriptions.Add(new DbPushSub
            {
                PushSubscriptionId = Guid.NewGuid(),
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task UnsubscribeAsync(string endpoint)
    {
        using var db = _factory.CreateDbContext();
        var sub = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (sub is not null)
        {
            db.PushSubscriptions.Remove(sub);
            await db.SaveChangesAsync();
        }
    }

    public async Task SendToAllAsync(string title, string body, string? url = null)
    {
        using var db = _factory.CreateDbContext();
        var subs = await db.PushSubscriptions.ToListAsync();

        var payload = JsonSerializer.Serialize(new { title, body, url });

        var stale = new List<DbPushSub>();
        foreach (var sub in subs)
        {
            try
            {
                var pushSub = new Lib.Net.Http.WebPush.PushSubscription
                {
                    Endpoint = sub.Endpoint,
                    Keys = { ["p256dh"] = sub.P256dh, ["auth"] = sub.Auth }
                };
                var message = new PushMessage(payload)
                {
                    Urgency = PushMessageUrgency.High
                };
                await _pushClient.RequestPushMessageDeliveryAsync(pushSub, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push delivery failed for {Endpoint}", sub.Endpoint);
                if (ex is PushServiceClientException psce && (int)psce.StatusCode is 404 or 410)
                    stale.Add(sub);
            }
        }

        if (stale.Count > 0)
        {
            db.PushSubscriptions.RemoveRange(stale);
            await db.SaveChangesAsync();
            _logger.LogInformation("Removed {Count} stale push subscriptions", stale.Count);
        }
    }
}
