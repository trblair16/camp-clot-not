using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleItemActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActivityId",
                table: "ScheduleItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_ActivityId",
                table: "ScheduleItems",
                column: "ActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItems_Activities_ActivityId",
                table: "ScheduleItems",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "ActivityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItems_Activities_ActivityId",
                table: "ScheduleItems");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleItems_ActivityId",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "ScheduleItems");
        }
    }
}
