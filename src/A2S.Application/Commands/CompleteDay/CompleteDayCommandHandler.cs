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
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<CompleteDayResult>("User must be authenticated.");
            }

            // Get the workout
            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure<CompleteDayResult>("Workout not found.");
            }

            // Verify the workout belongs to the current user
            if (workout.UserId != userId)
            {
                return Result.Failure<CompleteDayResult>("You can only complete days in your own workouts.");
            }

            // Verify the workout is active
            if (workout.Status != WorkoutStatus.Active)
            {
                return Result.Failure<CompleteDayResult>("Workout must be active to complete a day.");
            }

            // Get exercises for this day to calculate planned sets
            var dayExercises = workout.Exercises
                .Where(e => e.AssignedDay == request.Day)
                .ToDictionary(e => e.Id.Value, e => e);

            if (!dayExercises.Any())
            {
                return Result.Failure<CompleteDayResult>($"No exercises assigned to {request.Day}.");
            }

            // Build exercise performances from request data
            var performances = new List<ExercisePerformance>();
            var progressionChanges = new List<ProgressionChangeDto>();

            foreach (var performanceRequest in request.Performances)
            {
                if (!dayExercises.TryGetValue(performanceRequest.ExerciseId, out var exercise))
                {
                    return Result.Failure<CompleteDayResult>(
                        $"Exercise {performanceRequest.ExerciseId} not found or not assigned to {request.Day}.");
                }

                // Get planned sets for this exercise
                var plannedSets = exercise.CalculatePlannedSets(workout.CurrentWeek, workout.CurrentBlock).ToList();

                // Convert request data to domain value objects
                var completedSets = performanceRequest.CompletedSets
                    .Select(s => new CompletedSet(
                        s.SetNumber,
                        Weight.Create(s.Weight, s.WeightUnit),
                        s.ActualReps,
                        s.WasAmrap))
                    .ToList();

                // Create the performance record
                var performance = new ExercisePerformance(
                    exercise.Id,
                    plannedSets,
                    completedSets,
                    skipProgression: performanceRequest.WasTemporarySubstitution);

                performances.Add(performance);

                // Track progression changes for the response
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

            // Capture the week/day before completing
            var weekBeforeComplete = workout.CurrentWeek;

            // Complete the day - this applies progression rules and auto-progresses
            workout.CompleteDay(request.Day, performances);

            // Check if week progressed or program completed
            var weekProgressed = workout.CurrentWeek > weekBeforeComplete;
            var programComplete = workout.Status == WorkoutStatus.Completed;
            var isDeloadWeek = weekProgressed && workout.IsDeloadWeek();

            // Save changes
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
                IsDeloadWeek = isDeloadWeek
            });
        }
        catch (InvalidOperationException ex)
        {
            // Domain rule violations throw InvalidOperationException
            return Result.Failure<CompleteDayResult>(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<CompleteDayResult>($"Failed to complete day: {ex.Message}");
        }
    }

    private static string GetProgressionChangeDescription(Exercise exercise, ExercisePerformance performance)
    {
        // This is a simplified description - the actual change is computed when ApplyProgression is called
        // We can enhance this later to capture the actual before/after state

        if (exercise.Progression is LinearProgressionStrategy)
        {
            var amrapDelta = performance.GetAmrapDelta();
            return amrapDelta switch
            {
                >= 5 => "TM increased 3%",
                4 => "TM increased 2%",
                3 => "TM increased 1.5%",
                2 => "TM increased 1%",
                1 => "TM increased 0.5%",
                0 => "No change",
                -1 => "TM decreased 2%",
                _ => "TM decreased 5%"
            };
        }

        if (exercise.Progression is RepsPerSetStrategy repsStrategy)
        {
            var repRange = repsStrategy.RepRange;
            if (performance.AllSetsHitMax(repRange))
            {
                return repsStrategy.CurrentSetCount < Math.Min(repsStrategy.TargetSets, repsStrategy.MaxSets)
                    ? "Added 1 set"
                    : "Weight increased, sets reset";
            }
            if (performance.AnySetsBelowMin(repRange))
            {
                return repsStrategy.CurrentSetCount > 1
                    ? "Removed 1 set"
                    : "Weight decreased";
            }
            return "No change";
        }

        if (exercise.Progression is MinimalSetsStrategy minimalSetsStrategy)
        {
            var totalReps = performance.GetTotalRepsCompleted();
            var setsUsed = performance.GetSetsUsed();

            if (totalReps < minimalSetsStrategy.TargetTotalReps)
            {
                return "Added 1 set (did not hit target reps)";
            }
            if (setsUsed < minimalSetsStrategy.CurrentSetCount)
            {
                return "Reduced sets (completed in fewer sets)";
            }
            return "No change";
        }

        return "Progression applied";
    }
}
