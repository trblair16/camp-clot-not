using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class DocumentService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<CampDocument>> GetForEventAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.CampDocuments
            .Where(d => d.EventId == eventId)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<CampDocument?> GetByIdAsync(Guid id)
    {
        using var db = factory.CreateDbContext();
        return await db.CampDocuments.FindAsync(id);
    }

    public async Task AddAsync(Guid eventId, string title, string? originalFileName,
        byte[] data, string contentType, string? visibleRoles, Guid uploadedByUserId)
    {
        using var db = factory.CreateDbContext();
        var maxOrder = await db.CampDocuments
            .Where(d => d.EventId == eventId)
            .Select(d => (int?)d.SortOrder)
            .MaxAsync() ?? 0;
        db.CampDocuments.Add(new CampDocument
        {
            DocumentId       = Guid.NewGuid(),
            EventId          = eventId,
            Title            = title.Trim(),
            OriginalFileName = originalFileName,
            Data             = data,
            ContentType      = contentType,
            VisibleRoles     = visibleRoles,
            SortOrder        = maxOrder + 1,
            UploadedAt       = CampTime.Now,
            UploadedByUserId = uploadedByUserId,
        });
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid documentId)
    {
        using var db = factory.CreateDbContext();
        var doc = await db.CampDocuments.FindAsync(documentId);
        if (doc is null) return;
        db.CampDocuments.Remove(doc);
        await db.SaveChangesAsync();
    }
}
