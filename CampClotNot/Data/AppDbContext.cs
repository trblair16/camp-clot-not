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
    }
}
