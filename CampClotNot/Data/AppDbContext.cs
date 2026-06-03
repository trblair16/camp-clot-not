using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
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

    // Awards
    public DbSet<AwardType> AwardTypes => Set<AwardType>();
    public DbSet<CamperAward> CamperAwards => Set<CamperAward>();

    // Auth / RBAC
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Authority> Authorities => Set<Authority>();
    public DbSet<UserRoleAuthorityLink> UserRoleAuthorityLinks => Set<UserRoleAuthorityLink>();
    public DbSet<UserAuthorityLink> UserAuthorityLinks => Set<UserAuthorityLink>();
    public DbSet<User> Users => Set<User>();

    // Hub (Camp Info)
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<InfoPage> InfoPages => Set<InfoPage>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<ScheduleItem> ScheduleItems => Set<ScheduleItem>();
    public DbSet<ScheduleItemGroup> ScheduleItemGroups => Set<ScheduleItemGroup>();
    public DbSet<ScheduleItemType> ScheduleItemTypes => Set<ScheduleItemType>();
    public DbSet<EventScheduleItemType> EventScheduleItemTypes => Set<EventScheduleItemType>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();

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

        // InfoPage: unique index on Slug
        modelBuilder.Entity<InfoPage>()
            .HasIndex(p => p.Slug)
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
    }
}
