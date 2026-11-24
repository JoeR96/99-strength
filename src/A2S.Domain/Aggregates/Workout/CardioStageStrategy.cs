using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Cardio as an exercise: a lane (hard stair climber or easy incline walk) climbing a ladder of stages.
/// Each session's effort and pain decide the next prescription via <see cref="CardioLaneRules"/>;
/// the ladder moves are <see cref="CardioLadder"/>. There are no sets: the session is a <see cref="PlannedCardioBlock"/>.
/// While the level (or the walk's speed) is blank, sessions change nothing.
/// </summary>
public sealed class CardioStageStrategy : ExerciseProgression
{
    public const string TypeName = "CardioStage";

    public CardioLane Lane { get; private set; }
    public IReadOnlyList<CardioStageDefinition> Stages { get; private set; } = [];
    public CardioSegment? WarmUp { get; private set; }
    public CardioSegment? CoolDown { get; private set; }
    public bool RequiresSpeed { get; private set; }
    public CardioStageState State { get; private set; }

    // EF Core constructor
    private CardioStageStrategy()
    {
        State = null!;
    }

    private CardioStageStrategy(ExerciseProgressionId id, CardioLanePreset preset)
        : base(id, TypeName)
    {
        Lane = preset.Lane;
        Stages = preset.Stages;
        WarmUp = preset.WarmUp;
        CoolDown = preset.CoolDown;
        RequiresSpeed = preset.RequiresSpeed;
        State = CardioStageState.Start(preset.Stages);
    }

    public static CardioStageStrategy Create(CardioLanePreset preset) =>
        new(new ExerciseProgressionId(Guid.NewGuid()), preset);

    public override PlannedCardioBlock CalculateCardioBlock() => BlockFor(State);

    public override bool IsCardioSettingsPending => CalculateCardioBlock().IsSettingsPending;

    public override IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber) => [];

    /// <summary>Applies the lane rule to the session's result. No cardio result, or settings still blank: no change.</summary>
    public override void ApplyPerformanceResult(ExercisePerformance performance)
    {
        if (performance.Cardio is not { } result || IsCardioSettingsPending)
        {
            return;
        }

        State = Next(result);
    }

    public override string GetProgressionChangeDescription(ExercisePerformance performance)
    {
        if (performance.Cardio is not { } result || IsCardioSettingsPending)
        {
            return "No change";
        }

        var decision = CardioLaneRules.Decide(Lane, result, State.SolidsBanked, State.MissStreak, out var solids, out var misses);
        var current = CalculateCardioBlock().Title;
        var next = BlockFor(Next(result)).Title;

        return decision switch
        {
            CardioStepDecision.StepUp when next != current => $"Step up: {next}",
            CardioStepDecision.StepBack when next != current => $"Step back: {next}",
            CardioStepDecision.Hold when result.IsMiss =>
                $"Same again ({misses} of {CardioLaneRules.MissesToStepBack} before a step back)",
            CardioStepDecision.Hold when result.Effort == EffortRating.Solid && Lane == CardioLane.Hard =>
                $"Banked {solids} of {CardioLaneRules.SolidsToStepUp} Solids",
            _ => "Same again"
        };
    }

    /// <summary>
    /// Sets today's level/incline and/or speed (null leaves a value as it is). Returns the block text before and
    /// after, for the audit. When the prescription would read the same, the state is left exactly as it was.
    /// </summary>
    internal CardioSettingsChange SetSettings(decimal? intensity, decimal? speed)
    {
        CheckRule(intensity is null or > 0, "Intensity must be greater than zero");
        CheckRule(speed is null or > 0, "Speed must be greater than zero");

        var oldText = CalculateCardioBlock().Text;
        var next = CardioLadder.Choose(State, intensity, speed);
        var newText = BlockFor(next).Text;
        if (newText != oldText)
        {
            State = next;
        }

        return new CardioSettingsChange(oldText, newText);
    }

    /// <summary>
    /// Puts the lane at the start of its first stage with <paramref name="minutes"/> of work (inside that stage's range)
    /// and the stage's entry intensity, counters and history cleared. Returns the block text before and after.
    /// </summary>
    internal CardioSettingsChange Seed(int minutes)
    {
        var first = Stages[0];
        CheckRule(first.MinMinutes is not null && first.MaxMinutes is not null && minutes >= first.MinMinutes && minutes <= first.MaxMinutes,
            $"The first stage runs {first.MinMinutes} to {first.MaxMinutes} min");

        var oldText = CalculateCardioBlock().Text;
        State = CardioStageState.Start(Stages) with { Minutes = minutes };
        return new CardioSettingsChange(oldText, CalculateCardioBlock().Text);
    }

    public override ProgressionSummary GetSummary()
    {
        var block = CalculateCardioBlock();
        return new ProgressionSummary
        {
            Type = "Cardio Stage",
            Details = new Dictionary<string, string>
            {
                ["Lane"] = Lane.ToString(),
                ["Stage"] = $"{block.StageNumber} of {block.StageCount}: {block.StageName}",
                ["Prescription"] = block.Text
            }
        };
    }

    public override ProgressionData GetProgressionData()
    {
        var block = CalculateCardioBlock();
        return new ProgressionData
        {
            CardioLane = Lane,
            CardioStageNumber = block.StageNumber,
            CardioStageName = block.StageName,
            CardioPrescription = block.Text
        };
    }

    public override Weight? GetCurrentWeight() => null;

    public override TrainingMax? GetTrainingMax() => null;

    public override Weight GetWeightIncrement() => Weight.Create(0, WeightUnit.Kilograms);

    public override bool SupportsUnilateral => false;

    public override bool IsUnilateral => false;

    public override bool IsWeightPending => false;

    internal override ProgressionSnapshot CaptureSnapshot(ExerciseId exerciseId, string exerciseName) =>
        ProgressionSnapshot.FromState(exerciseId, exerciseName, CaptureState());

    internal override void RestoreFromSnapshot(ProgressionSnapshot snapshot)
    {
        var state = snapshot.GetCardioStageState()
            ?? throw new InvalidOperationException("Failed to deserialize CardioStage snapshot state");
        RestoreFromState(state);
    }

    internal CardioStageState CaptureState() => State;

    internal void RestoreFromState(CardioStageState state) => State = state;

    private CardioStageState Next(CardioResult result)
    {
        var decision = CardioLaneRules.Decide(Lane, result, State.SolidsBanked, State.MissStreak, out var solids, out var misses);
        var moved = decision switch
        {
            CardioStepDecision.StepUp => CardioLadder.StepUp(Stages, State),
            CardioStepDecision.StepBack => CardioLadder.StepBack(State),
            _ => State
        };

        // Decide clears both counters on a step, as the ladder does; at the very top (or with no history)
        // the ladder returns the state unchanged, and the cleared counters still apply.
        return moved with { SolidsBanked = solids, MissStreak = misses };
    }

    private PlannedCardioBlock BlockFor(CardioStageState state) =>
        PlannedCardioBlock.From(Lane, Stages, WarmUp, CoolDown, RequiresSpeed, state);
}

/// <summary>A settings edit on a cardio exercise: the block text before and after, for the progression history.</summary>
public sealed record CardioSettingsChange(string OldText, string NewText)
{
    public bool IsUnchanged => OldText == NewText;
}
