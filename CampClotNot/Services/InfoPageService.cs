using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampClotNot.Services;

public class InfoPageService(IDbContextFactory<AppDbContext> factory, IMemoryCache cache)
{
    private const string AllKey = "info.all";
    private static string SlugKey(string slug) => $"info.slug.{slug}";

    public async Task<List<InfoPage>> GetAllAsync()
    {
        return await cache.GetOrCreateAsync(AllKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var db = factory.CreateDbContext();
            return await db.InfoPages
                .OrderBy(p => p.SortOrder)
                .Select(p => new InfoPage
                {
                    PageId          = p.PageId,
                    Slug            = p.Slug,
                    Title           = p.Title,
                    Body            = p.Body,
                    IconEmoji       = p.IconEmoji,
                    SortOrder       = p.SortOrder,
                    UpdatedAt       = p.UpdatedAt,
                    UpdatedByUserId = p.UpdatedByUserId,
                    PdfContentType  = p.PdfContentType,
                    PdfVisibleRoles = p.PdfVisibleRoles
                    // PdfData excluded — served on demand via /hub/info/{slug}/pdf
                })
                .ToListAsync();
        }) ?? [];
    }

    public async Task<InfoPage?> GetBySlugAsync(string slug)
    {
        return await cache.GetOrCreateAsync(SlugKey(slug), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var db = factory.CreateDbContext();
            return await db.InfoPages
                .Where(p => p.Slug == slug)
                .Select(p => new InfoPage
                {
                    PageId          = p.PageId,
                    Slug            = p.Slug,
                    Title           = p.Title,
                    Body            = p.Body,
                    IconEmoji       = p.IconEmoji,
                    SortOrder       = p.SortOrder,
                    UpdatedAt       = p.UpdatedAt,
                    UpdatedByUserId = p.UpdatedByUserId,
                    PdfContentType  = p.PdfContentType,
                    PdfVisibleRoles = p.PdfVisibleRoles
                    // PdfData excluded — served on demand via /hub/info/{slug}/pdf
                })
                .FirstOrDefaultAsync();
        });
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
        cache.Remove(AllKey);
        cache.Remove(SlugKey(page.Slug));
    }

    // removePdf=true explicitly clears the blob; pdfData!=null uploads a new one; otherwise keeps existing.
    public async Task UpdatePdfAsync(Guid pageId, byte[]? pdfData, string? pdfContentType, string? pdfVisibleRoles, Guid updatedByUserId, bool removePdf = false)
    {
        using var db = factory.CreateDbContext();
        var page = await db.InfoPages.FindAsync(pageId);
        if (page is null) return;
        page.PdfVisibleRoles = pdfVisibleRoles;
        if (removePdf)
        {
            page.PdfData        = null;
            page.PdfContentType = null;
        }
        else if (pdfData is not null)
        {
            page.PdfData        = pdfData;
            page.PdfContentType = pdfContentType;
        }
        page.UpdatedAt       = DateTime.UtcNow;
        page.UpdatedByUserId = updatedByUserId;
        await db.SaveChangesAsync();
        cache.Remove(AllKey);
        cache.Remove(SlugKey(page.Slug));
    }
}
