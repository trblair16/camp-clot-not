using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class IncidentReportService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<IncidentReport>> GetAllForEventAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        return await db.IncidentReports
            .Include(r => r.IncidentLocation)
            .Where(r => r.EventId == eventId)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IncidentReport?> GetByIdAsync(Guid id)
    {
        using var db = factory.CreateDbContext();
        return await db.IncidentReports
            .Include(r => r.IncidentLocation)
            .FirstOrDefaultAsync(r => r.IncidentReportId == id);
    }

    public async Task<IncidentReport> SubmitAsync(IncidentReport report)
    {
        using var db = factory.CreateDbContext();
        if (report.IncidentReportId == Guid.Empty) report.IncidentReportId = Guid.NewGuid();
        report.SubmittedAt = DateTime.UtcNow;
        db.IncidentReports.Add(report);
        await db.SaveChangesAsync();
        return report;
    }

    public async Task AcknowledgeAsync(Guid id, Guid adminUserId, string adminName)
    {
        using var db = factory.CreateDbContext();
        var report = await db.IncidentReports.FindAsync(id);
        if (report is null) return;
        report.IsAcknowledged = true;
        report.AcknowledgedByUserId = adminUserId;
        report.AcknowledgedByName = adminName;
        report.AcknowledgedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
