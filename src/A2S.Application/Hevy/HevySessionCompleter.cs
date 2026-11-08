using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Hevy;

/// <summary>
/// Completes a scheduled session from a Hevy workout exactly as the UI would: a lift goes through
/// <see cref="CompleteDayCommand"/> (same <see cref="ExercisePerformanceRequest"/> shape, so A2S2
/// progression and undo behave as for manual entry) and then <see cref="CompleteSessionCommand"/>;
/// cardio and mobility go through <see cref="CompleteSessionCommand"/> with a log built from the sets.
/// Shared by the pull and by assigning an inbox workout by hand.
/// </summary>
public sealed class HevySessionCompleter
{
    private readonly ISender _sender;
    private readonly IMobilityRoutineRepository _routines;

    public HevySessionCompleter(ISender sender, IMobilityRoutineRepository routines)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _routines = routines ?? throw new ArgumentNullException(nameof(routines));
    }

    /// <param name="extra">
    /// True records the plan completion only (an additional session that must not drive A2S2
    /// progression); false also completes the A2S2 day for a lift.
    /// </param>
    public async Task<Result<SessionCompletionId>> CompleteAsync(
        TrainingPlan plan,
        Workout? program,
        ScheduledSession session,
        DateOnly date,
        HevyWorkout workout,
        IReadOnlyDictionary<string, string> templateLinks,
        bool extra,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(templateLinks);

        var command = session.Kind switch
        {
            SessionKind.Lift => await LiftAsync(plan, program, session, date, workout, templateLinks, extra, ct),
            SessionKind.Cardio => Result.Success(new CompleteSessionCommand(
                plan.Id.Value, date, SessionKind.Cardio,
                CardioProtocolId: session.CardioProtocolId?.Value,
                Cardio: CardioLogFrom(workout),
                HevyWorkoutId: workout.Id)),
            SessionKind.Mobility => Result.Success(new CompleteSessionCommand(
                plan.Id.Value, date, SessionKind.Mobility,
                MobilityRoutineId: session.MobilityRoutineId?.Value,
                DrillTicks: await DrillTicksAsync(session, workout, templateLinks, ct),
                HevyWorkoutId: workout.Id)),
            _ => Result.Failure<CompleteSessionCommand>("A rest day is not completed.", ErrorCode.ValidationFailed)
        };

        if (command.IsFailure)
        {
            return PlanAccess.Fail<SessionCompletionId>(command);
        }

        var completion = await _sender.Send(command.Value, ct);
        return completion.IsFailure
            ? PlanAccess.Fail<SessionCompletionId>(completion)
            : Result.Success(new SessionCompletionId(completion.Value.Id));
    }

    private async Task<Result<CompleteSessionCommand>> LiftAsync(
        TrainingPlan plan,
        Workout? program,
        ScheduledSession session,
        DateOnly date,
        HevyWorkout workout,
        IReadOnlyDictionary<string, string> templateLinks,
        bool extra,
        CancellationToken ct)
    {
        var liftDay = session.LiftDay!.Value;
        if (!extra)
        {
            if (program is null)
            {
                return Result.Failure<CompleteSessionCommand>("The plan has no linked A2S2 workout.", ErrorCode.ValidationFailed);
            }

            var performances = BuildPerformances(program, liftDay, workout, templateLinks);
            if (performances.Count == 0)
            {
                return Result.Failure<CompleteSessionCommand>("none of the day's exercises appear in the workout", ErrorCode.ValidationFailed);
            }

            var day = await _sender.Send(new CompleteDayCommand(program.Id.Value, liftDay, performances), ct);
            if (day.IsFailure)
            {
                return Result.Failure<CompleteSessionCommand>(day.Error, day.ErrorCode == ErrorCode.None ? ErrorCode.DomainRuleViolation : day.ErrorCode);
            }
        }

        return Result.Success(new CompleteSessionCommand(plan.Id.Value, date, SessionKind.Lift, LiftDay: liftDay, HevyWorkoutId: workout.Id));
    }

    /// <summary>
    /// For each planned exercise of the day, in order: the workout's working (normal/failure) sets for its
    /// template, weight in kg, reps, and <c>WasAmrap</c> on the last set when the plan calls for an AMRAP.
    /// Exercises absent from the workout are left out (their progression is untouched, as when skipped by hand).
    /// </summary>
    public static List<ExercisePerformanceRequest> BuildPerformances(
        Workout program,
        DayNumber liftDay,
        HevyWorkout workout,
        IReadOnlyDictionary<string, string> templateLinks)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(workout);

        var templateWeek = program.GetTemplateWeek(program.CurrentWeek);
        var blockType = program.GetBlockType(program.CurrentWeek);
        var performances = new List<ExercisePerformanceRequest>();

        foreach (var exercise in program.GetExercisesForDay(liftDay))
        {
            var templateId = WorkoutMatcher.HevyTemplateIdFor(exercise, templateLinks);
            if (templateId is null)
            {
                continue;
            }

            var sets = workout.Exercises
                .Where(e => string.Equals(e.ExerciseTemplateId, templateId, StringComparison.Ordinal))
                .SelectMany(e => e.Sets.OrderBy(s => s.Index))
                .Where(s => HevySetTypes.IsWorking(s.Type))
                .ToList();
            if (sets.Count == 0)
            {
                continue;
            }

            var plannedAmrap = exercise.CalculatePlannedSets(templateWeek, blockType).Any(p => p.IsAmrap);
            performances.Add(new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id.Value,
                CompletedSets = sets.Select((set, index) => new CompletedSetRequest
                {
                    SetNumber = index + 1,
                    Weight = set.WeightKg ?? 0m,
                    WeightUnit = WeightUnit.Kilograms,
                    ActualReps = set.Reps ?? 0,
                    WasAmrap = plannedAmrap && index == sets.Count - 1
                }).ToList()
            });
        }

        return performances;
    }

    /// <summary>Duration from the sets (or the wall clock), total distance, and the hardest RPE Hevy recorded.</summary>
    public static CardioLogDto CardioLogFrom(HevyWorkout workout)
    {
        ArgumentNullException.ThrowIfNull(workout);
        var rpe = workout.Exercises.SelectMany(e => e.Sets).Select(s => s.Rpe).Where(r => r.HasValue).Select(r => (int)Math.Round(r!.Value)).DefaultIfEmpty(0).Max();
        return new CardioLogDto(
            Math.Max(1, workout.TotalDurationSeconds()),
            null,
            rpe is >= 1 and <= 10 ? rpe : null,
            workout.TotalDistanceMeters(),
            null);
    }

    /// <summary>One tick per drill of the session's routine: done when the drill's template is in the workout.</summary>
    private async Task<IReadOnlyList<bool>?> DrillTicksAsync(ScheduledSession session, HevyWorkout workout, IReadOnlyDictionary<string, string> templateLinks, CancellationToken ct)
    {
        if (session.MobilityRoutineId is not { } routineId)
        {
            return null;
        }

        var routine = await _routines.GetByIdAsync(routineId, ct);
        if (routine is null)
        {
            return null;
        }

        var present = workout.Exercises
            .Select(e => templateLinks.TryGetValue(e.ExerciseTemplateId, out var key) ? key : null)
            .Where(k => k is not null)
            .ToHashSet(StringComparer.Ordinal);

        return routine.Drills.Select(d => present.Contains(HevyLocalKeys.DrillTemplate(d.Name))).ToList();
    }
}
