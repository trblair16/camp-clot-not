namespace CampClotNot.Data.Entities;

public class AwardType
{
    public Guid AwardTypeId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemName { get; set; } = "";

    public ICollection<CamperAward> CamperAwards { get; set; } = new List<CamperAward>();
}
