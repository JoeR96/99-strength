using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.Security;

/// <summary>
/// One-off, idempotent startup step: encrypts any legacy plaintext Hevy API keys that were stored
/// in <c>Users.HevyApiKey</c> before v2 into <c>Users.HevyApiKeyProtected</c>.
/// The plaintext value is erased in the same statement that stores its encrypted copy, so no
/// plaintext key outlives the first v2 boot and a key the user later removes cannot be re-encrypted
/// from a stale plaintext row on the next boot. The (now empty) column itself is dropped by the
/// later <c>DropLegacyHevyApiKey</c> migration. Safe to run on every boot. Rolling back to v1 after
/// this has run means re-entering the Hevy key, since v1 reads the plaintext column.
/// Logs counts only; never the key.
/// </summary>
public static class HevyApiKeyBackfill
{
    private sealed record LegacyRow(string Id, string HevyApiKey);

    /// <summary>Returns the number of rows encrypted in this run.</summary>
    public static async Task<int> RunAsync(
        A2SDbContext dbContext,
        ISecretProtector protector,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!await LegacyColumnExistsAsync(dbContext, cancellationToken))
        {
            return 0;
        }

        var rows = await dbContext.Database
            .SqlQueryRaw<LegacyRow>(
                """
                SELECT "Id", "HevyApiKey"
                FROM "Users"
                WHERE "HevyApiKeyProtected" IS NULL AND "HevyApiKey" IS NOT NULL AND "HevyApiKey" <> ''
                """)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            var protectedValue = protector.Protect(row.HevyApiKey.Trim());
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "Users" SET "HevyApiKeyProtected" = {protectedValue}, "HevyApiKey" = NULL WHERE "Id" = {row.Id}""",
                cancellationToken);
        }

        if (rows.Count > 0)
        {
            logger.LogInformation("Encrypted {Count} legacy Hevy API key(s) at rest.", rows.Count);
        }

        return rows.Count;
    }

    private static async Task<bool> LegacyColumnExistsAsync(A2SDbContext dbContext, CancellationToken ct)
    {
        var count = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM information_schema.columns
                WHERE table_name = 'Users' AND column_name = 'HevyApiKey'
                """)
            .SingleAsync(ct);
        return count > 0;
    }
}
