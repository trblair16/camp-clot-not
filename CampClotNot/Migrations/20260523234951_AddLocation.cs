using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationDisplayName",
                table: "ScheduleEvents");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "ScheduleEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivityId",
                table: "ScheduleEventGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "ScheduleEventGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "ScheduleEventGroups",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.LocationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_GroupId",
                table: "Users",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEvents_LocationId",
                table: "ScheduleEvents",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEventGroups_ActivityId",
                table: "ScheduleEventGroups",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEventGroups_LocationId",
                table: "ScheduleEventGroups",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEventGroups_Activities_ActivityId",
                table: "ScheduleEventGroups",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "ActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEventGroups_Locations_LocationId",
                table: "ScheduleEventGroups",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEvents_Locations_LocationId",
                table: "ScheduleEvents",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Groups_GroupId",
                table: "Users",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEventGroups_Activities_ActivityId",
                table: "ScheduleEventGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEventGroups_Locations_LocationId",
                table: "ScheduleEventGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEvents_Locations_LocationId",
                table: "ScheduleEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Groups_GroupId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Users_GroupId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEvents_LocationId",
                table: "ScheduleEvents");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEventGroups_ActivityId",
                table: "ScheduleEventGroups");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEventGroups_LocationId",
                table: "ScheduleEventGroups");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "ScheduleEvents");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "ScheduleEventGroups");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "ScheduleEventGroups");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "ScheduleEventGroups");

            migrationBuilder.AddColumn<string>(
                name: "LocationDisplayName",
                table: "ScheduleEvents",
                type: "text",
                nullable: true);
        }
    }
}
