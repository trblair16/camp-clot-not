namespace CampClotNot.Data.Entities;

public class Activity
{
    public Guid ActivityId { get; set; }
    public Guid EventId { get; set; }
    public Guid ActivityTypeId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public Event Event { get; set; } = null!;
    public ActivityType ActivityType { get; set; } = null!;
    public ICollection<BoardSpace> BoardSpaces { get; set; } = new List<BoardSpace>();
    public ICollection<ScriptedMiniGame> ScriptedMiniGames { get; set; } = new List<ScriptedMiniGame>();
}
