using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.HevySync;

public enum HevyPostStatus
{
    Pending,
    Posted,
    Failed,

    /// <summary>Not sent: the person has no Hevy key. A retry after connecting Hevy sends it.</summary>
    Skipped,

    /// <summary>
    /// Already in Hevy, but the payload changed since: the workout is replaced in place (<c>PUT</c> to
    /// <see cref="HevyWorkoutPost.HevyWorkoutId"/>), never posted again.
    /// </summary>
    UpdatePending
}

/// <summary>
/// A finished gym-mode session waiting to be (or already) written to Hevy as a workout. Keyed by the
/// client session id the phone generated when the session started, which makes finishing idempotent:
/// a replayed finish finds this record and returns its stored result instead of completing again.
/// Holds what the post needs (title, real start and end, the logged payload) and the retry state.
/// Unique per (user, client session id).
/// </summary>
public sealed class HevyWorkoutPost : AggregateRoot<HevyWorkoutPostId>
{
    public const int MaxTitleLength = 200;
    public const int MaxErrorLength = 500;

    /// <summary>After this many failed attempts the post stops retrying on its own (a person can retry).</summary>
    public const int MaxAttempts = 8;

    private static readonly TimeSpan FirstBackoff = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(6);

    private HevyWorkoutPost(
        HevyWorkoutPostId id,
        UserId userId,
        Guid clientSessionId,
        TrainingPlanId? planId,
        SessionCompletionId? completionId,
        DateOnly localDate,
        string title,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        string payloadJson,
        string resultJson,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        UserId = userId;
        ClientSessionId = clientSessionId;
        PlanId = planId;
        CompletionId = completionId;
        LocalDate = localDate;
        Title = title;
        StartedAtUtc = startedAtUtc;
        EndedAtUtc = endedAtUtc;
        PayloadJson = payloadJson;
        ResultJson = resultJson;
        CreatedAtUtc = createdAtUtc;
        Status = HevyPostStatus.Pending;
        NextAttemptAtUtc = createdAtUtc;
    }

    // EF Core constructor
    private HevyWorkoutPost()
    {
        Title = string.Empty;
        PayloadJson = string.Empty;
        ResultJson = string.Empty;
    }

    public UserId UserId { get; private set; }
    public Guid ClientSessionId { get; private set; }
    public TrainingPlanId? PlanId { get; private set; }
    public SessionCompletionId? CompletionId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public string Title { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset EndedAtUtc { get; private set; }

    /// <summary>What was logged (warm-up ticks, sets, finisher), as JSON; turned into a Hevy workout at post time.</summary>
    public string PayloadJson { get; private set; }

    /// <summary>The finish result returned to the phone, as JSON, so a replay gets the same answer.</summary>
    public string ResultJson { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public HevyPostStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public string? HevyWorkoutId { get; private set; }
    public DateTimeOffset? PostedAtUtc { get; private set; }

    public static HevyWorkoutPost Create(
        UserId userId,
        Guid clientSessionId,
        TrainingPlanId? planId,
        SessionCompletionId? completionId,
        DateOnly localDate,
        string title,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string payloadJson,
        string resultJson,
        DateTimeOffset now)
    {
        CheckRule(clientSessionId != Guid.Empty, "A gym session needs a client session id");
        CheckRule(!string.IsNullOrWhiteSpace(title), "A Hevy workout needs a title");
        CheckRule(endedAt >= startedAt, "A session cannot end before it starts");
        CheckRule(!string.IsNullOrWhiteSpace(payloadJson), "A Hevy post needs a payload");

        var trimmed = title.Trim();
        return new HevyWorkoutPost(
            new HevyWorkoutPostId(Guid.NewGuid()),
            userId,
            clientSessionId,
            planId,
            completionId,
            localDate,
            trimmed.Length > MaxTitleLength ? trimmed[..MaxTitleLength] : trimmed,
            startedAt.ToUniversalTime(),
            endedAt.ToUniversalTime(),
            payloadJson,
            string.IsNullOrWhiteSpace(resultJson) ? "{}" : resultJson,
            now.ToUniversalTime());
    }

    /// <summary>Whether the background retry should try this post now.</summary>
    public bool IsDue(DateTimeOffset now) =>
        (Status is HevyPostStatus.Pending or HevyPostStatus.UpdatePending) && NextAttemptAtUtc <= now.ToUniversalTime();

    /// <summary>Whether an earlier attempt may have reached Hevy, so the poster must look before posting again.</summary>
    public bool MayAlreadyExistInHevy => Attempts > 0;

    /// <summary>Whether a Hevy workout is this session (same title, start within a minute).</summary>
    public bool Describes(string? title, DateTimeOffset startTime) =>
        string.Equals(Title, title?.Trim(), StringComparison.Ordinal)
        && (StartedAtUtc - startTime.ToUniversalTime()).Duration() <= TimeSpan.FromMinutes(1);

    /// <summary>
    /// Records the Hevy workout this session became (posted now, or found already there). Idempotent for
    /// the same id; a different id once posted is refused so one session never maps to two workouts.
    /// </summary>
    public void MarkPosted(string hevyWorkoutId, DateTimeOffset now)
    {
        CheckRule(!string.IsNullOrWhiteSpace(hevyWorkoutId), "A Hevy workout id is required");
        var id = hevyWorkoutId.Trim();
        if (Status == HevyPostStatus.Posted)
        {
            CheckRule(string.Equals(HevyWorkoutId, id, StringComparison.Ordinal), "This session is already in Hevy as another workout");
            return;
        }

        HevyWorkoutId = id;
        Status = HevyPostStatus.Posted;
        PostedAtUtc = now.ToUniversalTime();
        LastError = null;
    }

    /// <summary>
    /// Records a failed attempt and schedules the next with exponential backoff (2, 4, 8 … minutes, at
    /// most 6 hours). A permanent failure (bad key, rejected payload) or too many attempts stops retrying.
    /// </summary>
    public void RecordFailure(string error, bool permanent, DateTimeOffset now)
    {
        if (Status == HevyPostStatus.Posted)
        {
            return;
        }

        Attempts++;
        var message = string.IsNullOrWhiteSpace(error) ? "Hevy post failed" : error.Trim();
        LastError = message.Length > MaxErrorLength ? message[..MaxErrorLength] : message;

        if (permanent || Attempts >= MaxAttempts)
        {
            // An update that gives up leaves the workout in Hevy as it was: back to Posted, never to a state
            // a retry would post again (a second workout).
            Status = Status == HevyPostStatus.UpdatePending ? HevyPostStatus.Posted : HevyPostStatus.Failed;
            return;
        }

        var factor = 1L << Math.Min(Attempts - 1, 20);
        var backoff = TimeSpan.FromTicks(Math.Min(FirstBackoff.Ticks * factor, MaxBackoff.Ticks));
        NextAttemptAtUtc = now.ToUniversalTime() + backoff;
    }

    /// <summary>Stores the finish result once it is known (the in-app completion is saved first).</summary>
    public void RecordResult(string resultJson)
    {
        CheckRule(!string.IsNullOrWhiteSpace(resultJson), "A finish result is required");
        ResultJson = resultJson;
    }

    /// <summary>Nothing will be sent (no Hevy key). Kept so a replayed finish is still recognised.</summary>
    public void MarkSkipped(string reason)
    {
        if (Status is HevyPostStatus.Posted or HevyPostStatus.UpdatePending)
        {
            return;
        }

        Status = HevyPostStatus.Skipped;
        LastError = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>A person asked to try again: back to pending and due now.</summary>
    public void RequeueNow(DateTimeOffset now)
    {
        if (Status == HevyPostStatus.Posted)
        {
            return;
        }

        if (Status != HevyPostStatus.UpdatePending)
        {
            Status = HevyPostStatus.Pending;
        }

        NextAttemptAtUtc = now.ToUniversalTime();
    }

    /// <summary>
    /// The logged session changed after it reached Hevy: stores the new payload and queues an in-place update of
    /// <see cref="HevyWorkoutId"/>, due now, with a fresh retry count. Only a posted session can be updated.
    /// </summary>
    public void RequestUpdate(string payloadJson, DateTimeOffset now)
    {
        CheckRule((Status is HevyPostStatus.Posted or HevyPostStatus.UpdatePending) && HevyWorkoutId is not null,
            "Only a session already in Hevy can be updated there");
        CheckRule(!string.IsNullOrWhiteSpace(payloadJson), "A Hevy post needs a payload");

        PayloadJson = payloadJson;
        Status = HevyPostStatus.UpdatePending;
        Attempts = 0;
        LastError = null;
        NextAttemptAtUtc = now.ToUniversalTime();
    }

    /// <summary>Hevy accepted the update: posted again, as the same workout.</summary>
    public void MarkUpdated()
    {
        CheckRule(Status == HevyPostStatus.UpdatePending, "No Hevy update was pending");
        Status = HevyPostStatus.Posted;
        LastError = null;
    }
}
