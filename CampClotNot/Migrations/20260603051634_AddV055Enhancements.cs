using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddV055Enhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "StaffMembers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhotoData",
                table: "StaffMembers",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "Sponsors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Sponsors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresenterBio",
                table: "ScheduleEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresenterName",
                table: "ScheduleEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Locations",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IncidentLocationId",
                table: "IncidentReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncidentLocationOther",
                table: "IncidentReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportType",
                table: "IncidentReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "Activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReports_IncidentLocationId",
                table: "IncidentReports",
                column: "IncidentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_LocationId",
                table: "Activities",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Locations_LocationId",
                table: "Activities",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentReports_Locations_IncidentLocationId",
                table: "IncidentReports",
                column: "IncidentLocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Locations_LocationId",
                table: "Activities");

            migrationBuilder.DropForeignKey(
                name: "FK_IncidentReports_Locations_IncidentLocationId",
                table: "IncidentReports");

            migrationBuilder.DropIndex(
                name: "IX_IncidentReports_IncidentLocationId",
                table: "IncidentReports");

            migrationBuilder.DropIndex(
                name: "IX_Activities_LocationId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "PhotoData",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "PresenterBio",
                table: "ScheduleEvents");

            migrationBuilder.DropColumn(
                name: "PresenterName",
                table: "ScheduleEvents");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "IncidentLocationId",
                table: "IncidentReports");

            migrationBuilder.DropColumn(
                name: "IncidentLocationOther",
                table: "IncidentReports");

            migrationBuilder.DropColumn(
                name: "ReportType",
                table: "IncidentReports");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Activities");
        }
    }
}
