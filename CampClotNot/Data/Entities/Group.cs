namespace CampClotNot.Data.Entities;

public class Group
{
    public Guid GroupId { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string Color { get; set; } = "#cccccc";
    public string? TokenAssetPath { get; set; }
    public string? CabinDisplayName { get; set; }

    public Event Event { get; set; } = null!;
    public GroupBoardPos? BoardPos { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<CamperAward> CamperAwards { get; set; } = new List<CamperAward>();
    public ICollection<ScriptedBlockHit> ScriptedBlockHits { get; set; } = new List<ScriptedBlockHit>();
}
