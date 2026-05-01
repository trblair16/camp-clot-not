using CampClotNot.Data.Entities;
using CampClotNot.Hubs;
using CampClotNot.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace CampClotNot.Services;

public class TransactionService(
    ITransactionRepository transactions,
    IHubContext<CampHub> hub)
{
    public Task<List<Transaction>> GetAllAsync() => transactions.GetAllAsync();

    public Task<List<Transaction>> GetByGroupAsync(Guid groupId) => transactions.GetByGroupAsync(groupId);

    public async Task<Transaction> PostAsync(Guid groupId, Guid currencyTypeId, int amount,
        string loggedBy, string? note = null)
    {
        var tx = await transactions.CreateAsync(new Transaction
        {
            TxId = Guid.NewGuid(),
            GroupId = groupId,
            CurrencyTypeId = currencyTypeId,
            Amount = amount,
            LoggedBy = loggedBy,
            Note = note,
            CreatedAt = DateTime.UtcNow
        });
        await hub.Clients.All.SendAsync("ScoresUpdated");
        return tx;
    }

    public async Task VoidAsync(Guid txId, string voidedBy)
    {
        await transactions.VoidAsync(txId, voidedBy);
        await hub.Clients.All.SendAsync("ScoresUpdated");
    }

    public async Task ReinstateAsync(Guid txId)
    {
        await transactions.ReinstateAsync(txId);
        await hub.Clients.All.SendAsync("ScoresUpdated");
    }
}
