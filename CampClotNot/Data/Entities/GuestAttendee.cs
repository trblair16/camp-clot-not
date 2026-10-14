namespace CampClotNot.Data.Entities;

public class GuestAttendee
{
    public Guid GuestAttendeeId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string NormalizedFirstName { get; set; } = "";
    public string NormalizedLastName { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public ICollection<GuestEventVisit> Visits { get; set; } = new List<GuestEventVisit>();
}
