namespace CampClotNot.Data.Entities;

public class StaffMember
{
    public Guid StaffMemberId { get; set; }
    public string DisplayName { get; set; } = "";
    public string RoleTitle { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string AvatarEmoji { get; set; } = "👤";
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid? LinkedUserId { get; set; }
    public User? LinkedUser { get; set; }
}
