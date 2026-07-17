using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Commands.CompleteDay;

/// <summary>
/// Handler for CompleteDayCommand.
/// Completes a training day by recording performance and applying progression rules.
/// </summary>
public sealed class CompleteDayCommandHandler : IRequestHandler<CompleteDayCommand, Result<CompleteDayResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public CompleteDayCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<CompleteDayResult>> Handle(CompleteDayCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure<CompleteDayResult>("User must be authenticated.");
            }

            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure<CompleteDayResult>("Workout not found.");
            }

            if (workout.UserId != userId.Value)
            {
                return Result.Failure<CompleteDayResult>("You can only complete days in your own workouts.");
            }

            if (workout.Status != WorkoutStatus.Active)
            {
                return Result.Failure<CompleteDayResult>("Workout must be active to complete a day.");
            }

            var dayExercises = workout.Exercises
                .Where(e => e.AssignedDay == request.Day)
                .ToDictionary(e => e.Id.Value, e => e);

            if (!dayExercises.Any())
            {
                return Result.Failure<CompleteDayResult>($"No exercises assigned to {request.Day}.");
            }

            // A Cable/Machine weight raised by progression is confirmed implicitly by the
            // next session: whatever the user actually lifted becomes the working weight
            // (gym stacks vary, so the suggested weight is only a starting point). Must
            // happen before progression is applied so it builds on the real weight.
            foreach (var performanceRequest in request.Performances)
            {
                if (dayExercises.TryGetValue(performanceRequest.ExerciseId, out var pendingEx)
                    && pendingEx.Progression.PendingWeightConfirmation
                    && performanceRequest.CompletedSets.Count > 0)
                {
                    var firstSet = performanceRequest.CompletedSets[0];
                    var actualWeight = Weight.Create(firstSet.Weight, firstSet.WeightUnit);
                    var currentWeight = pendingEx.Progression.GetCurrentWeight();
                    if (currentWeight == null || currentWeight.Unit == actualWeight.Unit)
                    {
                        workout.ConfirmExerciseWorkingWeight(pendingEx.Id, actualWeight);
                    }
                }
            }

            var performances = new List<ExercisePerformance>();
            var progressionChanges = new List<ProgressionChangeDto>();

            foreach (var performanceRequest in request.Performances)
            {
                if (!dayExercises.TryGetValue(performanceRequest.ExerciseId, out var exercise))
                {
                    return Result.Failure<CompleteDayResult>(
                        $"Exercise {performanceRequest.ExerciseId} not found or not assigned to {request.Day}.");
                }

                var templateWeek = workout.GetTemplateWeek(workout.CurrentWeek);
                var blockType = workout.GetBlockType(workout.CurrentWeek);
                var plannedSets = exercise.CalculatePlannedSets(templateWeek, blockType).ToList();

                var completedSets = performanceRequest.CompletedSets
                    .Select(s => new CompletedSet(
                        s.SetNumber,
                        Weight.Create(s.Weight, s.WeightUnit),
                        s.ActualReps,
                        s.WasAmrap))
                    .ToList();

                var performance = new ExercisePerformance(
                    exercise.Id,
                    plannedSets,
                    completedSets,
                    skipProgression: performanceRequest.WasTemporarySubstitution);

                performances.Add(performance);

                var changeDescription = performanceRequest.WasTemporarySubstitution
                    ? "Skipped (temporary substitution)"
                    : GetProgressionChangeDescription(exercise, performance);
                progressionChanges.Add(new ProgressionChangeDto
                {
                    ExerciseId = exercise.Id.Value,
                    ExerciseName = exercise.Name,
                    Change = changeDescription
                });
            }

            var pendingWeightExercises = new List<PendingWeightExerciseDto>();
            foreach (var performanceRequest in request.Performances)
            {
                if (dayExercises.TryGetValue(performanceRequest.ExerciseId, out var ex)
                    && ex.Progression.IsWeightPending
                    && performanceRequest.CompletedSets.Count > 0)
                {
                    var firstSet = performanceRequest.CompletedSets[0];
                    pendingWeightExercises.Add(new PendingWeightExerciseDto
                    {
                        ExerciseId = ex.Id.Value,
                        ExerciseName = ex.Name,
                        SuggestedWeight = firstSet.Weight,
                        WeightUnit = firstSet.WeightUnit.ToString(),
                        ConfirmationType = ConfirmationType.StartingWeight
                    });
                }
            }

            var weekBeforeComplete = workout.CurrentWeek;

            workout.CompleteDay(request.Day, performances);

            // (Cable/Machine exercises that had weight increased during progression)
            foreach (var ex in dayExercises.Values)
            {
                if (ex.Progression.PendingWeightConfirmation && ex.Progression.SuggestedWeight != null)
                {
                    pendingWeightExercises.Add(new PendingWeightExerciseDto
                    {
                        ExerciseId = ex.Id.Value,
                        ExerciseName = ex.Name,
                        SuggestedWeight = ex.Progression.SuggestedWeight.Value,
                        WeightUnit = ex.Progression.SuggestedWeight.Unit.ToString(),
                        ConfirmationType = ConfirmationType.WorkingWeight
                    });
                }
            }

            var weekProgressed = workout.CurrentWeek > weekBeforeComplete;
            var programComplete = workout.Status == WorkoutStatus.Completed;
            var isDeloadWeek = weekProgressed && workout.IsDeloadWeek();

            // This day's next occurrence is always in the following program week,
            // regardless of whether the current week has advanced yet (other days
            // of the week may still be outstanding).
            var nextSessionExercises = CalculateNextSessionPlan(workout, dayExercises.Values, weekBeforeComplete + 1);

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new CompleteDayResult
            {
                Day = request.Day,
                WeekNumber = weekBeforeComplete, // The week when the day was completed
                BlockNumber = workout.CurrentBlock,
                ExercisesCompleted = performances.Count,
                ProgressionChanges = progressionChanges,
                NewCurrentWeek = workout.CurrentWeek,
                NewCurrentDay = workout.CurrentDay,
                WeekProgressed = weekProgressed,
                ProgramComplete = programComplete,
                IsDeloadWeek = isDeloadWeek,
                ExercisesPendingWeightConfirmation = pendingWeightExercises,
                NextSessionExercises = nextSessionExercises
            });
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<CompleteDayResult>(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<CompleteDayResult>($"Failed to complete day: {ex.Message}");
        }
    }

    private static string GetProgressionChangeDescription(Exercise exercise, ExercisePerformance performance)
    {
        return exercise.Progression.GetProgressionChangeDescription(performance);
    }

    /// <summary>
    /// Computes the planned sets for the completed day's exercises at their next
    /// occurrence, using the post-progression state. Empty when the program has
    /// no week beyond the one just trained.
    /// </summary>
    private static List<NextSessionExerciseDto> CalculateNextSessionPlan(
        Workout workout,
        IEnumerable<Exercise> dayExercises,
        int nextWeek)
    {
        var nextSessionExercises = new List<NextSessionExerciseDto>();
        if (nextWeek > workout.TotalWeeks)
        {
            return nextSessionExercises;
        }

        var templateWeek = workout.GetTemplateWeek(nextWeek);
        var blockType = workout.GetBlockType(nextWeek);

        foreach (var exercise in dayExercises.OrderBy(e => e.OrderInDay))
        {
            var plannedSets = exercise.CalculatePlannedSets(templateWeek, blockType).ToList();
            if (plannedSets.Count == 0)
            {
                continue;
            }

            var firstSet = plannedSets[0];
            nextSessionExercises.Add(new NextSessionExerciseDto
            {
                ExerciseId = exercise.Id.Value,
                ExerciseName = exercise.Name,
                SetCount = plannedSets.Count,
                TargetReps = firstSet.TargetReps,
                Weight = firstSet.Weight.Value,
                WeightUnit = firstSet.Weight.Unit.ToString(),
                HasAmrap = plannedSets.Any(s => s.IsAmrap)
            });
        }

        return nextSessionExercises;
    }
}
