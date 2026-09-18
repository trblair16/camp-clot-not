using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestAttendee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestAttendees",
                columns: table => new
                {
                    GuestAttendeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    NormalizedFirstName = table.Column<string>(type: "text", nullable: false),
                    NormalizedLastName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestAttendees", x => x.GuestAttendeeId);
                });

            migrationBuilder.CreateTable(
                name: "GuestEventVisits",
                columns: table => new
                {
                    GuestEventVisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestAttendeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstJoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestEventVisits", x => x.GuestEventVisitId);
                    table.ForeignKey(
                        name: "FK_GuestEventVisits_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuestEventVisits_GuestAttendees_GuestAttendeeId",
                        column: x => x.GuestAttendeeId,
                        principalTable: "GuestAttendees",
                        principalColumn: "GuestAttendeeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestAttendees_NormalizedFirstName_NormalizedLastName",
                table: "GuestAttendees",
                columns: new[] { "NormalizedFirstName", "NormalizedLastName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestEventVisits_EventId",
                table: "GuestEventVisits",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestEventVisits_GuestAttendeeId_EventId",
                table: "GuestEventVisits",
                columns: new[] { "GuestAttendeeId", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestEventVisits");

            migrationBuilder.DropTable(
                name: "GuestAttendees");
        }
    }
}
