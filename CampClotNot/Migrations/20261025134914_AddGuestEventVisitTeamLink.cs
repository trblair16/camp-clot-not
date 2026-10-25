using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestEventVisitTeamLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedEventStaffId",
                table: "GuestEventVisits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestEventVisits_LinkedEventStaffId",
                table: "GuestEventVisits",
                column: "LinkedEventStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuestEventVisits_EventStaff_LinkedEventStaffId",
                table: "GuestEventVisits",
                column: "LinkedEventStaffId",
                principalTable: "EventStaff",
                principalColumn: "EventStaffId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuestEventVisits_EventStaff_LinkedEventStaffId",
                table: "GuestEventVisits");

            migrationBuilder.DropIndex(
                name: "IX_GuestEventVisits_LinkedEventStaffId",
                table: "GuestEventVisits");

            migrationBuilder.DropColumn(
                name: "LinkedEventStaffId",
                table: "GuestEventVisits");
        }
    }
}
