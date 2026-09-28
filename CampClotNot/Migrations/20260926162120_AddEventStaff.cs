using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddEventStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventStaff",
                columns: table => new
                {
                    EventStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStaff", x => x.EventStaffId);
                    table.ForeignKey(
                        name: "FK_EventStaff_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventStaff_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "GroupId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EventStaff_UserRoles_UserRoleId",
                        column: x => x.UserRoleId,
                        principalTable: "UserRoles",
                        principalColumn: "UserRoleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventStaff_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventStaff_EventId_UserId",
                table: "EventStaff",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventStaff_GroupId",
                table: "EventStaff",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EventStaff_UserId",
                table: "EventStaff",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventStaff_UserRoleId",
                table: "EventStaff",
                column: "UserRoleId");

            // Backfill: every active user becomes staff at every existing event with their current
            // role. A Volunteer's group only carries over to the event that group belongs to.
            migrationBuilder.Sql(@"
INSERT INTO ""EventStaff"" (""EventStaffId"", ""EventId"", ""UserId"", ""UserRoleId"", ""GroupId"", ""AddedAt"")
SELECT gen_random_uuid(), e.""EventId"", u.""UserId"", u.""UserRoleId"",
       CASE WHEN g.""EventId"" = e.""EventId"" THEN u.""GroupId"" END,
       now()
FROM ""Users"" u
CROSS JOIN ""Events"" e
LEFT JOIN ""Groups"" g ON g.""GroupId"" = u.""GroupId""
WHERE u.""IsActive"";");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Groups_GroupId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_GroupId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GroupId",
                table: "Users",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Groups_GroupId",
                table: "Users",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId");

            migrationBuilder.Sql(@"
UPDATE ""Users"" u SET ""GroupId"" = s.""GroupId""
FROM ""EventStaff"" s
WHERE s.""UserId"" = u.""UserId"" AND s.""GroupId"" IS NOT NULL;");

            migrationBuilder.DropTable(
                name: "EventStaff");
        }
    }
}
