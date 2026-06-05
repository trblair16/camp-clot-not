namespace CampClotNot.Data.Entities;

public class CampDocument
{
    public Guid DocumentId { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? OriginalFileName { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/pdf";
    public string? VisibleRoles { get; set; }
    public int SortOrder { get; set; }
    public DateTime UploadedAt { get; set; }
    public Guid UploadedByUserId { get; set; }
    public User UploadedBy { get; set; } = null!;
}
