using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementEventScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "Announcements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000009-0009-0009-0009-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_EventId",
                table: "Announcements",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Events_EventId",
                table: "Announcements",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Events_EventId",
                table: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_EventId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Announcements");
        }
    }
}
