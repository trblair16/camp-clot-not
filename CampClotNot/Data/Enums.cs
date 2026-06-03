namespace CampClotNot.Data;

public enum Currency { Primary, Prestige }

public enum AwardKind { Named, BigStick, Branch }

public enum Role { Admin, Staff, MedicalStaff, Volunteer }

public enum IncidentReportType { Internal = 0, ChildrensHarbor = 1 }

public enum Feature { BoardGame, CoinShop, MiniGameSpinner, Announcements, Itinerary }

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
