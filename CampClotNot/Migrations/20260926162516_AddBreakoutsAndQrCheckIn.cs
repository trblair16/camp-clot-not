using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddBreakoutsAndQrCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowSelfSignup",
                table: "ScheduleItems",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckInCode",
                table: "ScheduleItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBreakoutSlot",
                table: "ScheduleItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentScheduleItemId",
                table: "ScheduleItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SelfCheckInMode",
                table: "ScheduleItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ScheduleItemRegistrations",
                columns: table => new
                {
                    ScheduleItemRegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotScheduleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestAttendeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleItemRegistrations", x => x.ScheduleItemRegistrationId);
                    table.CheckConstraint("CK_ScheduleItemRegistrations_OneAttendee", "(\"GuestAttendeeId\" IS NULL) <> (\"UserId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ScheduleItemRegistrations_GuestAttendees_GuestAttendeeId",
                        column: x => x.GuestAttendeeId,
                        principalTable: "GuestAttendees",
                        principalColumn: "GuestAttendeeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleItemRegistrations_ScheduleItems_ScheduleItemId",
                        column: x => x.ScheduleItemId,
                        principalTable: "ScheduleItems",
                        principalColumn: "ScheduleItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleItemRegistrations_ScheduleItems_SlotScheduleItemId",
                        column: x => x.SlotScheduleItemId,
                        principalTable: "ScheduleItems",
                        principalColumn: "ScheduleItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleItemRegistrations_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleItemRegistrations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_CheckInCode",
                table: "ScheduleItems",
                column: "CheckInCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_ParentScheduleItemId",
                table: "ScheduleItems",
                column: "ParentScheduleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemRegistrations_GuestAttendeeId",
                table: "ScheduleItemRegistrations",
                column: "GuestAttendeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemRegistrations_RegisteredByUserId",
                table: "ScheduleItemRegistrations",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemRegistrations_ScheduleItemId",
                table: "ScheduleItemRegistrations",
                column: "ScheduleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemRegistrations_SlotScheduleItemId_GuestAttendeeId",
                table: "ScheduleItemRegistrations",
                columns: new[] { "SlotScheduleItemId", "GuestAttendeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemRegistrations_SlotScheduleItemId_UserId",
                table: "ScheduleItemRegistrations",
                columns: new[] { "SlotScheduleItemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemRegistrations_UserId",
                table: "ScheduleItemRegistrations",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItems_ScheduleItems_ParentScheduleItemId",
                table: "ScheduleItems",
                column: "ParentScheduleItemId",
                principalTable: "ScheduleItems",
                principalColumn: "ScheduleItemId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItems_ScheduleItems_ParentScheduleItemId",
                table: "ScheduleItems");

            migrationBuilder.DropTable(
                name: "ScheduleItemRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleItems_CheckInCode",
                table: "ScheduleItems");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleItems_ParentScheduleItemId",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "AllowSelfSignup",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "CheckInCode",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "IsBreakoutSlot",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "ParentScheduleItemId",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "SelfCheckInMode",
                table: "ScheduleItems");
        }
    }
}
