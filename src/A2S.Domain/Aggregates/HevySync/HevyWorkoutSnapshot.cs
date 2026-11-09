using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.HevySync;

/// <summary>Where a pulled Hevy workout stands against the plan.</summary>
public enum SnapshotStatus
{
    /// <summary>Stored, not yet run through the matcher.</summary>
    Pending,

    /// <summary>Completed a plan session; <see cref="HevyWorkoutSnapshot.SessionCompletionId"/> says which.</summary>
    Matched,

    /// <summary>No session fitted; shows in the inbox until assigned or ignored.</summary>
    Unmatched,

    /// <summary>The person dismissed it from the inbox.</summary>
    Ignored,

    /// <summary>Hevy reported it deleted. Any completion it produced stays; undoing is the person's call.</summary>
    DeletedInHevy
}

/// <summary>
/// One Hevy workout as last seen through the events endpoint, keyed by Hevy's workout id so a replayed
/// event updates the row instead of creating another. The raw JSON is kept for re-matching and the inbox.
/// </summary>
public sealed class HevyWorkoutSnapshot : AggregateRoot<HevyWorkoutId>
{
    public const int MaxTitleLength = 300;
    public const int MaxReasonLength = 500;

    private HevyWorkoutSnapshot(
        HevyWorkoutId id,
        UserId userId,
        string title,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string? routineId,
        string rawJson,
        DateOnly localDate,
        DateTimeOffset updatedAtUtc)
        : base(id)
    {
        UserId = userId;
        Title = title;
        StartUtc = startUtc;
        EndUtc = endUtc;
        RoutineId = routineId;
        RawJson = rawJson;
        LocalDate = localDate;
        Status = SnapshotStatus.Pending;
        UpdatedAtUtc = updatedAtUtc;
    }

    // EF Core constructor
    private HevyWorkoutSnapshot()
    {
        Title = string.Empty;
        RawJson = string.Empty;
    }

    public UserId UserId { get; private set; }
    public string Title { get; private set; }
    public DateTimeOffset StartUtc { get; private set; }
    public DateTimeOffset EndUtc { get; private set; }
    public string? RoutineId { get; private set; }
    public string RawJson { get; private set; }

    /// <summary>The Europe/London date the workout started on (via <see cref="ILocalClock"/>).</summary>
    public DateOnly LocalDate { get; private set; }

    public SnapshotStatus Status { get; private set; }
    public SessionCompletionId? SessionCompletionId { get; private set; }

    /// <summary>Why it is unmatched, for the inbox. Null otherwise.</summary>
    public string? Reason { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>True while the matcher should still look at it (new, or unmatched and not dismissed).</summary>
    public bool AwaitsMatching => Status is SnapshotStatus.Pending or SnapshotStatus.Unmatched;

    public static HevyWorkoutSnapshot Create(
        HevyWorkoutId id,
        UserId userId,
        string title,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string? routineId,
        string rawJson,
        DateOnly localDate,
        DateTimeOffset now)
    {
        CheckRule(!string.IsNullOrWhiteSpace(id.Value), "A snapshot needs the Hevy workout id");
        CheckRule(!string.IsNullOrWhiteSpace(rawJson), "A snapshot keeps the workout JSON");
        CheckRule(endUtc >= startUtc, "A workout cannot end before it starts");

        return new HevyWorkoutSnapshot(
            id,
            userId,
            Clip(title, MaxTitleLength) ?? string.Empty,
            startUtc.ToUniversalTime(),
            endUtc.ToUniversalTime(),
            string.IsNullOrWhiteSpace(routineId) ? null : routineId.Trim(),
            rawJson,
            localDate,
            now.ToUniversalTime());
    }

    /// <summary>
    /// A later event for the same workout: refresh what Hevy says, keep what the plan decided. A matched
    /// snapshot stays matched (never re-completes); a deleted one comes back to Pending if Hevy restores it;
    /// an ignored one stays ignored.
    /// </summary>
    public void Refresh(string title, DateTimeOffset startUtc, DateTimeOffset endUtc, string? routineId, string rawJson, DateOnly localDate, DateTimeOffset now)
    {
        CheckRule(!string.IsNullOrWhiteSpace(rawJson), "A snapshot keeps the workout JSON");
        CheckRule(endUtc >= startUtc, "A workout cannot end before it starts");

        Title = Clip(title, MaxTitleLength) ?? string.Empty;
        StartUtc = startUtc.ToUniversalTime();
        EndUtc = endUtc.ToUniversalTime();
        RoutineId = string.IsNullOrWhiteSpace(routineId) ? null : routineId.Trim();
        RawJson = rawJson;
        LocalDate = localDate;
        UpdatedAtUtc = now.ToUniversalTime();

        if (Status == SnapshotStatus.DeletedInHevy)
        {
            Status = SnapshotStatus.Pending;
            Reason = null;
        }
    }

    public void MarkMatched(SessionCompletionId completionId, DateTimeOffset now)
    {
        Status = SnapshotStatus.Matched;
        SessionCompletionId = completionId;
        Reason = null;
        UpdatedAtUtc = now.ToUniversalTime();
    }

    public void MarkUnmatched(string reason, DateTimeOffset now)
    {
        CheckRule(Status != SnapshotStatus.Matched, "A matched snapshot is not unmatched again; undo the completion instead");
        Status = SnapshotStatus.Unmatched;
        Reason = Clip(reason, MaxReasonLength);
        UpdatedAtUtc = now.ToUniversalTime();
    }

    public void Ignore(DateTimeOffset now)
    {
        CheckRule(Status != SnapshotStatus.Matched, "A matched snapshot cannot be ignored; undo the completion instead");
        Status = SnapshotStatus.Ignored;
        Reason = null;
        UpdatedAtUtc = now.ToUniversalTime();
    }

    /// <summary>Hevy deleted the workout. The completion, if any, is left alone.</summary>
    public void MarkDeletedInHevy(DateTimeOffset now)
    {
        Status = SnapshotStatus.DeletedInHevy;
        UpdatedAtUtc = now.ToUniversalTime();
    }

    private static string? Clip(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
