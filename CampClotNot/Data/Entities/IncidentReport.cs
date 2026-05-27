namespace CampClotNot.Data.Entities;

public class IncidentReport
{
    public Guid IncidentReportId { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public DateTime DateOfIncident { get; set; }
    public DateTime DateCompleted { get; set; }
    public string PersonsInvolved { get; set; } = "";
    public string Description { get; set; } = "";
    public string RecommendedAction { get; set; } = "";
    public Guid SubmittedByUserId { get; set; }
    public string SubmittedByName { get; set; } = "";
    public string SubmittedByRole { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public bool IsAcknowledged { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public string? AcknowledgedByName { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
