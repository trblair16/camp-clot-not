using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class FixScheduleEventCreatedByFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEvents_Users_CreatedByUserUserId",
                table: "ScheduleEvents");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEvents_CreatedByUserUserId",
                table: "ScheduleEvents");

            migrationBuilder.DropColumn(
                name: "CreatedByUserUserId",
                table: "ScheduleEvents");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEvents_CreatedBy",
                table: "ScheduleEvents",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEvents_Users_CreatedBy",
                table: "ScheduleEvents",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEvents_Users_CreatedBy",
                table: "ScheduleEvents");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEvents_CreatedBy",
                table: "ScheduleEvents");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserUserId",
                table: "ScheduleEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEvents_CreatedByUserUserId",
                table: "ScheduleEvents",
                column: "CreatedByUserUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEvents_Users_CreatedByUserUserId",
                table: "ScheduleEvents",
                column: "CreatedByUserUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
