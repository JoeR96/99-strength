using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Mobility;

/// <summary>
/// An ordered list of mobility drills with a target duration. Drill names are unique within a
/// routine so completions can tick drills by name. A routine has a kind (warm-up, corrective, stretch,
/// yoga flow, follow-along); a follow-along is just a link and a duration. Drills with levels progress
/// through <see cref="DrillProgressionRule"/>, with every change audited and the last one undoable.
/// </summary>
public sealed class MobilityRoutine : AggregateRoot<MobilityRoutineId>
{
    private readonly List<MobilityDrill> _drills = new();
    private List<DrillProgressionEntry> _progressionLog = new();

    private MobilityRoutine(MobilityRoutineId id, UserId userId, string name, int targetMinutes, List<MobilityDrill> drills,
        MobilityRoutineKind kind, string? followAlongUrl)
        : base(id)
    {
        UserId = userId;
        Name = name;
        TargetMinutes = targetMinutes;
        _drills = drills;
        Kind = kind;
        FollowAlongUrl = followAlongUrl;
    }

    // EF Core constructor
    private MobilityRoutine()
    {
        Name = string.Empty;
    }

    public UserId UserId { get; private set; }
    public string Name { get; private set; }
    public int TargetMinutes { get; private set; }
    public IReadOnlyList<MobilityDrill> Drills => _drills.AsReadOnly();
    public MobilityRoutineKind Kind { get; private set; }

    /// <summary>For a follow-along routine: the video or app session to open.</summary>
    public string? FollowAlongUrl { get; private set; }

    /// <summary>Every progression change with its reason, oldest first.</summary>
    public IReadOnlyList<DrillProgressionEntry> ProgressionLog => _progressionLog.AsReadOnly();

    /// <summary>Yoga flows and follow-along videos are logged in Hevy as Yoga; the rest as Stretching.</summary>
    public bool PostsToHevyAsYoga => Kind is MobilityRoutineKind.YogaFlow or MobilityRoutineKind.FollowAlong;

    public static MobilityRoutine Create(
        UserId userId,
        string name,
        int targetMinutes,
        IEnumerable<MobilityDrill> drills,
        MobilityRoutineKind kind = MobilityRoutineKind.Stretch,
        string? followAlongUrl = null)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Routine name is required");
        CheckRule(targetMinutes > 0, "Target minutes must be positive");
        var url = ValidatedUrl(kind, followAlongUrl);
        var list = ValidatedDrills(drills, kind);

        return new MobilityRoutine(new MobilityRoutineId(Guid.NewGuid()), userId, name.Trim(), targetMinutes, list, kind, url);
    }

    /// <summary>
    /// A sensible kind for a routine made before kinds existed (mirrors the AddMobilityLevels backfill): warm-ups by
    /// name, "yoga" a yoga flow (it already posted to Hevy as Yoga), else a stretch.
    /// </summary>
    public static MobilityRoutineKind DefaultKindFor(string name) =>
        name.Contains("mobility a", StringComparison.OrdinalIgnoreCase) || name.Contains("warm", StringComparison.OrdinalIgnoreCase)
            ? MobilityRoutineKind.WarmUp
            : name.Contains("yoga", StringComparison.OrdinalIgnoreCase) ? MobilityRoutineKind.YogaFlow : MobilityRoutineKind.Stretch;

    /// <summary>Renames the routine and changes its target duration; drills are untouched.</summary>
    public void UpdateDetails(string name, int targetMinutes)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Routine name is required");
        CheckRule(targetMinutes > 0, "Target minutes must be positive");

        Name = name.Trim();
        TargetMinutes = targetMinutes;
    }

    /// <summary>Changes the kind (and the follow-along link). A follow-along needs a link; other kinds need a drill.</summary>
    public void ChangeKind(MobilityRoutineKind kind, string? followAlongUrl)
    {
        var url = ValidatedUrl(kind, followAlongUrl);
        ValidatedDrills(_drills, kind);
        Kind = kind;
        FollowAlongUrl = url;
    }

    /// <summary>
    /// Applies a finished session's per-drill feedback through <see cref="DrillProgressionRule"/>. Idempotent on the
    /// session key; unknown drill names are ignored. Returns the changes made (empty when only counts moved).
    /// </summary>
    public IReadOnlyList<DrillProgressionEntry> RecordFeedback(
        string sessionKey,
        IEnumerable<DrillFeedback> feedback,
        DateTimeOffset at,
        int requiredCleanSessions = DrillProgressionRule.DefaultCleanSessions)
    {
        CheckRule(!string.IsNullOrWhiteSpace(sessionKey), "A session key is required");
        var key = sessionKey.Trim();
        if (_progressionLog.Any(e => e.SessionKey == key))
        {
            return [];
        }

        var changeId = Guid.NewGuid();
        var entries = new List<DrillProgressionEntry>();
        foreach (var item in feedback ?? [])
        {
            var index = IndexOf(item.DrillName);
            if (index < 0)
            {
                continue;
            }

            var before = _drills[index];
            var result = DrillProgressionRule.Evaluate(before, item.Outcome, requiredCleanSessions);
            _drills[index] = result.Drill;
            if (result.Change is { } change)
            {
                entries.Add(new DrillProgressionEntry(changeId, at, key, before.Name, change, result.Reason!, before));
            }
        }

        _progressionLog.Add(new DrillProgressionEntry(changeId, at, key, string.Empty, DrillChangeKind.SessionRecorded,
            "Session feedback recorded", null));
        _progressionLog.AddRange(entries);
        return entries;
    }

    /// <summary>Accepts (move up, dose to the new level's bottom) or declines the drill's pending level suggestion.</summary>
    public DrillProgressionEntry RespondToSuggestion(string drillName, bool accept, DateTimeOffset at)
    {
        var index = IndexOf(drillName);
        CheckRule(index >= 0, $"Drill '{drillName}' not found");
        var before = _drills[index];
        CheckRule(before.SuggestedLevel is not null, "There is no suggestion to accept or decline");

        var target = before.SuggestedLevel!.Value;
        var level = before.Levels[target];
        DrillProgressionEntry entry;
        if (accept)
        {
            _drills[index] = before.WithProgress(target, level.StartingDose, 0, null);
            entry = new DrillProgressionEntry(Guid.NewGuid(), at, null, before.Name, DrillChangeKind.LevelUp,
                $"Moved up to {level.Label}", before);
        }
        else
        {
            _drills[index] = before.WithProgress(before.CurrentLevel, before.Dose, 0, null);
            entry = new DrillProgressionEntry(Guid.NewGuid(), at, null, before.Name, DrillChangeKind.SuggestionDeclined,
                $"You stayed at {before.CurrentLevelDetails!.Label}", before);
        }

        _progressionLog.Add(entry);
        return entry;
    }

    /// <summary>Phase bump: every timed hold gets <paramref name="seconds"/> longer (ranges shift with it). One undoable change.</summary>
    public IReadOnlyList<DrillProgressionEntry> ApplyHoldIncrease(int seconds, DateTimeOffset at)
    {
        CheckRule(seconds > 0, "The hold increase must be positive");
        var changeId = Guid.NewGuid();
        var entries = new List<DrillProgressionEntry>();
        for (var i = 0; i < _drills.Count; i++)
        {
            var before = _drills[i];
            if (before.WithHoldIncrease(seconds) is not { } after)
            {
                continue;
            }

            _drills[i] = after;
            entries.Add(new DrillProgressionEntry(changeId, at, null, before.Name, DrillChangeKind.HoldIncreased,
                $"Phase bump: holds +{seconds} s ({DrillProgressionRule.Format(before.Dose)} to {DrillProgressionRule.Format(after.Dose)})", before));
        }

        _progressionLog.AddRange(entries);
        return entries;
    }

    /// <summary>Reverts the most recent change (every drill it touched) to how it was. False when there is nothing to undo.</summary>
    public bool UndoLastChange(DateTimeOffset at)
    {
        var last = _progressionLog.LastOrDefault(e => !e.Undone && e.Before is not null);
        if (last is null)
        {
            return false;
        }

        for (var i = 0; i < _progressionLog.Count; i++)
        {
            var entry = _progressionLog[i];
            if (entry.ChangeId != last.ChangeId || entry.Undone)
            {
                continue;
            }

            if (entry.Before is { } before && IndexOf(entry.DrillName) is var index and >= 0)
            {
                _drills[index] = before;
            }

            _progressionLog[i] = entry with { Undone = true };
        }

        return true;
    }

    public void AddDrill(MobilityDrill drill)
    {
        CheckRule(drill is not null, "Drill is required");
        CheckRule(!HasDrill(drill!.Name), $"Drill names must be unique within a routine: '{drill.Name}' already exists");
        _drills.Add(drill);
    }

    /// <summary>Removes the named drill. Returns false when no drill has that name.</summary>
    public bool RemoveDrill(string name)
    {
        var index = IndexOf(name);
        if (index < 0)
        {
            return false;
        }

        CheckRule(Kind == MobilityRoutineKind.FollowAlong || _drills.Count > 1, "A routine needs at least one drill");
        _drills.RemoveAt(index);
        return true;
    }

    public void MoveDrill(string name, int newIndex)
    {
        var index = IndexOf(name);
        CheckRule(index >= 0, $"Drill '{name}' not found");
        CheckRule(newIndex >= 0 && newIndex < _drills.Count, $"New index must be between 0 and {_drills.Count - 1}");

        var drill = _drills[index];
        _drills.RemoveAt(index);
        _drills.Insert(newIndex, drill);
    }

    public void ReplaceDrills(IEnumerable<MobilityDrill> drills)
    {
        var list = ValidatedDrills(drills, Kind);
        _drills.Clear();
        _drills.AddRange(list);
    }

    private bool HasDrill(string name) => IndexOf(name) >= 0;

    private int IndexOf(string name) =>
        _drills.FindIndex(d => string.Equals(d.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string? ValidatedUrl(MobilityRoutineKind kind, string? url)
    {
        if (kind != MobilityRoutineKind.FollowAlong)
        {
            return null;
        }

        var trimmed = url?.Trim();
        CheckRule(Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
            "A follow-along routine needs a web link (http or https)");
        CheckRule(trimmed!.Length <= 2000, "The follow-along link is too long");
        return trimmed;
    }

    private static List<MobilityDrill> ValidatedDrills(IEnumerable<MobilityDrill> drills, MobilityRoutineKind kind)
    {
        var list = drills?.ToList() ?? new List<MobilityDrill>();
        CheckRule(kind == MobilityRoutineKind.FollowAlong || list.Count > 0, "A routine needs at least one drill");

        var distinctNames = list.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        CheckRule(distinctNames == list.Count, "Drill names must be unique within a routine");

        return list;
    }
}
