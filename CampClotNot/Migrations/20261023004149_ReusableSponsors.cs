using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <summary>
    /// Reusable sponsors (#341). Sponsors stop belonging to one event: each existing row is linked
    /// to its event through the new EventSponsors table (keeping its order), sponsors with the same
    /// name are merged into one, and Sponsors.EventId/SortOrder are dropped.
    /// </summary>
    public partial class ReusableSponsors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventSponsors",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SponsorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSponsors", x => new { x.EventId, x.SponsorId });
                    table.ForeignKey(
                        name: "FK_EventSponsors_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventSponsors_Sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Sponsors",
                        principalColumn: "SponsorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventSponsors_SponsorId",
                table: "EventSponsors",
                column: "SponsorId");

            // 1. Every sponsor stays on the event it belonged to, in the same order.
            migrationBuilder.Sql("""
                INSERT INTO "EventSponsors" ("EventId", "SponsorId", "SortOrder")
                SELECT "EventId", "SponsorId", "SortOrder" FROM "Sponsors";
                """);

            // 2. Merge same-name sponsors (ignoring case and surrounding spaces). The keeper is the one
            //    with an uploaded logo, then the most recent event's copy.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE sponsor_merge AS
                WITH ranked AS (
                    SELECT s."SponsorId", lower(btrim(s."Name")) AS name_key,
                           ROW_NUMBER() OVER (PARTITION BY lower(btrim(s."Name"))
                                              ORDER BY (s."LogoData" IS NOT NULL) DESC, e."EffDate" DESC, s."SponsorId") AS rn
                    FROM "Sponsors" s
                    JOIN "Events" e ON e."EventId" = s."EventId"
                )
                SELECT d."SponsorId" AS dup, k."SponsorId" AS keeper
                FROM ranked d
                JOIN ranked k ON k.name_key = d.name_key AND k.rn = 1
                WHERE d.rn > 1;

                -- Fill details the keeper is missing from its duplicates.
                UPDATE "Sponsors" k SET
                    "LogoUrl"     = COALESCE(NULLIF(k."LogoUrl", ''),     f.logo_url),
                    "Website"     = COALESCE(NULLIF(k."Website", ''),     f.website),
                    "ContactName" = COALESCE(NULLIF(k."ContactName", ''), f.contact_name),
                    "Phone"       = COALESCE(NULLIF(k."Phone", ''),       f.phone)
                FROM (
                    SELECT m.keeper,
                           (array_agg(s."LogoUrl")     FILTER (WHERE s."LogoUrl"     <> ''))[1] AS logo_url,
                           (array_agg(s."Website")     FILTER (WHERE s."Website"     <> ''))[1] AS website,
                           (array_agg(s."ContactName") FILTER (WHERE s."ContactName" <> ''))[1] AS contact_name,
                           (array_agg(s."Phone")       FILTER (WHERE s."Phone"       <> ''))[1] AS phone
                    FROM sponsor_merge m
                    JOIN "Sponsors" s ON s."SponsorId" = m.dup
                    GROUP BY m.keeper
                ) f
                WHERE k."SponsorId" = f.keeper;

                -- Move the duplicates' event links to the keeper. Where the keeper is already on that
                -- event, it keeps the lower sort order.
                INSERT INTO "EventSponsors" ("EventId", "SponsorId", "SortOrder")
                SELECT es."EventId", m.keeper, MIN(es."SortOrder")
                FROM "EventSponsors" es
                JOIN sponsor_merge m ON m.dup = es."SponsorId"
                WHERE NOT EXISTS (SELECT 1 FROM "EventSponsors" x WHERE x."EventId" = es."EventId" AND x."SponsorId" = m.keeper)
                GROUP BY es."EventId", m.keeper;

                UPDATE "EventSponsors" x SET "SortOrder" = LEAST(x."SortOrder", d.sort_order)
                FROM (
                    SELECT es."EventId", m.keeper, MIN(es."SortOrder") AS sort_order
                    FROM "EventSponsors" es
                    JOIN sponsor_merge m ON m.dup = es."SponsorId"
                    GROUP BY es."EventId", m.keeper
                ) d
                WHERE x."EventId" = d."EventId" AND x."SponsorId" = d.keeper;

                -- The duplicates' own links go with them (cascade).
                DELETE FROM "Sponsors" WHERE "SponsorId" IN (SELECT dup FROM sponsor_merge);
                DROP TABLE sponsor_merge;
                """);

            // 3. The event and order now live on EventSponsors.
            migrationBuilder.DropForeignKey(
                name: "FK_Sponsors_Events_EventId",
                table: "Sponsors");

            migrationBuilder.DropIndex(
                name: "IX_Sponsors_EventId",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Sponsors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "Sponsors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Sponsors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Back to one row per event: a sponsor keeps its first link, and gets a copy for each other
            // event it's on. Sponsors on no event are dropped (the old schema can't hold them).
            migrationBuilder.Sql("""
                UPDATE "Sponsors" s SET "EventId" = f."EventId", "SortOrder" = f."SortOrder"
                FROM (SELECT DISTINCT ON ("SponsorId") "SponsorId", "EventId", "SortOrder"
                      FROM "EventSponsors" ORDER BY "SponsorId", "EventId") f
                WHERE s."SponsorId" = f."SponsorId";

                INSERT INTO "Sponsors" ("SponsorId", "EventId", "SortOrder", "Name", "LogoUrl", "LogoData",
                                        "LogoContentType", "Website", "ContactName", "Phone")
                SELECT gen_random_uuid(), es."EventId", es."SortOrder", s."Name", s."LogoUrl", s."LogoData",
                       s."LogoContentType", s."Website", s."ContactName", s."Phone"
                FROM "EventSponsors" es
                JOIN "Sponsors" s ON s."SponsorId" = es."SponsorId"
                WHERE es."EventId" <> s."EventId";

                DELETE FROM "Sponsors" WHERE "EventId" IS NULL;
                """);

            migrationBuilder.DropTable(
                name: "EventSponsors");

            migrationBuilder.AlterColumn<Guid>(
                name: "EventId",
                table: "Sponsors",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_EventId",
                table: "Sponsors",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sponsors_Events_EventId",
                table: "Sponsors",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
