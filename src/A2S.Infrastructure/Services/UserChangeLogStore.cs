using System.Data;
using System.Data.Common;
using A2S.Application.Sync;
using A2S.Domain.Aggregates.Sync;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;

namespace A2S.Infrastructure.Services;

/// <summary>
/// Persistence model (not a domain concept) for a user's <see cref="ChangeStamp"/>: one row per user.
/// </summary>
public sealed class UserChangeLogRecord
{
    public string UserId { get; set; } = string.Empty;
    public long Version { get; set; }
    public long Habits { get; set; }
    public long Plan { get; set; }
    public long Workout { get; set; }
    public long Cardio { get; set; }
    public long Mobility { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ChangeStamp ToStamp() => new(Version, Habits, Plan, Workout, Cardio, Mobility);
}

public sealed class UserChangeLogConfiguration : IEntityTypeConfiguration<UserChangeLogRecord>
{
    public void Configure(EntityTypeBuilder<UserChangeLogRecord> builder)
    {
        builder.ToTable("UserChangeLogs");
        builder.HasKey(r => r.UserId);
        builder.Property(r => r.UserId).HasMaxLength(256);
        builder.Property(r => r.UpdatedAtUtc).IsRequired();
    }
}

/// <summary>
/// EF-backed <see cref="IUserChangeLog"/>. The bump is one <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>, so
/// two writes at the same moment get versions n+1 and n+2 rather than both n+1. It follows the rule of
/// <see cref="ChangeStamp.Bump"/> (an area takes the new version when it is in the bump).
/// </summary>
public sealed class UserChangeLogStore : IUserChangeLog
{
    private readonly A2SDbContext _db;
    private readonly TimeProvider _time;

    public UserChangeLogStore(A2SDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<ChangeStamp> GetAsync(string userId, CancellationToken ct)
    {
        var row = await _db.Set<UserChangeLogRecord>().AsNoTracking().SingleOrDefaultAsync(r => r.UserId == userId, ct);
        return row?.ToStamp() ?? ChangeStamp.Empty;
    }

    public async Task<ChangeStamp> BumpAsync(string userId, SyncArea areas, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO "UserChangeLogs" AS l ("UserId", "Version", "Habits", "Plan", "Workout", "Cardio", "Mobility", "UpdatedAtUtc")
            VALUES (@user, 1,
                CASE WHEN (@areas & 1) <> 0 THEN 1 ELSE 0 END,
                CASE WHEN (@areas & 2) <> 0 THEN 1 ELSE 0 END,
                CASE WHEN (@areas & 4) <> 0 THEN 1 ELSE 0 END,
                CASE WHEN (@areas & 8) <> 0 THEN 1 ELSE 0 END,
                CASE WHEN (@areas & 16) <> 0 THEN 1 ELSE 0 END,
                @now)
            ON CONFLICT ("UserId") DO UPDATE SET
                "Version" = l."Version" + 1,
                "Habits" = CASE WHEN (@areas & 1) <> 0 THEN l."Version" + 1 ELSE l."Habits" END,
                "Plan" = CASE WHEN (@areas & 2) <> 0 THEN l."Version" + 1 ELSE l."Plan" END,
                "Workout" = CASE WHEN (@areas & 4) <> 0 THEN l."Version" + 1 ELSE l."Workout" END,
                "Cardio" = CASE WHEN (@areas & 8) <> 0 THEN l."Version" + 1 ELSE l."Cardio" END,
                "Mobility" = CASE WHEN (@areas & 16) <> 0 THEN l."Version" + 1 ELSE l."Mobility" END,
                "UpdatedAtUtc" = @now
            RETURNING "Version", "Habits", "Plan", "Workout", "Cardio", "Mobility";
            """;

        var connection = _db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
            Add(command, "user", userId);
            Add(command, "areas", (int)areas);
            Add(command, "now", _time.GetUtcNow());

            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            return new ChangeStamp(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3),
                reader.GetInt64(4), reader.GetInt64(5));
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
