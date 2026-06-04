using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <inheritdoc />
    public partial class AddV056ScheduleItemTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Create ScheduleItemTypes table ─────────────────────────────
            migrationBuilder.CreateTable(
                name: "ScheduleItemTypes",
                columns: table => new
                {
                    ScheduleItemTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name               = table.Column<string>(type: "text", nullable: false),
                    SystemName         = table.Column<string>(type: "text", nullable: false),
                    Description        = table.Column<string>(type: "text", nullable: true),
                    SortOrder          = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleItemTypes", x => x.ScheduleItemTypeId);
                });

            // ── 2. Seed six stable ScheduleItemType rows (used in data migration below) ──
            migrationBuilder.Sql(@"
                INSERT INTO ""ScheduleItemTypes"" (""ScheduleItemTypeId"", ""Name"", ""SystemName"", ""Description"", ""SortOrder"") VALUES
                ('0000000f-000f-000f-000f-000000000001', 'Activity',     'Activity',     'Scheduled camp activity',        1),
                ('0000000f-000f-000f-000f-000000000002', 'Meal',         'Meal',         'Breakfast, lunch, or dinner',    2),
                ('0000000f-000f-000f-000f-000000000003', 'Travel',       'Travel',       'Arrival or departure',           3),
                ('0000000f-000f-000f-000f-000000000004', 'Free Time',    'Free',         'Unstructured free time',         4),
                ('0000000f-000f-000f-000f-000000000005', 'Mandatory',    'Mandatory',    'All-group required session',     5),
                ('0000000f-000f-000f-000f-000000000006', 'Presentation', 'Presentation', 'Speaker or educational session', 6)
                ON CONFLICT DO NOTHING;
            ");

            // ── 3. Create EventScheduleItemTypes join table ───────────────────
            migrationBuilder.CreateTable(
                name: "EventScheduleItemTypes",
                columns: table => new
                {
                    EventId            = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleItemTypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventScheduleItemTypes", x => new { x.EventId, x.ScheduleItemTypeId });
                    table.ForeignKey(
                        name: "FK_EventScheduleItemTypes_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventScheduleItemTypes_ScheduleItemTypes_ScheduleItemTypeId",
                        column: x => x.ScheduleItemTypeId,
                        principalTable: "ScheduleItemTypes",
                        principalColumn: "ScheduleItemTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── 4. Seed CCN 2026 event with all six types ─────────────────────
            migrationBuilder.Sql(@"
                INSERT INTO ""EventScheduleItemTypes"" (""EventId"", ""ScheduleItemTypeId"") VALUES
                ('00000009-0009-0009-0009-000000000001', '0000000f-000f-000f-000f-000000000001'),
                ('00000009-0009-0009-0009-000000000001', '0000000f-000f-000f-000f-000000000002'),
                ('00000009-0009-0009-0009-000000000001', '0000000f-000f-000f-000f-000000000003'),
                ('00000009-0009-0009-0009-000000000001', '0000000f-000f-000f-000f-000000000004'),
                ('00000009-0009-0009-0009-000000000001', '0000000f-000f-000f-000f-000000000005'),
                ('00000009-0009-0009-0009-000000000001', '0000000f-000f-000f-000f-000000000006')
                ON CONFLICT DO NOTHING;
            ");

            // ── 5. Drop FKs/indexes on ScheduleEventGroups before table rename ─
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEventGroups_Activities_ActivityId",
                table: "ScheduleEventGroups");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEventGroups_Groups_GroupId",
                table: "ScheduleEventGroups");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEventGroups_Locations_LocationId",
                table: "ScheduleEventGroups");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEventGroups_ScheduleEvents_ScheduleEventId",
                table: "ScheduleEventGroups");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEventGroups_ActivityId",
                table: "ScheduleEventGroups");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEventGroups_GroupId",
                table: "ScheduleEventGroups");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEventGroups_LocationId",
                table: "ScheduleEventGroups");

            // ── 6. Drop FKs/indexes on ScheduleEvents before table rename ─────
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEvents_Events_CampEventId",
                table: "ScheduleEvents");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEvents_Locations_LocationId",
                table: "ScheduleEvents");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEvents_Users_CreatedBy",
                table: "ScheduleEvents");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEvents_CampEventId",
                table: "ScheduleEvents");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEvents_CreatedBy",
                table: "ScheduleEvents");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEvents_LocationId",
                table: "ScheduleEvents");

            // ── 7. Rename tables ──────────────────────────────────────────────
            migrationBuilder.RenameTable(
                name: "ScheduleEvents",
                newName: "ScheduleItems");
            migrationBuilder.RenameTable(
                name: "ScheduleEventGroups",
                newName: "ScheduleItemGroups");

            // ── 8. Rename PK columns ──────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "ScheduleEventId",
                table: "ScheduleItems",
                newName: "ScheduleItemId");
            migrationBuilder.RenameColumn(
                name: "ScheduleEventId",
                table: "ScheduleItemGroups",
                newName: "ScheduleItemId");

            // ── 9. Add ScheduleItemTypeId (nullable — filled in data migration) ─
            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleItemTypeId",
                table: "ScheduleItems",
                type: "uuid",
                nullable: true);

            // ── 10. Map old EventType int values to new ScheduleItemTypeId GUIDs ─
            // Activity=0, Meal=1, Travel=2, Free=3, Mandatory=4, Presentation=5
            migrationBuilder.Sql(@"
                UPDATE ""ScheduleItems"" SET ""ScheduleItemTypeId"" = CASE ""EventType""
                    WHEN 0 THEN '0000000f-000f-000f-000f-000000000001'::uuid
                    WHEN 1 THEN '0000000f-000f-000f-000f-000000000002'::uuid
                    WHEN 2 THEN '0000000f-000f-000f-000f-000000000003'::uuid
                    WHEN 3 THEN '0000000f-000f-000f-000f-000000000004'::uuid
                    WHEN 4 THEN '0000000f-000f-000f-000f-000000000005'::uuid
                    WHEN 5 THEN '0000000f-000f-000f-000f-000000000006'::uuid
                    ELSE        '0000000f-000f-000f-000f-000000000001'::uuid
                END
            ");

            // ── 11. Make ScheduleItemTypeId NOT NULL ──────────────────────────
            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduleItemTypeId",
                table: "ScheduleItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // ── 12. Drop old EventType column ─────────────────────────────────
            migrationBuilder.DropColumn(
                name: "EventType",
                table: "ScheduleItems");

            // ── 13. Recreate ScheduleItems FKs and indexes with new names ─────
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItems_Events_CampEventId",
                table: "ScheduleItems",
                column: "CampEventId",
                principalTable: "Events",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItems_Locations_LocationId",
                table: "ScheduleItems",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItems_Users_CreatedBy",
                table: "ScheduleItems",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItems_ScheduleItemTypes_ScheduleItemTypeId",
                table: "ScheduleItems",
                column: "ScheduleItemTypeId",
                principalTable: "ScheduleItemTypes",
                principalColumn: "ScheduleItemTypeId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_CampEventId",
                table: "ScheduleItems",
                column: "CampEventId");
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_CreatedBy",
                table: "ScheduleItems",
                column: "CreatedBy");
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_LocationId",
                table: "ScheduleItems",
                column: "LocationId");
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_ScheduleItemTypeId",
                table: "ScheduleItems",
                column: "ScheduleItemTypeId");

            // ── 14. Recreate ScheduleItemGroups FKs and indexes with new names ─
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItemGroups_Activities_ActivityId",
                table: "ScheduleItemGroups",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "ActivityId");
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItemGroups_Groups_GroupId",
                table: "ScheduleItemGroups",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItemGroups_Locations_LocationId",
                table: "ScheduleItemGroups",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleItemGroups_ScheduleItems_ScheduleItemId",
                table: "ScheduleItemGroups",
                column: "ScheduleItemId",
                principalTable: "ScheduleItems",
                principalColumn: "ScheduleItemId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemGroups_ActivityId",
                table: "ScheduleItemGroups",
                column: "ActivityId");
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemGroups_GroupId",
                table: "ScheduleItemGroups",
                column: "GroupId");
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItemGroups_LocationId",
                table: "ScheduleItemGroups",
                column: "LocationId");

            // ── 15. Index for EventScheduleItemTypes ──────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_EventScheduleItemTypes_ScheduleItemTypeId",
                table: "EventScheduleItemTypes",
                column: "ScheduleItemTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop new FKs and indexes
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItemGroups_Activities_ActivityId",
                table: "ScheduleItemGroups");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItemGroups_Groups_GroupId",
                table: "ScheduleItemGroups");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItemGroups_Locations_LocationId",
                table: "ScheduleItemGroups");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItemGroups_ScheduleItems_ScheduleItemId",
                table: "ScheduleItemGroups");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleItemGroups_ActivityId",
                table: "ScheduleItemGroups");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleItemGroups_GroupId",
                table: "ScheduleItemGroups");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleItemGroups_LocationId",
                table: "ScheduleItemGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItems_Events_CampEventId",
                table: "ScheduleItems");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItems_Locations_LocationId",
                table: "ScheduleItems");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItems_Users_CreatedBy",
                table: "ScheduleItems");
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleItems_ScheduleItemTypes_ScheduleItemTypeId",
                table: "ScheduleItems");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleItems_CampEventId",
                table: "ScheduleItems");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleItems_CreatedBy",
                table: "ScheduleItems");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleItems_LocationId",
                table: "ScheduleItems");
            migrationBuilder.DropIndex(
                name: "IX_ScheduleItems_ScheduleItemTypeId",
                table: "ScheduleItems");

            // Drop ScheduleItemTypeId, restore EventType column
            migrationBuilder.DropColumn(
                name: "ScheduleItemTypeId",
                table: "ScheduleItems");
            migrationBuilder.AddColumn<int>(
                name: "EventType",
                table: "ScheduleItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Rename columns back
            migrationBuilder.RenameColumn(
                name: "ScheduleItemId",
                table: "ScheduleItems",
                newName: "ScheduleEventId");
            migrationBuilder.RenameColumn(
                name: "ScheduleItemId",
                table: "ScheduleItemGroups",
                newName: "ScheduleEventId");

            // Rename tables back
            migrationBuilder.RenameTable(
                name: "ScheduleItems",
                newName: "ScheduleEvents");
            migrationBuilder.RenameTable(
                name: "ScheduleItemGroups",
                newName: "ScheduleEventGroups");

            // Recreate old FKs and indexes
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEvents_Events_CampEventId",
                table: "ScheduleEvents",
                column: "CampEventId",
                principalTable: "Events",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEvents_Locations_LocationId",
                table: "ScheduleEvents",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEvents_Users_CreatedBy",
                table: "ScheduleEvents",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEvents_CampEventId",
                table: "ScheduleEvents",
                column: "CampEventId");
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEvents_CreatedBy",
                table: "ScheduleEvents",
                column: "CreatedBy");
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEvents_LocationId",
                table: "ScheduleEvents",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEventGroups_Activities_ActivityId",
                table: "ScheduleEventGroups",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "ActivityId");
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEventGroups_Groups_GroupId",
                table: "ScheduleEventGroups",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEventGroups_Locations_LocationId",
                table: "ScheduleEventGroups",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");
            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEventGroups_ScheduleEvents_ScheduleEventId",
                table: "ScheduleEventGroups",
                column: "ScheduleEventId",
                principalTable: "ScheduleEvents",
                principalColumn: "ScheduleEventId",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEventGroups_ActivityId",
                table: "ScheduleEventGroups",
                column: "ActivityId");
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEventGroups_GroupId",
                table: "ScheduleEventGroups",
                column: "GroupId");
            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEventGroups_LocationId",
                table: "ScheduleEventGroups",
                column: "LocationId");

            migrationBuilder.DropTable(
                name: "EventScheduleItemTypes");
            migrationBuilder.DropTable(
                name: "ScheduleItemTypes");
        }
    }
}
