namespace CampClotNot.Data.Entities;

public class PushSubscription
{
    public Guid PushSubscriptionId { get; set; }
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
