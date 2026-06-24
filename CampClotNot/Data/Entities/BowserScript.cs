namespace CampClotNot.Data.Entities;

public class BowserScript
{
    public Guid BowserScriptId { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public string FacesJson { get; set; } = "[]";

    public Event Event { get; set; } = null!;
}
