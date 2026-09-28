using CampClotNot.Data.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    // Event structure
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Theme> Themes => Set<Theme>();

    // Capability system
    public DbSet<Capability> Capabilities => Set<Capability>();
    public DbSet<EventCapability> EventCapabilities => Set<EventCapability>();

    // Activity system
    public DbSet<ActivityTypeCategory> ActivityTypeCategories => Set<ActivityTypeCategory>();
    public DbSet<ActivityType> ActivityTypes => Set<ActivityType>();
    public DbSet<Activity> Activities => Set<Activity>();

    // Competition
    public DbSet<CurrencyType> CurrencyTypes => Set<CurrencyType>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<BoardSpace> BoardSpaces => Set<BoardSpace>();
    public DbSet<GroupBoardPos> GroupBoardPositions => Set<GroupBoardPos>();
    public DbSet<ScriptedBlockHit> ScriptedBlockHits => Set<ScriptedBlockHit>();
    public DbSet<ScriptedMiniGame> ScriptedMiniGames => Set<ScriptedMiniGame>();
    public DbSet<BowserScript> BowserScripts => Set<BowserScript>();

    // Awards
    public DbSet<AwardType> AwardTypes => Set<AwardType>();
    public DbSet<CamperAward> CamperAwards => Set<CamperAward>();

    // Auth / RBAC
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Authority> Authorities => Set<Authority>();
    public DbSet<UserRoleAuthorityLink> UserRoleAuthorityLinks => Set<UserRoleAuthorityLink>();
    public DbSet<UserAuthorityLink> UserAuthorityLinks => Set<UserAuthorityLink>();
    public DbSet<User> Users => Set<User>();
    public DbSet<EventStaff> EventStaff => Set<EventStaff>();

    // Hub (Camp Info)
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<InfoPage> InfoPages => Set<InfoPage>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<ScheduleItem> ScheduleItems => Set<ScheduleItem>();
    public DbSet<ScheduleItemGroup> ScheduleItemGroups => Set<ScheduleItemGroup>();
    public DbSet<ScheduleItemType> ScheduleItemTypes => Set<ScheduleItemType>();
    public DbSet<EventScheduleItemType> EventScheduleItemTypes => Set<EventScheduleItemType>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<CampDocument> CampDocuments => Set<CampDocument>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    // Guest identity
    public DbSet<GuestAttendee> GuestAttendees => Set<GuestAttendee>();
    public DbSet<GuestEventVisit> GuestEventVisits => Set<GuestEventVisit>();

    // Attendance
    public DbSet<ScheduleItemAttendance> ScheduleItemAttendances => Set<ScheduleItemAttendance>();
    public DbSet<ScheduleItemRegistration> ScheduleItemRegistrations => Set<ScheduleItemRegistration>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Non-conventional primary keys
        modelBuilder.Entity<ActivityTypeCategory>().HasKey(c => c.CategoryId);
        modelBuilder.Entity<BoardSpace>().HasKey(b => b.SpaceId);
        modelBuilder.Entity<Transaction>().HasKey(t => t.TxId);
        modelBuilder.Entity<ScriptedBlockHit>().HasKey(s => s.ScriptId);
        modelBuilder.Entity<ScriptedMiniGame>().HasKey(s => s.ScriptId);
        modelBuilder.Entity<CamperAward>().HasKey(a => a.AwardId);

        // GroupBoardPos: PK is also the FK to Group (one-to-one)
        modelBuilder.Entity<GroupBoardPos>()
            .HasKey(g => g.GroupId);
        modelBuilder.Entity<GroupBoardPos>()
            .HasOne(g => g.Group)
            .WithOne(g => g.BoardPos)
            .HasForeignKey<GroupBoardPos>(g => g.GroupId);

        // Link table composite keys
        modelBuilder.Entity<UserRoleAuthorityLink>()
            .HasKey(l => new { l.UserRoleId, l.AuthorityId });
        modelBuilder.Entity<UserAuthorityLink>()
            .HasKey(l => new { l.UserId, l.AuthorityId });

        // InfoPage: non-conventional PK (PageId, not InfoPageId)
        modelBuilder.Entity<InfoPage>().HasKey(p => p.PageId);

        // ScheduleItem: explicit PK + FK configs to avoid shadow property bugs
        modelBuilder.Entity<ScheduleItem>().HasKey(e => e.ScheduleItemId);
        modelBuilder.Entity<ScheduleItem>()
            .HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy);
        modelBuilder.Entity<ScheduleItem>()
            .HasOne(e => e.ScheduleItemType)
            .WithMany()
            .HasForeignKey(e => e.ScheduleItemTypeId);
        modelBuilder.Entity<ScheduleItem>()
            .HasOne(e => e.Activity)
            .WithMany()
            .HasForeignKey(e => e.ActivityId);
        // Breakout options → slot. Deleting a slot deletes its options (and, by cascade, their
        // sign-ups and check-ins).
        modelBuilder.Entity<ScheduleItem>()
            .HasOne(e => e.ParentScheduleItem)
            .WithMany(e => e.Options)
            .HasForeignKey(e => e.ParentScheduleItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        // QR check-in code (nullable — Postgres allows multiple NULLs under a unique index)
        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(e => e.CheckInCode)
            .IsUnique();

        // ScheduleItemGroup: composite PK
        modelBuilder.Entity<ScheduleItemGroup>()
            .HasKey(eg => new { eg.ScheduleItemId, eg.GroupId });
        modelBuilder.Entity<ScheduleItemGroup>()
            .HasOne(eg => eg.ScheduleItem)
            .WithMany(e => e.ItemGroups)
            .HasForeignKey(eg => eg.ScheduleItemId);

        // EventScheduleItemType: composite PK + FK configs
        modelBuilder.Entity<EventScheduleItemType>()
            .HasKey(e => new { e.EventId, e.ScheduleItemTypeId });
        modelBuilder.Entity<EventScheduleItemType>()
            .HasOne(e => e.Event)
            .WithMany()
            .HasForeignKey(e => e.EventId);
        modelBuilder.Entity<EventScheduleItemType>()
            .HasOne(e => e.ScheduleItemType)
            .WithMany()
            .HasForeignKey(e => e.ScheduleItemTypeId);

        // Announcement → Event: explicit FK to avoid shadow property bug
        modelBuilder.Entity<Announcement>()
            .HasOne(a => a.Event)
            .WithMany()
            .HasForeignKey(a => a.EventId);

        // InfoPage: unique index on Slug
        modelBuilder.Entity<InfoPage>()
            .HasIndex(p => p.Slug)
            .IsUnique();

        // Event: unique guest join code (nullable — Postgres allows multiple NULLs under a unique index)
        modelBuilder.Entity<Event>()
            .HasIndex(e => e.GuestCode)
            .IsUnique();

        // Activity → Location (optional): explicit FK to avoid shadow property bug
        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Location)
            .WithMany()
            .HasForeignKey(a => a.LocationId)
            .IsRequired(false);

        // IncidentReport → Location (optional): explicit FK to avoid shadow property bug
        modelBuilder.Entity<IncidentReport>()
            .HasOne(r => r.IncidentLocation)
            .WithMany()
            .HasForeignKey(r => r.IncidentLocationId)
            .IsRequired(false);

        // CampDocument: PK + explicit FK configs to avoid shadow property bugs
        modelBuilder.Entity<CampDocument>().HasKey(d => d.DocumentId);
        modelBuilder.Entity<CampDocument>()
            .HasOne(d => d.Event)
            .WithMany()
            .HasForeignKey(d => d.EventId);
        modelBuilder.Entity<CampDocument>()
            .HasOne(d => d.UploadedBy)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId);

        // GuestAttendee: unique on normalized name pair (the cross-event matching key)
        modelBuilder.Entity<GuestAttendee>()
            .HasIndex(g => new { g.NormalizedFirstName, g.NormalizedLastName })
            .IsUnique();

        // GuestEventVisit: composite unique (one row per guest per event) + explicit FKs
        modelBuilder.Entity<GuestEventVisit>()
            .HasIndex(v => new { v.GuestAttendeeId, v.EventId })
            .IsUnique();
        modelBuilder.Entity<GuestEventVisit>()
            .HasOne(v => v.GuestAttendee)
            .WithMany(g => g.Visits)
            .HasForeignKey(v => v.GuestAttendeeId);
        modelBuilder.Entity<GuestEventVisit>()
            .HasOne(v => v.Event)
            .WithMany()
            .HasForeignKey(v => v.EventId);

        // PasswordResetToken: two FKs point at Users, so both are configured explicitly.
        // Users are deactivated, never deleted, hence Restrict.
        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();
        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => new { t.UserId, t.CreatedAt });
        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.CreatedByUser)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ScheduleItemAttendance: one row per attendee per tracked item. Owned by exactly one of
        // GuestAttendeeId / UserId (check constraint). Two FKs point at Users, so every
        // relationship is configured explicitly to avoid shadow FK properties.
        modelBuilder.Entity<ScheduleItemAttendance>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_ScheduleItemAttendances_OneAttendee",
                "(\"GuestAttendeeId\" IS NULL) <> (\"UserId\" IS NULL)"));
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasIndex(a => new { a.ScheduleItemId, a.GuestAttendeeId })
            .IsUnique();
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasIndex(a => new { a.ScheduleItemId, a.UserId })
            .IsUnique();
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasOne(a => a.ScheduleItem)
            .WithMany()
            .HasForeignKey(a => a.ScheduleItemId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasOne(a => a.GuestAttendee)
            .WithMany()
            .HasForeignKey(a => a.GuestAttendeeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ScheduleItemAttendance>()
            .HasOne(a => a.CheckedInByUser)
            .WithMany()
            .HasForeignKey(a => a.CheckedInByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // EventStaff: one row per user per event. Three navigations point at reference tables
        // (Event, User, UserRole) plus an optional Group, all configured explicitly.
        modelBuilder.Entity<EventStaff>()
            .HasIndex(s => new { s.EventId, s.UserId })
            .IsUnique();
        modelBuilder.Entity<EventStaff>()
            .HasOne(s => s.Event)
            .WithMany()
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EventStaff>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EventStaff>()
            .HasOne(s => s.UserRole)
            .WithMany()
            .HasForeignKey(s => s.UserRoleId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EventStaff>()
            .HasOne(s => s.Group)
            .WithMany()
            .HasForeignKey(s => s.GroupId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ScheduleItemRegistration: same ownership rule as attendance, plus one option per person
        // per slot via the denormalized SlotScheduleItemId. Two navigations point at ScheduleItems
        // and two at Users, so all are configured explicitly.
        modelBuilder.Entity<ScheduleItemRegistration>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_ScheduleItemRegistrations_OneAttendee",
                "(\"GuestAttendeeId\" IS NULL) <> (\"UserId\" IS NULL)"));
        modelBuilder.Entity<ScheduleItemRegistration>()
            .HasIndex(r => new { r.SlotScheduleItemId, r.GuestAttendeeId })
            .IsUnique();
        modelBuilder.Entity<ScheduleItemRegistration>()
            .HasIndex(r => new { r.SlotScheduleItemId, r.UserId })
            .IsUnique();
        modelBuilder.Entity<ScheduleItemRegistration>()
            .HasOne(r => r.ScheduleItem)
            .WithMany()
            .HasForeignKey(r => r.ScheduleItemId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScheduleItemRegistration>()
            .HasOne(r => r.SlotScheduleItem)
            .WithMany()
            .HasForeignKey(r => r.SlotScheduleItemId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScheduleItemRegistration>()
            .HasOne(r => r.GuestAttendee)
            .WithMany()
            .HasForeignKey(r => r.GuestAttendeeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ScheduleItemRegistration>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ScheduleItemRegistration>()
            .HasOne(r => r.RegisteredByUser)
            .WithMany()
            .HasForeignKey(r => r.RegisteredByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
