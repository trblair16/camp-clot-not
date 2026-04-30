using CampClotNot.Data.Entities;

namespace CampClotNot.Repositories;

public interface ITransactionRepository
{
    Task<List<Transaction>> GetAllAsync();
    Task<List<Transaction>> GetByGroupAsync(Guid groupId);
    Task<Transaction> CreateAsync(Transaction transaction);
    Task VoidAsync(Guid txId, string voidedBy);
}
