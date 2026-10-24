using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampClotNot.Migrations
{
    /// <summary>
    /// Merges duplicate people that share an email. MergeStaffDirectoryIntoTeam (#311) turned Hub
    /// directory cards that weren't linked to an account into new listed-only people, without
    /// checking whether an account already had that email. So someone with both a sign-in account
    /// and a directory card got two records, and editing either failed with "X already has that
    /// email". Each copy that never set a password is folded into the record that can sign in
    /// (its photo/phone fill gaps, its event-team rows, sign-ups and check-ins move over) and
    /// deleted. Two real accounts sharing an email are left alone.
    /// </summary>
    public partial class MergeDuplicatePeopleByEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE person_merge AS
                WITH grp AS (
                    SELECT u."UserId", lower(btrim(u."Email")) AS email_key,
                           ROW_NUMBER() OVER (
                               PARTITION BY lower(btrim(u."Email"))
                               ORDER BY (COALESCE(u."PasswordHash", '') <> '') DESC, u."CanSignIn" DESC, u."IsActive" DESC,
                                        (SELECT count(*) FROM "EventStaff" s WHERE s."UserId" = u."UserId") DESC, u."UserId") AS rn
                    FROM "Users" u
                    WHERE btrim(COALESCE(u."Email", '')) <> ''
                )
                SELECT d."UserId" AS dup, k."UserId" AS keeper
                FROM grp d
                JOIN grp k ON k.email_key = d.email_key AND k.rn = 1
                JOIN "Users" du ON du."UserId" = d."UserId"
                WHERE d.rn > 1 AND COALESCE(du."PasswordHash", '') = '';

                -- Fill the keeper's gaps from its copies (a copy with a photo first).
                UPDATE "Users" k SET
                    "Phone"               = COALESCE(NULLIF(k."Phone", ''), d."Phone"),
                    "PhotoData"           = CASE WHEN k."PhotoContentType" IS NULL THEN d."PhotoData" ELSE k."PhotoData" END,
                    "PhotoObjectPosition" = CASE WHEN k."PhotoContentType" IS NULL THEN d."PhotoObjectPosition" ELSE k."PhotoObjectPosition" END,
                    "AvatarEmoji"         = CASE WHEN COALESCE(k."AvatarEmoji", '') IN ('', '👤') AND COALESCE(d."AvatarEmoji", '') <> ''
                                                 THEN d."AvatarEmoji" ELSE k."AvatarEmoji" END,
                    "PhotoContentType"    = COALESCE(k."PhotoContentType", d."PhotoContentType")
                FROM (
                    SELECT DISTINCT ON (m.keeper) m.keeper, u."Phone", u."PhotoData", u."PhotoContentType", u."PhotoObjectPosition", u."AvatarEmoji"
                    FROM person_merge m JOIN "Users" u ON u."UserId" = m.dup
                    ORDER BY m.keeper, (u."PhotoContentType" IS NULL), (u."Phone" IS NULL)
                ) d
                WHERE k."UserId" = d.keeper;

                -- Event team: where the keeper and a copy are on the same event, combine the rows
                -- (shown in the directory if either was, keep a title), then move the rest over.
                DELETE FROM "EventStaff" d USING person_merge m, "EventStaff" d2, person_merge m2
                WHERE d."UserId" = m.dup AND d2."UserId" = m2.dup AND m2.keeper = m.keeper
                  AND d2."EventId" = d."EventId" AND d2."EventStaffId" < d."EventStaffId";
                UPDATE "EventStaff" k SET
                    "ShowInDirectory" = k."ShowInDirectory" OR d."ShowInDirectory",
                    "Title"           = COALESCE(NULLIF(k."Title", ''), d."Title"),
                    "GroupId"         = COALESCE(k."GroupId", d."GroupId"),
                    "SortOrder"       = CASE WHEN NOT k."ShowInDirectory" AND d."ShowInDirectory" THEN d."SortOrder" ELSE k."SortOrder" END
                FROM "EventStaff" d JOIN person_merge m ON m.dup = d."UserId"
                WHERE k."UserId" = m.keeper AND k."EventId" = d."EventId";
                DELETE FROM "EventStaff" d USING person_merge m
                WHERE d."UserId" = m.dup
                  AND EXISTS (SELECT 1 FROM "EventStaff" k WHERE k."UserId" = m.keeper AND k."EventId" = d."EventId");
                UPDATE "EventStaff" d SET "UserId" = m.keeper FROM person_merge m WHERE d."UserId" = m.dup;

                -- Breakout sign-ups (one per person per slot).
                DELETE FROM "ScheduleItemRegistrations" d USING person_merge m, "ScheduleItemRegistrations" d2, person_merge m2
                WHERE d."UserId" = m.dup AND d2."UserId" = m2.dup AND m2.keeper = m.keeper
                  AND d2."SlotScheduleItemId" = d."SlotScheduleItemId" AND d2."ScheduleItemRegistrationId" < d."ScheduleItemRegistrationId";
                DELETE FROM "ScheduleItemRegistrations" d USING person_merge m
                WHERE d."UserId" = m.dup
                  AND EXISTS (SELECT 1 FROM "ScheduleItemRegistrations" k WHERE k."UserId" = m.keeper AND k."SlotScheduleItemId" = d."SlotScheduleItemId");
                UPDATE "ScheduleItemRegistrations" d SET "UserId" = m.keeper FROM person_merge m WHERE d."UserId" = m.dup;
                UPDATE "ScheduleItemRegistrations" d SET "RegisteredByUserId" = m.keeper FROM person_merge m WHERE d."RegisteredByUserId" = m.dup;

                -- Check-ins (one per person per item).
                DELETE FROM "ScheduleItemAttendances" d USING person_merge m, "ScheduleItemAttendances" d2, person_merge m2
                WHERE d."UserId" = m.dup AND d2."UserId" = m2.dup AND m2.keeper = m.keeper
                  AND d2."ScheduleItemId" = d."ScheduleItemId" AND d2."ScheduleItemAttendanceId" < d."ScheduleItemAttendanceId";
                DELETE FROM "ScheduleItemAttendances" d USING person_merge m
                WHERE d."UserId" = m.dup
                  AND EXISTS (SELECT 1 FROM "ScheduleItemAttendances" k WHERE k."UserId" = m.keeper AND k."ScheduleItemId" = d."ScheduleItemId");
                UPDATE "ScheduleItemAttendances" d SET "UserId" = m.keeper FROM person_merge m WHERE d."UserId" = m.dup;
                UPDATE "ScheduleItemAttendances" d SET "CheckedInByUserId" = m.keeper FROM person_merge m WHERE d."CheckedInByUserId" = m.dup;

                -- Anything else that points at a copy (a listed-only copy can't normally have these).
                UPDATE "Announcements" t SET "AuthorId" = m.keeper FROM person_merge m WHERE t."AuthorId" = m.dup;
                UPDATE "CampDocuments" t SET "UploadedByUserId" = m.keeper FROM person_merge m WHERE t."UploadedByUserId" = m.dup;
                UPDATE "InfoPages" t SET "UpdatedByUserId" = m.keeper FROM person_merge m WHERE t."UpdatedByUserId" = m.dup;
                UPDATE "ScheduleItems" t SET "CreatedBy" = m.keeper FROM person_merge m WHERE t."CreatedBy" = m.dup;
                UPDATE "PasswordResetTokens" t SET "CreatedByUserId" = m.keeper FROM person_merge m WHERE t."CreatedByUserId" = m.dup;
                DELETE FROM "PasswordResetTokens" t USING person_merge m WHERE t."UserId" = m.dup;
                DELETE FROM "UserAuthorityLinks" t USING person_merge m WHERE t."UserId" = m.dup;

                DELETE FROM "Users" u USING person_merge m WHERE u."UserId" = m.dup;
                DROP TABLE person_merge;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Merged duplicates can't be split back apart; this migration only removes copies.
        }
    }
}
