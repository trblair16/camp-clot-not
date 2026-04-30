namespace CampClotNot.Data.Entities;

public class Event
{
    public Guid EventId { get; set; }
    public Guid EventTypeId { get; set; }
    public Guid ThemeId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly EffDate { get; set; }
    public DateOnly ExpDate { get; set; }
    public bool IsActive { get; set; }

    public EventType EventType { get; set; } = null!;
    public Theme Theme { get; set; } = null!;
    public ICollection<EventCapability> EventCapabilities { get; set; } = new List<EventCapability>();
    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<BoardSpace> BoardSpaces { get; set; } = new List<BoardSpace>();
    public ICollection<ScriptedBlockHit> ScriptedBlockHits { get; set; } = new List<ScriptedBlockHit>();
    public ICollection<ScriptedMiniGame> ScriptedMiniGames { get; set; } = new List<ScriptedMiniGame>();
    public ICollection<CamperAward> CamperAwards { get; set; } = new List<CamperAward>();
}
