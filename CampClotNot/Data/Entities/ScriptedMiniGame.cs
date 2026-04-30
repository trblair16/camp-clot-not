namespace CampClotNot.Data.Entities;

public class ScriptedMiniGame
{
    public Guid ScriptId { get; set; }
    public Guid EventId { get; set; }
    public Guid ActivityId { get; set; }
    public int CampDay { get; set; }
    public bool IsTriggered { get; set; }

    public Event Event { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
}
