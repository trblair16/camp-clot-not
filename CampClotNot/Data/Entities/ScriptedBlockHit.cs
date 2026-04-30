namespace CampClotNot.Data.Entities;

public class ScriptedBlockHit
{
    public Guid ScriptId { get; set; }
    public Guid GroupId { get; set; }
    public Guid EventId { get; set; }
    public int CampDay { get; set; }
    public int DestinationSpaceIndex { get; set; }
    public bool IsTriggered { get; set; }

    public Group Group { get; set; } = null!;
    public Event Event { get; set; } = null!;
}
