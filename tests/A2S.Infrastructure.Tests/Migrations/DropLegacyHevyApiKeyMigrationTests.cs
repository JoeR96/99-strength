using A2S.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace A2S.Infrastructure.Tests.Migrations;

/// <summary>
/// The <c>DropLegacyHevyApiKey</c> migration against a real Postgres: it drops the pre-v2 plaintext
/// column only when no key would be lost, and otherwise fails loudly and changes nothing.
/// </summary>
[Collection("Database")]
public class DropLegacyHevyApiKeyMigrationTests
{
    /// <summary>The last migration of the pre-v2 schema: plaintext column only.</summary>
    private const string LastPreV2Migration = "SeedCableCorePallofPress";

    /// <summary>The migration directly before the drop: both columns exist.</summary>
    private const string PreviousMigration = "AddApiTokens";

    private const string DropMigration = "DropLegacyHevyApiKey";

    private const string PlaintextKey = "legacy-plain-key-0001";

    private readonly DatabaseFixture _fixture;

    public DropLegacyHevyApiKeyMigrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static Task InsertUserAsync(A2SDbContext db, string id, string? plaintextKey, string? protectedKey) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Users" ("Id", "Email", "Name", "CreatedAt", "HevyApiKey", "HevyApiKeyProtected")
            VALUES ({id}, {id + "@example.com"}, {"Legacy"}, {new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)}, {plaintextKey}, {protectedKey})
            """);

    private static Task InsertPreV2UserAsync(A2SDbContext db, string id, string? plaintextKey) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Users" ("Id", "Email", "Name", "CreatedAt", "HevyApiKey")
            VALUES ({id}, {id + "@example.com"}, {"Legacy"}, {new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)}, {plaintextKey})
            """);

    /// <summary>Type and nullability of a Users column, or an empty list when the column does not exist.</summary>
    private static async Task<List<string>> ColumnDefinitionAsync(A2SDbContext db, string column) =>
        await db.Database
            .SqlQuery<string>(
                $"""
                SELECT data_type || ' ' || is_nullable AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = {column}
                """)
            .ToListAsync();

    private static async Task<bool> ColumnExistsAsync(A2SDbContext db, string column) =>
        (await ColumnDefinitionAsync(db, column)).Count == 1;

    private static Task<string?> ScalarAsync(A2SDbContext db, FormattableString sql) =>
        db.Database.SqlQuery<string?>(sql).SingleAsync();

    private static async Task<bool> DropMigrationAppliedAsync(A2SDbContext db) =>
        (await db.Database.GetAppliedMigrationsAsync()).Any(id => id.EndsWith("_" + DropMigration, StringComparison.Ordinal));

    [Fact]
    public async Task Up_WhenAKeyExistsOnlyInPlaintext_FailsWithAnActionableMessage_AndKeepsTheColumnAndValue()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "dropkey_guard", PreviousMigration);
        await InsertUserAsync(db, "user_unprotected", PlaintextKey, protectedKey: null);
        await InsertUserAsync(db, "user_protected", "legacy-plain-key-0002", protectedKey: "CfDJ8-already-encrypted");

        var act = () => db.Database.MigrateAsync();

        var exception = (await act.Should().ThrowAsync<PostgresException>()).Which;
        exception.SqlState.Should().Be(PostgresErrorCodes.RaiseException);
        exception.MessageText.Should().Contain("Refusing to drop \"Users\".\"HevyApiKey\"");
        exception.MessageText.Should().Contain("1 user(s) still have a plaintext Hevy API key");
        exception.MessageText.Should().Contain("Nothing was dropped");
        exception.MessageText.Should().Contain("Deploy the previous release");
        exception.MessageText.Should().Contain("startup backfill");
        exception.Message.Should().NotContain(PlaintextKey, "the key itself must never reach a log");

        await using var fresh = MigratedDatabase.Reopen(db);
        (await ColumnExistsAsync(fresh, "HevyApiKey")).Should().BeTrue();
        (await ScalarAsync(fresh, $"""SELECT "HevyApiKey" AS "Value" FROM "Users" WHERE "Id" = 'user_unprotected'"""))
            .Should().Be(PlaintextKey);
        (await ScalarAsync(fresh, $"""SELECT "HevyApiKey" AS "Value" FROM "Users" WHERE "Id" = 'user_protected'"""))
            .Should().Be("legacy-plain-key-0002");
        (await DropMigrationAppliedAsync(fresh)).Should().BeFalse();
    }

    /// <summary>
    /// The case the guard exists for: this migration shipped in the same release as the first v2
    /// deploy, so the bundle walks the whole chain over a database no v2 app has ever started on.
    /// </summary>
    [Fact]
    public async Task Up_OnAPreV2DatabaseThatNeverRanTheBackfill_Fails_AndKeepsTheColumnAndValue()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "dropkey_early", LastPreV2Migration);
        await InsertPreV2UserAsync(db, "user_prev2", PlaintextKey);

        var act = () => db.Database.MigrateAsync();

        var exception = (await act.Should().ThrowAsync<PostgresException>()).Which;
        exception.MessageText.Should().Contain("Refusing to drop \"Users\".\"HevyApiKey\"");

        await using var fresh = MigratedDatabase.Reopen(db);
        (await ColumnExistsAsync(fresh, "HevyApiKey")).Should().BeTrue();
        (await ScalarAsync(fresh, $"""SELECT "HevyApiKey" AS "Value" FROM "Users" WHERE "Id" = 'user_prev2'"""))
            .Should().Be(PlaintextKey);
        (await DropMigrationAppliedAsync(fresh)).Should().BeFalse();

        // EF applies the pending chain in one transaction, so the earlier v2 migrations roll back too.
        // The database is left as the pre-v2 release had it, ready for the previous v2 release to
        // migrate and backfill.
        (await fresh.Database.GetAppliedMigrationsAsync()).Last().Should().EndWith("_" + LastPreV2Migration);
        (await ColumnExistsAsync(fresh, "HevyApiKeyProtected")).Should().BeFalse();
    }

    [Fact]
    public async Task Up_WhenEveryPlaintextKeyHasAProtectedValue_DropsTheColumn_AndKeepsTheProtectedValues()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "dropkey_ok", PreviousMigration);
        await InsertUserAsync(db, "user_backfilled", PlaintextKey, protectedKey: "CfDJ8-encrypted-1");
        await InsertUserAsync(db, "user_v2_only", plaintextKey: null, protectedKey: "CfDJ8-encrypted-2");
        await InsertUserAsync(db, "user_no_key", plaintextKey: null, protectedKey: null);
        await InsertUserAsync(db, "user_blank_key", plaintextKey: "", protectedKey: null);

        await db.Database.MigrateAsync();

        await using var fresh = MigratedDatabase.Reopen(db);
        (await ColumnExistsAsync(fresh, "HevyApiKey")).Should().BeFalse();
        (await DropMigrationAppliedAsync(fresh)).Should().BeTrue();
        (await ScalarAsync(fresh, $"""SELECT "HevyApiKeyProtected" AS "Value" FROM "Users" WHERE "Id" = 'user_backfilled'"""))
            .Should().Be("CfDJ8-encrypted-1");
        (await ScalarAsync(fresh, $"""SELECT "HevyApiKeyProtected" AS "Value" FROM "Users" WHERE "Id" = 'user_v2_only'"""))
            .Should().Be("CfDJ8-encrypted-2");
        (await fresh.Users.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task Up_WhenTheColumnHoldsNoKeysAtAll_DropsTheColumn()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "dropkey_empty", PreviousMigration);
        await InsertUserAsync(db, "user_no_key", plaintextKey: null, protectedKey: null);
        await InsertUserAsync(db, "user_blank_key", plaintextKey: "", protectedKey: null);

        await db.Database.MigrateAsync();

        await using var fresh = MigratedDatabase.Reopen(db);
        (await ColumnExistsAsync(fresh, "HevyApiKey")).Should().BeFalse();
        (await DropMigrationAppliedAsync(fresh)).Should().BeTrue();
    }

    [Fact]
    public async Task Up_WhenTheColumnIsAlreadyAbsent_Succeeds()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "dropkey_absent", PreviousMigration);
        await InsertUserAsync(db, "user_v2_only", plaintextKey: null, protectedKey: "CfDJ8-encrypted-2");
        await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Users" DROP COLUMN "HevyApiKey";""");

        await db.Database.MigrateAsync();

        await using var fresh = MigratedDatabase.Reopen(db);
        (await ColumnExistsAsync(fresh, "HevyApiKey")).Should().BeFalse();
        (await DropMigrationAppliedAsync(fresh)).Should().BeTrue();
        (await ScalarAsync(fresh, $"""SELECT "HevyApiKeyProtected" AS "Value" FROM "Users" WHERE "Id" = 'user_v2_only'"""))
            .Should().Be("CfDJ8-encrypted-2");
    }

    [Fact]
    public async Task Down_ReAddsTheColumnAsNullableTextWithoutValues_AndUpDropsItAgain()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "dropkey_roundtrip", PreviousMigration);
        await InsertUserAsync(db, "user_backfilled", PlaintextKey, protectedKey: "CfDJ8-encrypted-1");
        await db.Database.MigrateAsync();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(PreviousMigration);

        await using (var afterDown = MigratedDatabase.Reopen(db))
        {
            (await ColumnDefinitionAsync(afterDown, "HevyApiKey")).Should().Equal("text YES");
            (await DropMigrationAppliedAsync(afterDown)).Should().BeFalse();
            (await ScalarAsync(afterDown, $"""SELECT "HevyApiKey" AS "Value" FROM "Users" WHERE "Id" = 'user_backfilled'"""))
                .Should().BeNull("Down cannot bring the plaintext back");
            (await ScalarAsync(afterDown, $"""SELECT "HevyApiKeyProtected" AS "Value" FROM "Users" WHERE "Id" = 'user_backfilled'"""))
                .Should().Be("CfDJ8-encrypted-1");
        }

        await migrator.MigrateAsync();

        await using var afterUp = MigratedDatabase.Reopen(db);
        (await ColumnExistsAsync(afterUp, "HevyApiKey")).Should().BeFalse();
        (await DropMigrationAppliedAsync(afterUp)).Should().BeTrue();
        (await ScalarAsync(afterUp, $"""SELECT "HevyApiKeyProtected" AS "Value" FROM "Users" WHERE "Id" = 'user_backfilled'"""))
            .Should().Be("CfDJ8-encrypted-1");
    }
}
