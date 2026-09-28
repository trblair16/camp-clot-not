using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class MergeStaffDirectoryIntoTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarEmoji",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "👤");

            migrationBuilder.AddColumn<bool>(
                name: "CanSignIn",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);   // everyone who exists today can sign in

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhotoData",
                table: "Users",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoObjectPosition",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInDirectory",
                table: "EventStaff",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "EventStaff",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "EventStaff",
                type: "text",
                nullable: true);

            // ── Carry the old per-event directory cards (StaffMembers) over ──────────────────
            // 1. Cards linked to an account: photo, phone, and emoji move onto the person (prefer a
            //    card with a photo, then the most recent event's).
            migrationBuilder.Sql(@"
WITH card AS (
    SELECT DISTINCT ON (sm.""LinkedUserId"") sm.*
    FROM ""StaffMembers"" sm JOIN ""Events"" e ON e.""EventId"" = sm.""CampEventId""
    WHERE sm.""LinkedUserId"" IS NOT NULL
    ORDER BY sm.""LinkedUserId"", (sm.""PhotoData"" IS NULL), e.""EffDate"" DESC
)
UPDATE ""Users"" u SET
    ""Phone""               = NULLIF(btrim(card.""Phone""), ''),
    ""PhotoData""           = card.""PhotoData"",
    ""PhotoContentType""    = card.""PhotoContentType"",
    ""PhotoObjectPosition"" = card.""PhotoObjectPosition"",
    ""AvatarEmoji""         = COALESCE(NULLIF(card.""AvatarEmoji"", ''), '👤')
FROM card WHERE card.""LinkedUserId"" = u.""UserId"";");

            //    …and their title, visibility, and order onto that event's team row (added if missing).
            migrationBuilder.Sql(@"
UPDATE ""EventStaff"" s SET
    ""Title""           = NULLIF(btrim(sm.""RoleTitle""), ''),
    ""ShowInDirectory"" = sm.""IsVisible"",
    ""SortOrder""       = sm.""SortOrder""
FROM (SELECT DISTINCT ON (""CampEventId"", ""LinkedUserId"") * FROM ""StaffMembers""
      WHERE ""LinkedUserId"" IS NOT NULL ORDER BY ""CampEventId"", ""LinkedUserId"", ""SortOrder"") sm
WHERE s.""EventId"" = sm.""CampEventId"" AND s.""UserId"" = sm.""LinkedUserId"";

INSERT INTO ""EventStaff"" (""EventStaffId"", ""EventId"", ""UserId"", ""UserRoleId"", ""GroupId"", ""AddedAt"", ""Title"", ""ShowInDirectory"", ""SortOrder"")
SELECT gen_random_uuid(), sm.""CampEventId"", sm.""LinkedUserId"", u.""UserRoleId"", NULL, now(),
       NULLIF(btrim(sm.""RoleTitle""), ''), sm.""IsVisible"", sm.""SortOrder""
FROM (SELECT DISTINCT ON (""CampEventId"", ""LinkedUserId"") * FROM ""StaffMembers""
      WHERE ""LinkedUserId"" IS NOT NULL ORDER BY ""CampEventId"", ""LinkedUserId"", ""SortOrder"") sm
JOIN ""Users"" u ON u.""UserId"" = sm.""LinkedUserId""
WHERE NOT EXISTS (SELECT 1 FROM ""EventStaff"" s WHERE s.""EventId"" = sm.""CampEventId"" AND s.""UserId"" = sm.""LinkedUserId"");");

            // 2. Cards not linked to anyone (a nurse line, a speaker): one listed-only person per
            //    distinct name (they can't sign in), on each event's team where they had a card.
            migrationBuilder.Sql(@"
WITH staff_role AS (
    SELECT ""UserRoleId"" FROM ""UserRoles"" WHERE ""SystemName"" = 'Staff'
),
person AS MATERIALIZED (
    SELECT DISTINCT ON (lower(btrim(sm.""DisplayName""))) sm.*, gen_random_uuid() AS ""NewUserId""
    FROM ""StaffMembers"" sm JOIN ""Events"" e ON e.""EventId"" = sm.""CampEventId""
    WHERE sm.""LinkedUserId"" IS NULL AND btrim(sm.""DisplayName"") <> ''
    ORDER BY lower(btrim(sm.""DisplayName"")), (sm.""PhotoData"" IS NULL), e.""EffDate"" DESC
),
inserted AS (
    INSERT INTO ""Users"" (""UserId"", ""UserRoleId"", ""FirstName"", ""LastName"", ""Email"", ""PasswordHash"",
                         ""IsActive"", ""MustChangePassword"", ""Phone"", ""PhotoData"", ""PhotoContentType"",
                         ""PhotoObjectPosition"", ""AvatarEmoji"", ""CanSignIn"")
    SELECT p.""NewUserId"", (SELECT ""UserRoleId"" FROM staff_role),
           split_part(btrim(p.""DisplayName""), ' ', 1),
           btrim(substr(btrim(p.""DisplayName""), length(split_part(btrim(p.""DisplayName""), ' ', 1)) + 1)),
           lower(COALESCE(btrim(p.""Email""), '')), '', true, false,
           NULLIF(btrim(p.""Phone""), ''), p.""PhotoData"", p.""PhotoContentType"", p.""PhotoObjectPosition"",
           COALESCE(NULLIF(p.""AvatarEmoji"", ''), '👤'), false
    FROM person p
    RETURNING ""UserId""
)
INSERT INTO ""EventStaff"" (""EventStaffId"", ""EventId"", ""UserId"", ""UserRoleId"", ""GroupId"", ""AddedAt"", ""Title"", ""ShowInDirectory"", ""SortOrder"")
SELECT gen_random_uuid(), sm.""CampEventId"", p.""NewUserId"", (SELECT ""UserRoleId"" FROM staff_role), NULL, now(),
       NULLIF(btrim(sm.""RoleTitle""), ''), sm.""IsVisible"", sm.""SortOrder""
FROM (SELECT DISTINCT ON (""CampEventId"", lower(btrim(""DisplayName""))) * FROM ""StaffMembers""
      WHERE ""LinkedUserId"" IS NULL AND btrim(""DisplayName"") <> ''
      ORDER BY ""CampEventId"", lower(btrim(""DisplayName"")), ""SortOrder"") sm
JOIN person p ON lower(btrim(p.""DisplayName"")) = lower(btrim(sm.""DisplayName""))
JOIN inserted i ON i.""UserId"" = p.""NewUserId"";");

            migrationBuilder.DropTable(
                name: "StaffMembers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AvatarEmoji = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    PhotoContentType = table.Column<string>(type: "text", nullable: true),
                    PhotoData = table.Column<byte[]>(type: "bytea", nullable: true),
                    PhotoObjectPosition = table.Column<string>(type: "text", nullable: true),
                    RoleTitle = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.StaffMemberId);
                    table.ForeignKey(
                        name: "FK_StaffMembers_Events_CampEventId",
                        column: x => x.CampEventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffMembers_Users_LinkedUserId",
                        column: x => x.LinkedUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_CampEventId",
                table: "StaffMembers",
                column: "CampEventId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_LinkedUserId",
                table: "StaffMembers",
                column: "LinkedUserId");

            // Best effort: rebuild directory cards from the team rows shown in the directory.
            // (Listed-only people stay as users; they can't sign in with an empty password.)
            migrationBuilder.Sql(@"
INSERT INTO ""StaffMembers"" (""StaffMemberId"", ""CampEventId"", ""DisplayName"", ""RoleTitle"", ""Phone"", ""Email"",
    ""PhotoData"", ""PhotoContentType"", ""PhotoObjectPosition"", ""AvatarEmoji"", ""IsVisible"", ""SortOrder"", ""LinkedUserId"")
SELECT gen_random_uuid(), s.""EventId"", btrim(u.""FirstName"" || ' ' || u.""LastName""), COALESCE(s.""Title"", ''),
    u.""Phone"", NULLIF(u.""Email"", ''), u.""PhotoData"", u.""PhotoContentType"", u.""PhotoObjectPosition"", u.""AvatarEmoji"",
    true, s.""SortOrder"", CASE WHEN u.""CanSignIn"" THEN u.""UserId"" END
FROM ""EventStaff"" s JOIN ""Users"" u ON u.""UserId"" = s.""UserId""
WHERE s.""ShowInDirectory"";");

            migrationBuilder.DropColumn(
                name: "AvatarEmoji",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanSignIn",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhotoData",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhotoObjectPosition",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowInDirectory",
                table: "EventStaff");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "EventStaff");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "EventStaff");
        }
    }
}
