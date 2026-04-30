namespace CampClotNot.Data.Entities;

public class BoardSpace
{
    public Guid SpaceId { get; set; }
    public Guid EventId { get; set; }
    public Guid ActivityId { get; set; }
    public int SpaceIndex { get; set; }
    public float XPos { get; set; }
    public float YPos { get; set; }
    public string? IconAssetPath { get; set; }

    public Event Event { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
}
