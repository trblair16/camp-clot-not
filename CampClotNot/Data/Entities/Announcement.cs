namespace CampClotNot.Data.Entities;

public enum AnnouncementPriority { Normal, Urgent }

public class Announcement
{
    public Guid AnnouncementId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;
    public bool IsPinned { get; set; }
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsArchived { get; set; }
    public string? ReactionsJson { get; set; }
}
