using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffMemberEventScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampEventId",
                table: "StaffMembers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000009-0009-0009-0009-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_CampEventId",
                table: "StaffMembers",
                column: "CampEventId");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffMembers_Events_CampEventId",
                table: "StaffMembers",
                column: "CampEventId",
                principalTable: "Events",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaffMembers_Events_CampEventId",
                table: "StaffMembers");

            migrationBuilder.DropIndex(
                name: "IX_StaffMembers_CampEventId",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "CampEventId",
                table: "StaffMembers");
        }
    }
}
