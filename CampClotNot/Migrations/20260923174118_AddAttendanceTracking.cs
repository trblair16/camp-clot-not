using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TrackAttendance",
                table: "ScheduleItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ScheduleItemAttendances",
                columns: table => new
                {
                    ScheduleItemAttendanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestAttendeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    CheckedInByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleItemAttendances", x => x.ScheduleItemAttendanceId);
                    table.CheckConstraint("CK_ScheduleItemAttendances_OneAttendee", "(\"GuestAttendeeId\" IS NULL) <> (\"UserId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ScheduleItemAttendances_GuestAttendees_GuestAttendeeId",
                        column: x => x.GuestAttendeeId,
                        principalTable: "GuestAttendees",
                        principalColumn: "GuestAttendeeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleItemAttendances_ScheduleItems_ScheduleItemId",
                        column: x => x.ScheduleItemId,
                        principalTable: "ScheduleItems",
                        principalColumn: "ScheduleItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleItemAttendances_Users_CheckedInByUserId",
                        column: x => x.CheckedInByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleItemAttendances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemAttendances_CheckedInByUserId",
                table: "ScheduleItemAttendances",
                column: "CheckedInByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemAttendances_GuestAttendeeId",
                table: "ScheduleItemAttendances",
                column: "GuestAttendeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemAttendances_ScheduleItemId_GuestAttendeeId",
                table: "ScheduleItemAttendances",
                columns: new[] { "ScheduleItemId", "GuestAttendeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemAttendances_ScheduleItemId_UserId",
                table: "ScheduleItemAttendances",
                columns: new[] { "ScheduleItemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemAttendances_UserId",
                table: "ScheduleItemAttendances",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleItemAttendances");

            migrationBuilder.DropColumn(
                name: "TrackAttendance",
                table: "ScheduleItems");
        }
    }
}
