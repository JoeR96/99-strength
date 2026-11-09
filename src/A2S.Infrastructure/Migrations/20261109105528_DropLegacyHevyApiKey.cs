using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <summary>
    /// Drops the pre-v2 plaintext column <c>"Users"."HevyApiKey"</c>. The EF model stopped mapping it in
    /// <c>AddHevyApiKeyProtected</c>, so the scaffold is empty and the SQL below is written by hand.
    /// </summary>
    /// <remarks>
    /// Deploy this only after a v2 release that still contains the startup backfill
    /// (<c>HevyApiKeyBackfill</c>, removed in the same change as this migration) has started once in
    /// production. Migrations run before the app starts and a key can only be encrypted by the app,
    /// so the guard below refuses to drop the column while any key exists only in plaintext.
    /// </remarks>
    public partial class DropLegacyHevyApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Users" has no explicit schema in any earlier migration, so it is resolved through the
            // search_path. to_regclass resolves it the same way ALTER TABLE does, and the pg_attribute
            // lookup then asks whether that exact table still has the column.
            // The guard uses the same predicate the backfill used, so it passes once the backfill has run.
            // The message reports a count only, never a key.
            migrationBuilder.Sql(
                """
                DO $drop_legacy_hevy_api_key$
                DECLARE
                    unprotected_count bigint;
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_attribute
                        WHERE attrelid = to_regclass('"Users"')
                          AND attname = 'HevyApiKey'
                          AND NOT attisdropped
                    ) THEN
                        RETURN;
                    END IF;

                    SELECT count(*) INTO unprotected_count
                    FROM "Users"
                    WHERE "HevyApiKey" IS NOT NULL
                      AND "HevyApiKey" <> ''
                      AND "HevyApiKeyProtected" IS NULL;

                    IF unprotected_count > 0 THEN
                        RAISE EXCEPTION 'Refusing to drop "Users"."HevyApiKey": % user(s) still have a plaintext Hevy API key with no encrypted copy in "HevyApiKeyProtected". Nothing was dropped. Deploy the previous release (the last one without the DropLegacyHevyApiKey migration) and let the API start once so its startup backfill encrypts those keys, then deploy this release again. If such a key was removed on purpose in v2, set "HevyApiKey" to NULL for that user instead.', unprotected_count;
                    END IF;

                    ALTER TABLE "Users" DROP COLUMN "HevyApiKey";
                END
                $drop_legacy_hevy_api_key$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Brings back the column only, as nullable text. The plaintext values are gone for good and
            // cannot be restored from here: the keys now live encrypted in "HevyApiKeyProtected".
            migrationBuilder.AddColumn<string>(
                name: "HevyApiKey",
                table: "Users",
                type: "text",
                nullable: true);
        }
    }
}
