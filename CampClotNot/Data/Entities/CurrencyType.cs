namespace CampClotNot.Data.Entities;

public class CurrencyType
{
    public Guid CurrencyTypeId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string? Icon { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
