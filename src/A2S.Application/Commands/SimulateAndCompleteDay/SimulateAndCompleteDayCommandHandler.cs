using A2S.Application.Commands.CompleteDay;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.SimulateAndCompleteDay;

/// <summary>
/// Handles SimulateAndCompleteDayCommand.
///
/// For each exercise assigned to the workout's current day, synthesises a
/// CompletedSetRequest list based on the planned sets, then fires the existing
/// CompleteDayCommand via MediatR so the simulation goes through the exact same
/// progression path a real user would.
///
/// Outcomes are weighted-random per day (not per exercise) so each simulated
/// day has a coherent "success / maintain / fail" story.
/// </summary>
public sealed class SimulateAndCompleteDayCommandHandler
    : IRequestHandler<SimulateAndCompleteDayCommand, Result<SimulateAndCompleteDayResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ISender _sender;

    public SimulateAndCompleteDayCommandHandler(
        IWorkoutRepository workoutRepository,
        ISender sender)
    {
        _workoutRepository = workoutRepository;
        _sender = sender;
    }

    public async Task<Result<SimulateAndCompleteDayResult>> Handle(
        SimulateAndCompleteDayCommand request,
        CancellationToken cancellationToken)
    {
        var workout = await _workoutRepository.GetByIdAsync(
            new WorkoutId(request.WorkoutId),
            cancellationToken);

        if (workout == null)
        {
            return Result.Failure<SimulateAndCompleteDayResult>("Workout not found.");
        }

        if (workout.Status != WorkoutStatus.Active)
        {
            return Result.Failure<SimulateAndCompleteDayResult>(
                "Workout must be active to simulate a day.");
        }

        var day = (DayNumber)workout.CurrentDay;
        var rng = request.Seed.HasValue ? new Random(request.Seed.Value) : new Random();

        // Pick one outcome for the whole day.
        var roll = rng.NextDouble();
        var outcome = roll < request.SuccessRate
            ? SimulationOutcome.Success
            : roll < request.SuccessRate + request.MaintainRate
                ? SimulationOutcome.Maintain
                : SimulationOutcome.Fail;

        var performanceRequests = new List<ExercisePerformanceRequest>();

        foreach (var exercise in workout.GetExercisesForDay(day))
        {
            var plannedSets = workout.GetPlannedSetsForExercise(exercise.Id).ToList();
            if (plannedSets.Count == 0) continue;

            var completedSets = plannedSets.Select(ps =>
            {
                int actualReps = outcome switch
                {
                    // SUCCESS = all sets >= Maximum. For AMRAP, overshoot by 1 to trigger weight bump.
                    SimulationOutcome.Success => ps.IsAmrap ? ps.TargetReps + 1 : ps.TargetReps,
                    // MAINTAIN = hit Minimum but not Maximum. TargetReps here is Maximum for RepsPerSet,
                    // so shave a rep. Guard against going below 1.
                    SimulationOutcome.Maintain => Math.Max(1, ps.TargetReps - 2),
                    // FAIL = below Minimum. Heuristic: half the target, min 1.
                    SimulationOutcome.Fail => Math.Max(1, ps.TargetReps / 2),
                    _ => ps.TargetReps
                };

                return new CompletedSetRequest
                {
                    SetNumber = ps.SetNumber,
                    Weight = ps.Weight.Value,
                    WeightUnit = ps.Weight.Unit,
                    ActualReps = actualReps,
                    WasAmrap = ps.IsAmrap
                };
            }).ToList();

            performanceRequests.Add(new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id.Value,
                CompletedSets = completedSets
            });
        }

        if (performanceRequests.Count == 0)
        {
            return Result.Failure<SimulateAndCompleteDayResult>(
                $"No exercises to simulate on {day}.");
        }

        var innerResult = await _sender.Send(
            new CompleteDayCommand(request.WorkoutId, day, performanceRequests),
            cancellationToken);

        if (!innerResult.IsSuccess)
        {
            return Result.Failure<SimulateAndCompleteDayResult>(innerResult.Error);
        }

        return Result.Success(new SimulateAndCompleteDayResult
        {
            InnerResult = innerResult.Value,
            OutcomeLabel = outcome.ToString()
        });
    }

    private enum SimulationOutcome
    {
        Success,
        Maintain,
        Fail
    }
}
