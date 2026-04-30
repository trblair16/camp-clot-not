namespace CampClotNot.Data.Entities;

public class GroupBoardPos
{
    public Guid GroupId { get; set; }
    public int SpaceIndex { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Group Group { get; set; } = null!;
}
