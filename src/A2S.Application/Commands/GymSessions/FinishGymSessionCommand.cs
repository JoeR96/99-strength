using A2S.Application.Commands.CompleteDay;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;
using FluentValidation;
using System.Text.Json.Serialization;

namespace A2S.Application.Commands.GymSessions;

/// <summary>
/// Finishes a gym-mode session (brief § 3a): completes the A2S2 day exactly as <see cref="CompleteDayCommand"/>
/// does (same performances shape; exercises left out keep their progression, which is "Finish early"),
/// records the plan's lift completion and the finisher's cardio completion, and queues the workout for
/// Hevy. <paramref name="ClientSessionId"/> is generated on the phone when the session starts and is the
/// idempotency key: finishing the same session again returns the first result and changes nothing.
/// </summary>
public sealed record FinishGymSessionCommand(
    Guid ClientSessionId,
    Guid WorkoutId,
    DayNumber Day,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    IReadOnlyList<ExercisePerformanceRequest> Performances,
    IReadOnlyList<bool>? WarmUpTicks = null,
    FinisherLogRequest? Finisher = null) : ICommand<Result<FinishGymSessionResult>>;

/// <summary>What was logged for the attached finisher. Level and floors are what the stair machine showed.</summary>
public sealed record FinisherLogRequest(
    int DurationSeconds,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Effort>))] Effort Effort,
    int? Level = null,
    int? Floors = null);

/// <summary>The answer to a finish (or to its replay).</summary>
public sealed record FinishGymSessionResult
{
    public Guid ClientSessionId { get; init; }
    /// <summary>The A2S2 day result (progression changes, next session). Null only for a replay whose stored result was lost.</summary>
    public CompleteDayResult? CompleteDay { get; init; }
    public Guid? CompletionId { get; init; }
    public Guid? FinisherCompletionId { get; init; }

    /// <summary>Pending, Posted, Failed or Skipped (no Hevy key).</summary>
    public string HevyStatus { get; init; } = string.Empty;
    public string? HevyWorkoutId { get; init; }

    /// <summary>True when this session had already been finished and nothing was done again.</summary>
    public bool Replayed { get; init; }
}

public sealed class FinishGymSessionCommandValidator : AbstractValidator<FinishGymSessionCommand>
{
    public FinishGymSessionCommandValidator()
    {
        RuleFor(x => x.ClientSessionId).NotEmpty();
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.Day).IsInEnum();
        RuleFor(x => x.EndedAt).GreaterThanOrEqualTo(x => x.StartedAt).WithMessage("A session cannot end before it starts.");
        RuleFor(x => x.EndedAt - x.StartedAt).LessThanOrEqualTo(TimeSpan.FromHours(12)).WithMessage("A gym session is at most 12 hours long.");
        RuleFor(x => x.Performances).NotNull();
        RuleForEach(x => x.Performances).ChildRules(p =>
        {
            p.RuleFor(e => e.ExerciseId).NotEmpty();
            p.RuleForEach(e => e.CompletedSets).ChildRules(s =>
            {
                s.RuleFor(c => c.ActualReps).InclusiveBetween(0, 200);
                s.RuleFor(c => c.Weight).InclusiveBetween(0m, 1000m);
            });
        });
        When(x => x.Finisher is not null, () =>
        {
            RuleFor(x => x.Finisher!.DurationSeconds).InclusiveBetween(1, 4 * 3600);
            RuleFor(x => x.Finisher!.Effort).IsInEnum();
            RuleFor(x => x.Finisher!.Level).InclusiveBetween(1, 100).When(x => x.Finisher!.Level.HasValue);
            RuleFor(x => x.Finisher!.Floors).InclusiveBetween(0, 2000).When(x => x.Finisher!.Floors.HasValue);
        });
    }
}

/// <summary>What a gym session logged, stored on its <c>HevyWorkoutPost</c> and turned into a Hevy workout at post time.</summary>
public sealed record GymSessionPayload
{
    public Guid WorkoutId { get; init; }
    public DayNumber Day { get; init; }
    public Guid? WarmUpRoutineId { get; init; }
    public IReadOnlyList<bool>? WarmUpTicks { get; init; }
    public IReadOnlyList<ExercisePerformanceRequest> Performances { get; init; } = [];
    public Guid? FinisherProtocolId { get; init; }
    public FinisherLogRequest? Finisher { get; init; }
}
