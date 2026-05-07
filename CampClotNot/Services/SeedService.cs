using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class SeedService(IDbContextFactory<AppDbContext> factory, IConfiguration config, ILogger<SeedService> logger)
{
    // Stable IDs — never change these; they anchor FK references across re-seeds
    public static class Id
    {
        // UserRoles
        public static readonly Guid RoleAdmin   = new("00000001-0001-0001-0001-000000000001");
        public static readonly Guid RoleStaff   = new("00000001-0001-0001-0001-000000000002");
        public static readonly Guid RoleDisplay = new("00000001-0001-0001-0001-000000000003");

        // Authorities
        public static readonly Guid AuthLogTransaction   = new("00000002-0002-0002-0002-000000000001");
        public static readonly Guid AuthVoidTransaction  = new("00000002-0002-0002-0002-000000000002");
        public static readonly Guid AuthTriggerBlockHit  = new("00000002-0002-0002-0002-000000000003");
        public static readonly Guid AuthTriggerScoreLock = new("00000002-0002-0002-0002-000000000004");
        public static readonly Guid AuthManageUsers      = new("00000002-0002-0002-0002-000000000005");
        public static readonly Guid AuthManageGroups     = new("00000002-0002-0002-0002-000000000006");
        public static readonly Guid AuthManageBoard      = new("00000002-0002-0002-0002-000000000007");
        public static readonly Guid AuthManageShop       = new("00000002-0002-0002-0002-000000000008");
        public static readonly Guid AuthViewDisplay      = new("00000002-0002-0002-0002-000000000009");
        public static readonly Guid AuthAccessAdminPanel = new("00000002-0002-0002-0002-000000000010");

        // EventTypes
        public static readonly Guid EventTypeCcn = new("00000003-0003-0003-0003-000000000001");

        // Themes
        public static readonly Guid ThemeSuperMarioParty2026 = new("00000004-0004-0004-0004-000000000001");

        // Capabilities
        public static readonly Guid CapBoardGame       = new("00000005-0005-0005-0005-000000000001");
        public static readonly Guid CapCoinShop        = new("00000005-0005-0005-0005-000000000002");
        public static readonly Guid CapMiniGameSpinner = new("00000005-0005-0005-0005-000000000003");
        public static readonly Guid CapAnnouncements   = new("00000005-0005-0005-0005-000000000004");
        public static readonly Guid CapItinerary       = new("00000005-0005-0005-0005-000000000005");

        // ActivityTypeCategories — camp activities
        public static readonly Guid CatMinuteToWinIt   = new("00000006-0006-0006-0006-000000000001");
        public static readonly Guid CatWaterActivities = new("00000006-0006-0006-0006-000000000002");
        public static readonly Guid CatArts            = new("00000006-0006-0006-0006-000000000003");
        public static readonly Guid CatGames           = new("00000006-0006-0006-0006-000000000004");
        public static readonly Guid CatDiscussion      = new("00000006-0006-0006-0006-000000000005");

        // ActivityTypeCategories — board mechanic spaces (not real camp activities)
        public static readonly Guid CatBoardStart     = new("00000006-0006-0006-0006-000000000006");
        public static readonly Guid CatBoardCoinBonus = new("00000006-0006-0006-0006-000000000007");
        public static readonly Guid CatBoardPrestige  = new("00000006-0006-0006-0006-000000000008");
        public static readonly Guid CatBoardPenalty   = new("00000006-0006-0006-0006-000000000009");

        // CurrencyTypes
        public static readonly Guid CurrencyPrimary  = new("00000007-0007-0007-0007-000000000001");
        public static readonly Guid CurrencyPrestige = new("00000007-0007-0007-0007-000000000002");

        // AwardTypes
        public static readonly Guid AwardNamed    = new("00000008-0008-0008-0008-000000000001");
        public static readonly Guid AwardBigStick = new("00000008-0008-0008-0008-000000000002");
        public static readonly Guid AwardBranch   = new("00000008-0008-0008-0008-000000000003");

        // Events
        public static readonly Guid EventCcn2026 = new("00000009-0009-0009-0009-000000000001");

        // Groups (CCN 2026 — placeholder names until cabin groupings confirmed)
        public static readonly Guid Group1 = new("0000000a-000a-000a-000a-000000000001");
        public static readonly Guid Group2 = new("0000000a-000a-000a-000a-000000000002");
        public static readonly Guid Group3 = new("0000000a-000a-000a-000a-000000000003");
        public static readonly Guid Group4 = new("0000000a-000a-000a-000a-000000000004");
        public static readonly Guid Group5 = new("0000000a-000a-000a-000a-000000000005");
        public static readonly Guid Group6 = new("0000000a-000a-000a-000a-000000000006");

        // ActivityTypes — one placeholder per board space category + MinuteToWinIt
        public static readonly Guid ActTypeBoardStart      = new("0000000b-000b-000b-000b-000000000001");
        public static readonly Guid ActTypeBoardCoinBonus  = new("0000000b-000b-000b-000b-000000000002");
        public static readonly Guid ActTypeBoardPrestige   = new("0000000b-000b-000b-000b-000000000003");
        public static readonly Guid ActTypeBoardPenalty    = new("0000000b-000b-000b-000b-000000000004");
        public static readonly Guid ActTypeMtwiPlaceholder = new("0000000b-000b-000b-000b-000000000005");

        // Activities — one placeholder per type, scoped to CCN 2026
        // Real activities replace these when Katelyn/Vicki confirm the schedule
        public static readonly Guid ActBoardStart      = new("0000000c-000c-000c-000c-000000000001");
        public static readonly Guid ActBoardCoinBonus  = new("0000000c-000c-000c-000c-000000000002");
        public static readonly Guid ActBoardPrestige   = new("0000000c-000c-000c-000c-000000000003");
        public static readonly Guid ActBoardPenalty    = new("0000000c-000c-000c-000c-000000000004");
        public static readonly Guid ActMtwiPlaceholder = new("0000000c-000c-000c-000c-000000000005");

        // BoardSpaces — 20 spaces, layout matches mockup/ccn-mockup-v2.jsx
        public static readonly Guid Space0  = new("0000000d-000d-000d-000d-000000000001");
        public static readonly Guid Space1  = new("0000000d-000d-000d-000d-000000000002");
        public static readonly Guid Space2  = new("0000000d-000d-000d-000d-000000000003");
        public static readonly Guid Space3  = new("0000000d-000d-000d-000d-000000000004");
        public static readonly Guid Space4  = new("0000000d-000d-000d-000d-000000000005");
        public static readonly Guid Space5  = new("0000000d-000d-000d-000d-000000000006");
        public static readonly Guid Space6  = new("0000000d-000d-000d-000d-000000000007");
        public static readonly Guid Space7  = new("0000000d-000d-000d-000d-000000000008");
        public static readonly Guid Space8  = new("0000000d-000d-000d-000d-000000000009");
        public static readonly Guid Space9  = new("0000000d-000d-000d-000d-00000000000a");
        public static readonly Guid Space10 = new("0000000d-000d-000d-000d-00000000000b");
        public static readonly Guid Space11 = new("0000000d-000d-000d-000d-00000000000c");
        public static readonly Guid Space12 = new("0000000d-000d-000d-000d-00000000000d");
        public static readonly Guid Space13 = new("0000000d-000d-000d-000d-00000000000e");
        public static readonly Guid Space14 = new("0000000d-000d-000d-000d-00000000000f");
        public static readonly Guid Space15 = new("0000000d-000d-000d-000d-000000000010");
        public static readonly Guid Space16 = new("0000000d-000d-000d-000d-000000000011");
        public static readonly Guid Space17 = new("0000000d-000d-000d-000d-000000000012");
        public static readonly Guid Space18 = new("0000000d-000d-000d-000d-000000000013");
        public static readonly Guid Space19 = new("0000000d-000d-000d-000d-000000000014");
    }

    public async Task SeedAsync()
    {
        using var db = factory.CreateDbContext();
        await SeedUserRolesAsync(db);
        await SeedAuthoritiesAsync(db);
        await SeedUserRoleAuthoritiesAsync(db);
        await SeedEventTypesAsync(db);
        await SeedThemeAsync(db);
        await SeedCapabilitiesAsync(db);
        await SeedActivityTypeCategoriesAsync(db);
        await SeedCurrencyTypesAsync(db);
        await SeedAwardTypesAsync(db);
        await SeedEventAsync(db);
        await SeedEventCapabilitiesAsync(db);
        await SeedGroupsAsync(db);
        await SeedAdminUserAsync(db);
        await SeedBoardSpaceActivityTypesAsync(db);
        await SeedBoardSpaceActivitiesAsync(db);
        await SeedBoardSpacesAsync(db);
        await SeedGroupBoardPositionsAsync(db);
    }

    private async Task SeedUserRolesAsync(AppDbContext db)
    {
        if (await db.UserRoles.AnyAsync()) return;
        db.UserRoles.AddRange(
            new UserRole { UserRoleId = Id.RoleAdmin,   Name = "Admin",   Description = "Full system access",           SystemName = nameof(Role.Admin) },
            new UserRole { UserRoleId = Id.RoleStaff,   Name = "Staff",   Description = "Staff and volunteers",          SystemName = nameof(Role.Staff) },
            new UserRole { UserRoleId = Id.RoleDisplay, Name = "Display", Description = "Projector display (read-only)", SystemName = nameof(Role.Display) }
        );
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded UserRoles.");
    }

    private async Task SeedAuthoritiesAsync(AppDbContext db)
    {
        if (await db.Authorities.AnyAsync()) return;
        db.Authorities.AddRange(
            new Authority { AuthorityId = Id.AuthLogTransaction,   Name = "Log Transaction",    Description = "Post coins or stars to a group",      SystemName = nameof(Permission.LogTransaction) },
            new Authority { AuthorityId = Id.AuthVoidTransaction,  Name = "Void Transaction",   Description = "Void a posted transaction",            SystemName = nameof(Permission.VoidTransaction) },
            new Authority { AuthorityId = Id.AuthTriggerBlockHit,  Name = "Trigger Block Hit",  Description = "Trigger a scripted block hit",          SystemName = nameof(Permission.TriggerBlockHit) },
            new Authority { AuthorityId = Id.AuthTriggerScoreLock, Name = "Trigger Score Lock", Description = "Lock scores for end-of-day ceremony",   SystemName = nameof(Permission.TriggerScoreLock) },
            new Authority { AuthorityId = Id.AuthManageUsers,      Name = "Manage Users",       Description = "Create and deactivate user accounts",   SystemName = nameof(Permission.ManageUsers) },
            new Authority { AuthorityId = Id.AuthManageGroups,     Name = "Manage Groups",      Description = "Create and edit groups",                SystemName = nameof(Permission.ManageGroups) },
            new Authority { AuthorityId = Id.AuthManageBoard,      Name = "Manage Board",       Description = "Configure board spaces and scripting",  SystemName = nameof(Permission.ManageBoard) },
            new Authority { AuthorityId = Id.AuthManageShop,       Name = "Manage Shop",        Description = "Configure the coin shop",               SystemName = nameof(Permission.ManageShop) },
            new Authority { AuthorityId = Id.AuthViewDisplay,      Name = "View Display",       Description = "Access the projector display page",     SystemName = nameof(Permission.ViewDisplay) },
            new Authority { AuthorityId = Id.AuthAccessAdminPanel, Name = "Access Admin Panel", Description = "Access admin-only pages and controls",  SystemName = nameof(Permission.AccessAdminPanel) }
        );
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded Authorities.");
    }

    private async Task SeedUserRoleAuthoritiesAsync(AppDbContext db)
    {
        if (await db.UserRoleAuthorityLinks.AnyAsync()) return;

        var adminLinks = new[]
        {
            Id.AuthLogTransaction, Id.AuthVoidTransaction, Id.AuthTriggerBlockHit,
            Id.AuthTriggerScoreLock, Id.AuthManageUsers, Id.AuthManageGroups,
            Id.AuthManageBoard, Id.AuthManageShop, Id.AuthViewDisplay, Id.AuthAccessAdminPanel
        }.Select(authId => new UserRoleAuthorityLink { UserRoleId = Id.RoleAdmin, AuthorityId = authId });

        var staffLinks = new[] { Id.AuthLogTransaction, Id.AuthViewDisplay }
            .Select(authId => new UserRoleAuthorityLink { UserRoleId = Id.RoleStaff, AuthorityId = authId });

        var displayLinks = new[] { Id.AuthViewDisplay }
            .Select(authId => new UserRoleAuthorityLink { UserRoleId = Id.RoleDisplay, AuthorityId = authId });

        db.UserRoleAuthorityLinks.AddRange(adminLinks.Concat(staffLinks).Concat(displayLinks));
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded UserRoleAuthorityLinks.");
    }

    private async Task SeedEventTypesAsync(AppDbContext db)
    {
        if (await db.EventTypes.AnyAsync()) return;
        db.EventTypes.Add(new EventType
        {
            EventTypeId = Id.EventTypeCcn,
            Name        = "Camp Clot Not",
            Description = "Annual summer camp for kids with bleeding disorders",
            SystemName  = "CCN"
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded EventTypes.");
    }

    private async Task SeedThemeAsync(AppDbContext db)
    {
        if (await db.Themes.AnyAsync()) return;
        db.Themes.Add(new Theme
        {
            ThemeId     = Id.ThemeSuperMarioParty2026,
            Name        = "Super Mario Party",
            Year        = 2026,
            Description = "Super Mario Party themed camp — CCN 2026"
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded Themes.");
    }

    private async Task SeedCapabilitiesAsync(AppDbContext db)
    {
        if (await db.Capabilities.AnyAsync()) return;
        db.Capabilities.AddRange(
            new Capability { CapabilityId = Id.CapBoardGame,       Name = "Board Game",        Description = "Mario Party-style board game with block hits", SystemName = nameof(Feature.BoardGame) },
            new Capability { CapabilityId = Id.CapCoinShop,        Name = "Coin Shop",         Description = "Shop where groups spend coins for rewards",     SystemName = nameof(Feature.CoinShop) },
            new Capability { CapabilityId = Id.CapMiniGameSpinner, Name = "Mini-Game Spinner", Description = "Pre-scripted evening mini-game selector",        SystemName = nameof(Feature.MiniGameSpinner) },
            new Capability { CapabilityId = Id.CapAnnouncements,   Name = "Announcements",     Description = "Real-time schedule announcements",              SystemName = nameof(Feature.Announcements) },
            new Capability { CapabilityId = Id.CapItinerary,       Name = "Itinerary",         Description = "Camp itinerary and schedule",                    SystemName = nameof(Feature.Itinerary) }
        );
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded Capabilities.");
    }

    // Upsert-by-ID: v0.3.0 added board mechanic categories so AnyAsync() check would skip them on existing DBs
    private async Task SeedActivityTypeCategoriesAsync(AppDbContext db)
    {
        var defs = new[]
        {
            new { Id = Id.CatMinuteToWinIt,   Name = "Minute to Win It", Description = "Fast-paced individual or group challenges", SystemName = "MinuteToWinIt"   },
            new { Id = Id.CatWaterActivities, Name = "Water Activities", Description = "Pool and lake activities",                   SystemName = "WaterActivities" },
            new { Id = Id.CatArts,            Name = "Arts & Crafts",    Description = "Creative arts and craft projects",           SystemName = "Arts"            },
            new { Id = Id.CatGames,           Name = "Games",            Description = "Group games and competitions",               SystemName = "Games"           },
            new { Id = Id.CatDiscussion,      Name = "Discussion",       Description = "Devotions and group discussion sessions",    SystemName = "Discussion"      },
            new { Id = Id.CatBoardStart,     Name = "Board Start",   Description = "Starting space — groups begin here",       SystemName = "BoardStart"     },
            new { Id = Id.CatBoardCoinBonus, Name = "Coin Bonus",    Description = "Board space — land here to earn coins",    SystemName = "BoardCoinBonus" },
            new { Id = Id.CatBoardPrestige,  Name = "Star Space",    Description = "Board space — land here to earn a star",   SystemName = "BoardPrestige"  },
            new { Id = Id.CatBoardPenalty,   Name = "Bowser!",       Description = "Board space — Bowser penalty",             SystemName = "BoardPenalty"   },
        };

        foreach (var def in defs)
        {
            if (!await db.ActivityTypeCategories.AnyAsync(c => c.CategoryId == def.Id))
            {
                db.ActivityTypeCategories.Add(new ActivityTypeCategory
                {
                    CategoryId  = def.Id,
                    Name        = def.Name,
                    Description = def.Description,
                    SystemName  = def.SystemName
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded ActivityTypeCategories.");
    }

    private async Task SeedCurrencyTypesAsync(AppDbContext db)
    {
        if (await db.CurrencyTypes.AnyAsync()) return;
        db.CurrencyTypes.AddRange(
            new CurrencyType { CurrencyTypeId = Id.CurrencyPrimary,  Name = "Coins", Description = "Primary currency — awarded frequently throughout camp", SystemName = nameof(Currency.Primary),  Icon = "🪙" },
            new CurrencyType { CurrencyTypeId = Id.CurrencyPrestige, Name = "Stars", Description = "Prestige currency — rare, high-value awards",          SystemName = nameof(Currency.Prestige), Icon = "⭐" }
        );
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded CurrencyTypes.");
    }

    private async Task SeedAwardTypesAsync(AppDbContext db)
    {
        if (await db.AwardTypes.AnyAsync()) return;
        db.AwardTypes.AddRange(
            new AwardType { AwardTypeId = Id.AwardNamed,    Name = "Named Award",  Description = "Award given to a named individual camper", SystemName = nameof(AwardKind.Named) },
            new AwardType { AwardTypeId = Id.AwardBigStick, Name = "Big Stick",    Description = "Big stick award for outstanding effort",    SystemName = nameof(AwardKind.BigStick) },
            new AwardType { AwardTypeId = Id.AwardBranch,   Name = "Branch Award", Description = "Branch award for group excellence",         SystemName = nameof(AwardKind.Branch) }
        );
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded AwardTypes.");
    }

    private async Task SeedEventAsync(AppDbContext db)
    {
        if (await db.Events.AnyAsync()) return;
        db.Events.Add(new Event
        {
            EventId     = Id.EventCcn2026,
            EventTypeId = Id.EventTypeCcn,
            ThemeId     = Id.ThemeSuperMarioParty2026,
            Name        = "Camp Clot Not 2026",
            EffDate     = new DateOnly(2026, 6, 20),
            ExpDate     = new DateOnly(2026, 6, 25),
            IsActive    = true
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded Events.");
    }

    private async Task SeedEventCapabilitiesAsync(AppDbContext db)
    {
        if (await db.EventCapabilities.AnyAsync()) return;
        var capIds = new[] { Id.CapBoardGame, Id.CapCoinShop, Id.CapMiniGameSpinner, Id.CapAnnouncements, Id.CapItinerary };
        db.EventCapabilities.AddRange(capIds.Select(capId => new EventCapability
        {
            EventCapabilityId = Guid.NewGuid(),
            EventId           = Id.EventCcn2026,
            CapabilityId      = capId
        }));
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded EventCapabilities for CCN 2026.");
    }

    private async Task SeedGroupsAsync(AppDbContext db)
    {
        // Upsert by stable ID so name/color updates here apply on next app start.
        // Update this table when real cabin groupings are confirmed with Katelyn/Vicki/Amanda.
        var defs = new[]
        {
            new { Id = Id.Group1, Name = "Mario Group",       ShortName = "MA", Color = "#E74C3C" },
            new { Id = Id.Group2, Name = "Luigi Group",       ShortName = "LU", Color = "#27AE60" },
            new { Id = Id.Group3, Name = "Yoshi Group",       ShortName = "YO", Color = "#F1C40F" },
            new { Id = Id.Group4, Name = "Donkey Kong Group", ShortName = "DK", Color = "#E67E22" },
            new { Id = Id.Group5, Name = "Peach Group",       ShortName = "PE", Color = "#E91E8C" },
            new { Id = Id.Group6, Name = "Rosalina Group",    ShortName = "RO", Color = "#3498DB" },
        };

        foreach (var def in defs)
        {
            var existing = await db.Groups.FindAsync(def.Id);
            if (existing is null)
            {
                db.Groups.Add(new Group
                {
                    GroupId   = def.Id,
                    EventId   = Id.EventCcn2026,
                    Name      = def.Name,
                    ShortName = def.ShortName,
                    Color     = def.Color,
                });
            }
            else
            {
                existing.Name      = def.Name;
                existing.ShortName = def.ShortName;
                existing.Color     = def.Color;
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded/updated CCN 2026 groups.");
    }

    private async Task SeedAdminUserAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var email    = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No admin user seeded. Add Seed:AdminEmail and Seed:AdminPassword to appsettings.Development.json.");
            return;
        }

        db.Users.Add(new User
        {
            UserId       = Guid.NewGuid(),
            UserRoleId   = Id.RoleAdmin,
            FirstName    = "Camp",
            LastName     = "Admin",
            Email        = email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive     = true
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded admin user {Email}.", email);
    }

    // One placeholder ActivityType per board space category + one for MinuteToWinIt spaces
    private async Task SeedBoardSpaceActivityTypesAsync(AppDbContext db)
    {
        var defs = new[]
        {
            new { Id = Id.ActTypeBoardStart,      CategoryId = Id.CatBoardStart,     Name = "Start Space",    SystemName = "BoardStart"      },
            new { Id = Id.ActTypeBoardCoinBonus,  CategoryId = Id.CatBoardCoinBonus, Name = "Coin Bonus",     SystemName = "BoardCoinBonus"  },
            new { Id = Id.ActTypeBoardPrestige,   CategoryId = Id.CatBoardPrestige,  Name = "Star Space",     SystemName = "BoardPrestige"   },
            new { Id = Id.ActTypeBoardPenalty,    CategoryId = Id.CatBoardPenalty,   Name = "Bowser Penalty", SystemName = "BoardPenalty"    },
            new { Id = Id.ActTypeMtwiPlaceholder, CategoryId = Id.CatMinuteToWinIt,  Name = "Mini-Game",      SystemName = "MtwiPlaceholder" },
        };

        foreach (var def in defs)
        {
            if (!await db.ActivityTypes.AnyAsync(t => t.ActivityTypeId == def.Id))
            {
                db.ActivityTypes.Add(new ActivityType
                {
                    ActivityTypeId = def.Id,
                    CategoryId     = def.CategoryId,
                    Name           = def.Name,
                    Description    = def.Name,
                    SystemName     = def.SystemName
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded board space ActivityTypes.");
    }

    // One placeholder Activity per type, scoped to CCN 2026
    // Real MinuteToWinIt activities replace the placeholder when Katelyn/Vicki confirm the schedule
    private async Task SeedBoardSpaceActivitiesAsync(AppDbContext db)
    {
        var defs = new[]
        {
            new { Id = Id.ActBoardStart,      TypeId = Id.ActTypeBoardStart,      Name = "Start"       },
            new { Id = Id.ActBoardCoinBonus,  TypeId = Id.ActTypeBoardCoinBonus,  Name = "Coin Bonus"  },
            new { Id = Id.ActBoardPrestige,   TypeId = Id.ActTypeBoardPrestige,   Name = "Star Space"  },
            new { Id = Id.ActBoardPenalty,    TypeId = Id.ActTypeBoardPenalty,    Name = "Bowser!"     },
            new { Id = Id.ActMtwiPlaceholder, TypeId = Id.ActTypeMtwiPlaceholder, Name = "Mini-Game"   },
        };

        foreach (var def in defs)
        {
            if (!await db.Activities.AnyAsync(a => a.ActivityId == def.Id))
            {
                db.Activities.Add(new Activity
                {
                    ActivityId     = def.Id,
                    EventId        = Id.EventCcn2026,
                    ActivityTypeId = def.TypeId,
                    Name           = def.Name,
                    Description    = def.Name
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded board space Activities.");
    }

    // 20-space rectangular loop layout — clockwise: bottom → right → top → left.
    // XPos/YPos are SVG coordinates within a 600×440 viewBox.
    // Upserts coordinates so layout changes here apply on next restart.
    // Update space count here if Katelyn/Vicki confirm a different number.
    private async Task SeedBoardSpacesAsync(AppDbContext db)
    {
        var defs = new[]
        {
            // Bottom row — left → right (y=400)
            new { Id = Id.Space0,  SpaceIndex = 0,  ActivityId = Id.ActBoardStart,      XPos = 80f,  YPos = 400f },
            new { Id = Id.Space1,  SpaceIndex = 1,  ActivityId = Id.ActBoardCoinBonus,  XPos = 160f, YPos = 400f },
            new { Id = Id.Space2,  SpaceIndex = 2,  ActivityId = Id.ActMtwiPlaceholder, XPos = 240f, YPos = 400f },
            new { Id = Id.Space3,  SpaceIndex = 3,  ActivityId = Id.ActBoardCoinBonus,  XPos = 320f, YPos = 400f },
            new { Id = Id.Space4,  SpaceIndex = 4,  ActivityId = Id.ActBoardPenalty,    XPos = 400f, YPos = 400f },
            new { Id = Id.Space5,  SpaceIndex = 5,  ActivityId = Id.ActBoardCoinBonus,  XPos = 480f, YPos = 400f },
            // Right side — bottom → top (x=540)
            new { Id = Id.Space6,  SpaceIndex = 6,  ActivityId = Id.ActMtwiPlaceholder, XPos = 540f, YPos = 330f },
            new { Id = Id.Space7,  SpaceIndex = 7,  ActivityId = Id.ActBoardCoinBonus,  XPos = 540f, YPos = 250f },
            new { Id = Id.Space8,  SpaceIndex = 8,  ActivityId = Id.ActBoardPrestige,   XPos = 540f, YPos = 170f },
            new { Id = Id.Space9,  SpaceIndex = 9,  ActivityId = Id.ActBoardCoinBonus,  XPos = 540f, YPos = 90f  },
            // Top row — right → left (y=40)
            new { Id = Id.Space10, SpaceIndex = 10, ActivityId = Id.ActMtwiPlaceholder, XPos = 480f, YPos = 40f  },
            new { Id = Id.Space11, SpaceIndex = 11, ActivityId = Id.ActBoardPrestige,   XPos = 400f, YPos = 40f  },
            new { Id = Id.Space12, SpaceIndex = 12, ActivityId = Id.ActBoardCoinBonus,  XPos = 320f, YPos = 40f  },
            new { Id = Id.Space13, SpaceIndex = 13, ActivityId = Id.ActMtwiPlaceholder, XPos = 240f, YPos = 40f  },
            new { Id = Id.Space14, SpaceIndex = 14, ActivityId = Id.ActBoardPenalty,    XPos = 160f, YPos = 40f  },
            new { Id = Id.Space15, SpaceIndex = 15, ActivityId = Id.ActBoardPrestige,   XPos = 80f,  YPos = 40f  },
            // Left side — top → bottom (x=60)
            new { Id = Id.Space16, SpaceIndex = 16, ActivityId = Id.ActBoardCoinBonus,  XPos = 60f,  YPos = 110f },
            new { Id = Id.Space17, SpaceIndex = 17, ActivityId = Id.ActMtwiPlaceholder, XPos = 60f,  YPos = 190f },
            new { Id = Id.Space18, SpaceIndex = 18, ActivityId = Id.ActBoardPrestige,   XPos = 60f,  YPos = 270f },
            new { Id = Id.Space19, SpaceIndex = 19, ActivityId = Id.ActBoardPenalty,    XPos = 60f,  YPos = 350f },
        };

        foreach (var def in defs)
        {
            var existing = await db.BoardSpaces.FindAsync(def.Id);
            if (existing is null)
            {
                db.BoardSpaces.Add(new BoardSpace
                {
                    SpaceId    = def.Id,
                    EventId    = Id.EventCcn2026,
                    ActivityId = def.ActivityId,
                    SpaceIndex = def.SpaceIndex,
                    XPos       = def.XPos,
                    YPos       = def.YPos
                });
            }
            else
            {
                existing.ActivityId = def.ActivityId;
                existing.SpaceIndex = def.SpaceIndex;
                existing.XPos       = def.XPos;
                existing.YPos       = def.YPos;
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded/updated BoardSpaces (20-space rectangular loop, CCN 2026).");
    }

    // Initialize all groups at the Start space (index 0); skip groups that already have a position
    private async Task SeedGroupBoardPositionsAsync(AppDbContext db)
    {
        var groupIds = new[] { Id.Group1, Id.Group2, Id.Group3, Id.Group4, Id.Group5, Id.Group6 };
        foreach (var groupId in groupIds)
        {
            if (!await db.GroupBoardPositions.AnyAsync(p => p.GroupId == groupId))
            {
                db.GroupBoardPositions.Add(new GroupBoardPos
                {
                    GroupId    = groupId,
                    SpaceIndex = 0,
                    UpdatedAt  = DateTime.UtcNow
                });
            }
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded GroupBoardPositions.");
    }
}
