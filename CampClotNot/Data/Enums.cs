namespace CampClotNot.Data;

public enum Currency { Primary, Prestige }

public enum AwardKind { Named, BigStick, Branch }

public enum Role { Admin, Staff, MedicalStaff, Volunteer }

public enum IncidentReportType { Internal = 0, ChildrensHarbor = 1 }

public enum AttendanceMethod { Self = 0, Admin = 1 }

public enum PasswordTokenPurpose { SelfReset = 0, AdminReset = 1, Invite = 2 }

public enum Feature { BoardGame, CoinShop, MiniGameSpinner, Announcements, Itinerary, BowserEvent, Awards }

public enum Permission
{
    LogTransaction,
    VoidTransaction,
    TriggerBlockHit,
    TriggerScoreLock,
    ManageUsers,
    ManageGroups,
    ManageBoard,
    ManageShop,
    AccessAdminPanel
}
