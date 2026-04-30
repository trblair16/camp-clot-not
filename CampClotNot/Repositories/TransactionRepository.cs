using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Repositories;

public class TransactionRepository(IDbContextFactory<AppDbContext> factory) : ITransactionRepository
{
    public async Task<List<Transaction>> GetAllAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.Transactions
            .Include(t => t.Group)
            .Include(t => t.CurrencyType)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Transaction>> GetByGroupAsync(Guid groupId)
    {
        using var db = factory.CreateDbContext();
        return await db.Transactions
            .Include(t => t.CurrencyType)
            .Where(t => t.GroupId == groupId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        using var db = factory.CreateDbContext();
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return transaction;
    }

    public async Task VoidAsync(Guid txId, string voidedBy)
    {
        using var db = factory.CreateDbContext();
        var tx = await db.Transactions.FindAsync(txId);
        if (tx is not null && tx.VoidedAt is null)
        {
            tx.VoidedAt = DateTime.UtcNow;
            tx.VoidedBy = voidedBy;
            await db.SaveChangesAsync();
        }
    }
}
