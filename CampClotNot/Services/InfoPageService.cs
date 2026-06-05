using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class InfoPageService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<InfoPage>> GetAllAsync()
    {
        using var db = factory.CreateDbContext();
        return await db.InfoPages.OrderBy(p => p.SortOrder).ToListAsync();
    }

    public async Task<InfoPage?> GetBySlugAsync(string slug)
    {
        using var db = factory.CreateDbContext();
        return await db.InfoPages.FirstOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task UpdateBodyAsync(Guid pageId, string body, Guid updatedByUserId)
    {
        using var db = factory.CreateDbContext();
        var page = await db.InfoPages.FindAsync(pageId);
        if (page is null) return;
        page.Body = body;
        page.UpdatedAt = DateTime.UtcNow;
        page.UpdatedByUserId = updatedByUserId;
        await db.SaveChangesAsync();
    }

    public async Task UpdatePdfAsync(Guid pageId, byte[]? pdfData, string? pdfContentType, string? pdfVisibleRoles, Guid updatedByUserId)
    {
        using var db = factory.CreateDbContext();
        var page = await db.InfoPages.FindAsync(pageId);
        if (page is null) return;
        page.PdfData         = pdfData;
        page.PdfContentType  = pdfContentType;
        page.PdfVisibleRoles = pdfVisibleRoles;
        page.UpdatedAt       = DateTime.UtcNow;
        page.UpdatedByUserId = updatedByUserId;
        await db.SaveChangesAsync();
    }
}
