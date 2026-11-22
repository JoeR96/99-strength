using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Cardio;

/// <summary>A track template the person can start (and then edit). Not a hard-coded routine.</summary>
public sealed record CardioTrackTemplate(string Key, string Name, CardioModality Modality, string Description);

/// <summary>
/// The shipped track templates (brief § 4c). Each call to <see cref="Create"/> returns a fresh,
/// editable track; nothing here is referenced after creation.
/// </summary>
public static class CardioTrackTemplates
{
    public const string WalkBuilder = "walk-builder";
    public const string WalkToRun = "walk-to-run";
    public const string StairFinisher = "stair-finisher";

    public const string SafetyLine = "Build up gradually, and stop for chest pain, dizziness or unusual breathlessness.";

    public static IReadOnlyList<CardioTrackTemplate> All { get; } =
    [
        new(WalkBuilder, "Walk builder", CardioModality.Walk,
            "Easy zone 2 walking: 20 → 45 min (+5 min a step), then hold 45 min and add 1 % incline a step up to 10 %."),
        new(WalkToRun, "Walk → run", CardioModality.Run,
            "Couch to 5K over 9 weeks (2 runs a week, move on after 3 good sessions), then a 5 K builder: continuous running +10 % a step up to 35 min."),
        new(StairFinisher, "Stair finisher", CardioModality.StairClimber,
            "After lifting, zone 4–5: continuous 5 → 10 min then level +1 and back to 5; then 1 min hard / 1 min easy intervals, 4 → 8 rounds, then hard level +1.")
    ];

    /// <summary>The start gate a template suggests (only Walk → run has one). Applied when the person asks for it.</summary>
    public static IReadOnlyList<GateCondition> DefaultGate(string key) => key == WalkToRun ? StartGate.RunningGate() : [];

    /// <summary>A fresh track from the template; <paramref name="withGate"/> applies <see cref="DefaultGate"/> so it waits until the gate is met.</summary>
    public static CardioTrack Create(string key, UserId userId, DateOnly startedOn, bool withGate)
    {
        var track = Create(key, userId, startedOn);
        var gate = DefaultGate(key);
        if (withGate && gate.Count > 0)
        {
            track.InitialiseGate(gate);
        }

        return track;
    }

    public static CardioTrack Create(string key, UserId userId, DateOnly startedOn) => key switch
    {
        WalkBuilder => CardioTrack.Create(userId, "Walk builder", CardioModality.Walk,
            [
                TrackStage.Double("Build duration", TrackEffortTarget.Easy,
                    new DoubleProgressionSettings(VolumeUnit.Minutes, 20, 45, 5, null, IntensityUnit.None, null, null, null, 2),
                    hrCeilingZone: 2, note: "Easy: you can talk in full sentences."),
                TrackStage.Double("Add incline", TrackEffortTarget.Easy,
                    new DoubleProgressionSettings(VolumeUnit.Minutes, 45, 45, 5, null, IntensityUnit.InclinePercent, 1, 1, 10, 2),
                    hrCeilingZone: 2, note: "Treadmill incline. Easy: you can talk in full sentences.")
            ],
            startedOn,
            new TrackBenchmark("Avg HR, 30 min at a fixed pace", "bpm", HigherIsBetter: false),
            WalkBuilder,
            "Treadmill incline walking."),

        WalkToRun => CardioTrack.Create(userId, "Walk → run", CardioModality.Run,
            [
                TrackStage.Weeks("Couch to 5K", new WeekProgramSettings(CouchToFiveKWeeks(), sessionsToAdvance: 3, sessionsPerWeek: 2)),
                TrackStage.Double("5 K builder", TrackEffortTarget.Easy,
                    new DoubleProgressionSettings(VolumeUnit.Minutes, 30, 35, 1, 10, IntensityUnit.None, null, null, null, 2),
                    note: "Continuous running at an easy, steady pace.")
            ],
            startedOn,
            new TrackBenchmark("5 K time", "min", HigherIsBetter: false),
            WalkToRun,
            "Treadmill or grass preferred."),

        StairFinisher => CardioTrack.Create(userId, "Stair finisher", CardioModality.StairClimber,
            [
                TrackStage.Double("Continuous", TrackEffortTarget.Hard,
                    new DoubleProgressionSettings(VolumeUnit.Minutes, 5, 10, 1, null, IntensityUnit.Level, 6, 1, 9, 2),
                    zoneTargetFraction: 0.5m, note: "After lifting. Steady and hard: zone 4–5."),
                TrackStage.Double("Intervals", TrackEffortTarget.Hard,
                    new DoubleProgressionSettings(VolumeUnit.Rounds, 4, 8, 1, null, IntensityUnit.Level, 10, 1, null, 2, 60, 60),
                    zoneTargetFraction: 0.5m, note: "1 min hard / 1 min easy. Level is the hard minute.")
            ],
            startedOn,
            new TrackBenchmark("Floors in 5 min, all out", "floors", HigherIsBetter: true),
            StairFinisher,
            "Finisher for upper-body days."),

        _ => throw new BusinessRuleViolationException($"Unknown cardio track template '{key}'")
    };

    /// <summary>The nine Couch to 5K weeks, built from the existing protocol's intervals (unchanged).</summary>
    private static IReadOnlyList<ProgramWeek> CouchToFiveKWeeks()
    {
        var weeks = new List<ProgramWeek>();
        for (var week = CardioProtocol.CouchToFiveKFirstWeek; week <= CardioProtocol.CouchToFiveKLastWeek; week++)
        {
            var intervals = CardioProtocol.CouchToFiveKWeek(week);
            var runs = intervals.Where(i => i.Effort == CardioEffort.Work).ToList();
            var minutes = (int)Math.Ceiling(intervals.Sum(i => i.Seconds) / 60m);
            var runMinutes = runs.Sum(r => r.Seconds) / 60m;
            var description = runs.Count == 1
                ? $"Run {DoubleProgressionStrategy.Number(runMinutes)} min non-stop, 5 min walk either side"
                : $"{runs.Count} runs, {DoubleProgressionStrategy.Number(runMinutes)} min running in all, walking between";
            weeks.Add(new ProgramWeek(week, $"C25K wk {week}", minutes, description));
        }

        return weeks;
    }
}
