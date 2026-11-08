using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace A2S.Infrastructure.Tests.Security;

[Collection("Database")]
public class HevyApiKeyBackfillTests
{
    private readonly DatabaseFixture _fixture;

    public HevyApiKeyBackfillTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static ISecretProtector NewProtector() =>
        new DataProtectionSecretProtector(new EphemeralDataProtectionProvider(), NullLogger<DataProtectionSecretProtector>.Instance);

    private static Task InsertLegacyUserAsync(A2SDbContext db, string id, string? plaintextKey) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Users" ("Id", "Email", "Name", "CreatedAt", "HevyApiKey")
            VALUES ({id}, {id + "@example.com"}, {"Legacy"}, {new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)}, {plaintextKey})
            """);

    [Fact]
    public async Task Run_EncryptsLegacyPlaintextRow_AndLeavesPlaintextColumnInPlace()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "backfill");
        await InsertLegacyUserAsync(db, "user_legacy_1", "legacy-plain-key-0001");
        var protector = NewProtector();

        var encrypted = await HevyApiKeyBackfill.RunAsync(db, protector, NullLogger.Instance);

        encrypted.Should().Be(1);
        await using var fresh = MigratedDatabase.Reopen(db);
        var user = await fresh.Users.SingleAsync(u => u.Id == new UserId("user_legacy_1"));
        user.HasHevyApiKey.Should().BeTrue();
        user.HevyApiKeyProtected.Should().NotBe("legacy-plain-key-0001");
        user.RevealHevyApiKey(protector).Should().Be("legacy-plain-key-0001");

        var plaintextStillThere = await fresh.Database
            .SqlQueryRaw<string>("""SELECT "HevyApiKey" AS "Value" FROM "Users" WHERE "Id" = 'user_legacy_1'""")
            .SingleAsync();
        plaintextStillThere.Should().Be("legacy-plain-key-0001");
    }

    [Fact]
    public async Task Run_IsIdempotent_SecondRunChangesNothing()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "backfill");
        await InsertLegacyUserAsync(db, "user_legacy_2", "legacy-plain-key-0002");
        var protector = NewProtector();
        await HevyApiKeyBackfill.RunAsync(db, protector, NullLogger.Instance);
        var firstValue = (await MigratedDatabase.Reopen(db).Users.SingleAsync(u => u.Id == new UserId("user_legacy_2"))).HevyApiKeyProtected;

        var second = await HevyApiKeyBackfill.RunAsync(db, protector, NullLogger.Instance);

        second.Should().Be(0);
        var after = (await MigratedDatabase.Reopen(db).Users.SingleAsync(u => u.Id == new UserId("user_legacy_2"))).HevyApiKeyProtected;
        after.Should().Be(firstValue);
    }

    [Fact]
    public async Task Run_SkipsUsersWithoutLegacyKey()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "backfill");
        await InsertLegacyUserAsync(db, "user_nokey", null);
        await InsertLegacyUserAsync(db, "user_blank", "");

        var encrypted = await HevyApiKeyBackfill.RunAsync(db, NewProtector(), NullLogger.Instance);

        encrypted.Should().Be(0);
        (await MigratedDatabase.Reopen(db).Users.CountAsync(u => u.HevyApiKeyProtected != null)).Should().Be(0);
    }
}
