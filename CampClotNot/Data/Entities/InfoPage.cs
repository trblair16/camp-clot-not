namespace CampClotNot.Data.Entities;

public class InfoPage
{
    public Guid PageId { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string IconEmoji { get; set; } = "📄";
    public int SortOrder { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedBy { get; set; } = null!;

    public byte[]? PdfData { get; set; }
    public string? PdfContentType { get; set; }
    public string? PdfVisibleRoles { get; set; }
}
