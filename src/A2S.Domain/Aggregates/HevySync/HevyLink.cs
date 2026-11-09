using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.HevySync;

/// <summary>What a <see cref="HevyLink"/> points at on the Hevy side.</summary>
public enum HevyLinkKind
{
    Routine,
    Folder,
    Template,
    Workout
}

/// <summary>
/// Maps one local key (a plan session, a plan folder, a drill or cardio template) to the id Hevy gave
/// it, so every push is idempotent: find the link, update in place; no link, create and remember.
/// Unique per (user, kind, local key).
/// </summary>
public sealed class HevyLink : AggregateRoot<HevyLinkId>
{
    public const int MaxLocalKeyLength = 200;
    public const int MaxHevyIdLength = 100;

    private HevyLink(HevyLinkId id, UserId userId, HevyLinkKind kind, string localKey, string hevyId, DateTimeOffset syncedAtUtc)
        : base(id)
    {
        UserId = userId;
        Kind = kind;
        LocalKey = localKey;
        HevyId = hevyId;
        SyncedAtUtc = syncedAtUtc;
    }

    // EF Core constructor
    private HevyLink()
    {
        LocalKey = string.Empty;
        HevyId = string.Empty;
    }

    public UserId UserId { get; private set; }
    public HevyLinkKind Kind { get; private set; }
    public string LocalKey { get; private set; }
    public string HevyId { get; private set; }
    public DateTimeOffset SyncedAtUtc { get; private set; }

    public static HevyLink Create(UserId userId, HevyLinkKind kind, string localKey, string hevyId, DateTimeOffset syncedAt)
    {
        CheckRule(!string.IsNullOrWhiteSpace(localKey), "A Hevy link needs a local key");
        CheckRule(localKey.Trim().Length <= MaxLocalKeyLength, $"A local key is at most {MaxLocalKeyLength} characters");
        CheckRule(!string.IsNullOrWhiteSpace(hevyId), "A Hevy link needs the Hevy id");
        CheckRule(hevyId.Trim().Length <= MaxHevyIdLength, $"A Hevy id is at most {MaxHevyIdLength} characters");

        return new HevyLink(new HevyLinkId(Guid.NewGuid()), userId, kind, localKey.Trim(), hevyId.Trim(), syncedAt.ToUniversalTime());
    }

    /// <summary>Points the link at a different Hevy id (the routine was recreated) and stamps the sync time.</summary>
    public void Relink(string hevyId, DateTimeOffset syncedAt)
    {
        CheckRule(!string.IsNullOrWhiteSpace(hevyId), "A Hevy link needs the Hevy id");
        CheckRule(hevyId.Trim().Length <= MaxHevyIdLength, $"A Hevy id is at most {MaxHevyIdLength} characters");
        HevyId = hevyId.Trim();
        SyncedAtUtc = syncedAt.ToUniversalTime();
    }

    /// <summary>Records that the linked item was pushed again (an update), keeping the same Hevy id.</summary>
    public void Touch(DateTimeOffset syncedAt) => SyncedAtUtc = syncedAt.ToUniversalTime();
}
