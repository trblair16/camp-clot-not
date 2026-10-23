using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddSponsorAds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SponsorAds",
                columns: table => new
                {
                    SponsorAdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SponsorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkUrl = table.Column<string>(type: "text", nullable: true),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: false),
                    ImageContentType = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorAds", x => x.SponsorAdId);
                    table.ForeignKey(
                        name: "FK_SponsorAds_Sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Sponsors",
                        principalColumn: "SponsorId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EventAds",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SponsorAdId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ClickCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventAds", x => new { x.EventId, x.SponsorAdId });
                    table.ForeignKey(
                        name: "FK_EventAds_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventAds_SponsorAds_SponsorAdId",
                        column: x => x.SponsorAdId,
                        principalTable: "SponsorAds",
                        principalColumn: "SponsorAdId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventAds_SponsorAdId",
                table: "EventAds",
                column: "SponsorAdId");

            migrationBuilder.CreateIndex(
                name: "IX_SponsorAds_SponsorId",
                table: "SponsorAds",
                column: "SponsorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventAds");

            migrationBuilder.DropTable(
                name: "SponsorAds");
        }
    }
}
