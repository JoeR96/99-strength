using System.Text.Json;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Commands.RetrofixLinearTm;

internal static class LinearProgressionSnapshotProperties
{
    public const string TrainingMaxValue = "TrainingMaxValue";
    public const string TrainingMaxUnit = "TrainingMaxUnit";
    public const string UseAmrap = "UseAmrap";
    public const string BaseSetsPerExercise = "BaseSetsPerExercise";
}

public sealed class RetrofixLinearTmCommandHandler
    : IRequestHandler<RetrofixLinearTmCommand, Result<List<RetrofixLinearTmResult>>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RetrofixLinearTmCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<RetrofixLinearTmResult>>> Handle(
        RetrofixLinearTmCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure<List<RetrofixLinearTmResult>>("User must be authenticated.");
            }

            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId), cancellationToken);

            if (workout == null)
            {
                return Result.Failure<List<RetrofixLinearTmResult>>("Workout not found.");
            }

            if (workout.UserId != userId.Value)
            {
                return Result.Failure<List<RetrofixLinearTmResult>>("You can only modify your own workouts.");
            }

            var exerciseId = new ExerciseId(request.ExerciseId);
            var exercise = workout.Exercises.FirstOrDefault(e => e.Id == exerciseId);
            if (exercise == null)
            {
                return Result.Failure<List<RetrofixLinearTmResult>>($"Exercise {exerciseId} not found in this workout.");
            }

            if (exercise.Progression is not LinearProgressionStrategy linear)
            {
                return Result.Failure<List<RetrofixLinearTmResult>>($"Exercise {exercise.Name} does not use Linear progression.");
            }

            var changes = RetrofixLinearTmHistory(workout, exerciseId, linear, request.OriginalStartingTm);

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var results = changes
                .Select(c => new RetrofixLinearTmResult(c.Week, c.OldTm, c.NewTm))
                .ToList();

            return Result.Success(results);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<List<RetrofixLinearTmResult>>(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<RetrofixLinearTmResult>>($"Failed to retrofix TM history: {ex.Message}");
        }
    }

    private static List<(int Week, decimal OldTm, decimal NewTm)> RetrofixLinearTmHistory(
        Workout workout, ExerciseId exerciseId, LinearProgressionStrategy linear, decimal originalStartingTm)
    {
        var changes = new List<(int Week, decimal OldTm, decimal NewTm)>();

        // Sort activities chronologically for this exercise's day
        var activitiesForExercise = workout.CompletedActivities
            .Select((activity, index) => (activity, index))
            .Where(x => x.activity.Performances.Any(p => p.ExerciseId == exerciseId))
            .OrderBy(x => x.activity.WeekNumber)
            .ThenBy(x => x.activity.CompletedAt)
            .ToList();

        if (activitiesForExercise.Count == 0)
        {
            return changes;
        }

        // Walk through activities, recalculating TM with proper precision
        var currentTm = originalStartingTm;

        foreach (var (activity, activityIndex) in activitiesForExercise)
        {
            // Find the snapshot for this exercise (captured BEFORE progression)
            var snapshotIndex = -1;
            for (var i = 0; i < activity.ProgressionSnapshots.Count; i++)
            {
                if (activity.ProgressionSnapshots[i].ExerciseId == exerciseId)
                {
                    snapshotIndex = i;
                    break;
                }
            }

            if (snapshotIndex >= 0)
            {
                var oldSnapshot = activity.ProgressionSnapshots[snapshotIndex];
                using var oldJson = JsonDocument.Parse(oldSnapshot.ProgressionStateJson);
                var oldTmValue = oldJson.RootElement.GetProperty(LinearProgressionSnapshotProperties.TrainingMaxValue).GetDecimal();

                // Create corrected snapshot with the recalculated TM
                var correctedJson = JsonSerializer.Serialize(new
                {
                    TrainingMaxValue = currentTm,
                    TrainingMaxUnit = oldJson.RootElement.GetProperty(LinearProgressionSnapshotProperties.TrainingMaxUnit).GetInt32(),
                    UseAmrap = oldJson.RootElement.GetProperty(LinearProgressionSnapshotProperties.UseAmrap).GetBoolean(),
                    BaseSetsPerExercise = oldJson.RootElement.GetProperty(LinearProgressionSnapshotProperties.BaseSetsPerExercise).GetInt32()
                });

                var correctedSnapshot = new ProgressionSnapshot(
                    oldSnapshot.ExerciseId,
                    oldSnapshot.ExerciseName,
                    oldSnapshot.ProgressionType,
                    correctedJson);

                // Replace with new immutable activity containing corrected snapshot
                var correctedActivity = activity.WithReplacedSnapshot(snapshotIndex, correctedSnapshot);
                workout.ReplaceCompletedActivity(activityIndex, correctedActivity);

                changes.Add((activity.WeekNumber, oldTmValue, currentTm));
            }

            // Now apply the AMRAP delta to get the TM for the NEXT week
            var performance = activity.Performances.FirstOrDefault(p => p.ExerciseId == exerciseId);
            if (performance != null && !performance.SkipProgression)
            {
                var delta = performance.GetAmrapDelta();
                var adjustment = AmrapDeltaTable.GetAdjustment(delta);

                if (adjustment.Type != AdjustmentType.None)
                {
                    currentTm = Math.Round(currentTm * (1 + adjustment.Amount), 2);
                }
            }
        }

        // Update the current exercise TM to the correctly calculated value
        workout.SetExerciseTrainingMax(exerciseId, TrainingMax.Create(currentTm, linear.TrainingMax.Unit));

        return changes;
    }
}
