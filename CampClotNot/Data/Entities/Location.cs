namespace CampClotNot.Data.Entities;

public class Location
{
    public Guid LocationId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }
    public int SortOrder { get; set; }
}
