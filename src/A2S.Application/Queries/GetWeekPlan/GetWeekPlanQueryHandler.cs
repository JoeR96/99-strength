using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.GetWeekPlan;

/// <summary>
/// Handler for GetWeekPlanQuery.
/// Calculates planned sets using domain logic - this is the authoritative source.
/// </summary>
public sealed class GetWeekPlanQueryHandler : IRequestHandler<GetWeekPlanQuery, Result<WeekPlanDto>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IA2SProgramProvider _programProvider;

    public GetWeekPlanQueryHandler(
        IWorkoutRepository workoutRepository,
        ICurrentUserService currentUserService,
        IA2SProgramProvider programProvider)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _programProvider = programProvider ?? throw new ArgumentNullException(nameof(programProvider));
    }

    public async Task<Result<WeekPlanDto>> Handle(GetWeekPlanQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<WeekPlanDto>("User must be authenticated.");
            }

            // Get workout - either by ID or active workout
            Workout? workout;
            if (request.WorkoutId.HasValue)
            {
                workout = await _workoutRepository.GetByIdAsync(new WorkoutId(request.WorkoutId.Value), cancellationToken);
            }
            else
            {
                workout = await _workoutRepository.GetActiveWorkoutAsync(userId, cancellationToken);
            }

            if (workout == null)
            {
                return Result.Failure<WeekPlanDto>("Workout not found.");
            }

            // Validate week number
            if (request.WeekNumber < 1 || request.WeekNumber > workout.TotalWeeks)
            {
                return Result.Failure<WeekPlanDto>($"Week number must be between 1 and {workout.TotalWeeks}.");
            }

            // Validate day number
            var daysPerWeek = workout.GetDaysPerWeek();
            if (request.DayNumber < 1 || request.DayNumber > daysPerWeek)
            {
                return Result.Failure<WeekPlanDto>($"Day number must be between 1 and {daysPerWeek}.");
            }

            // Translate program week to template week using block sequence
            var templateWeek = workout.GetTemplateWeek(request.WeekNumber);
            var blockType = workout.GetBlockType(request.WeekNumber);

            // Get exercises for this day
            var dayNumber = (DayNumber)request.DayNumber;
            var dayExercises = workout.Exercises
                .Where(e => e.AssignedDay == dayNumber)
                .OrderBy(e => e.OrderInDay)
                .ToList();

            // Get week parameters from the program provider using template week
            var weekParams = _programProvider.GetWeekParameters(templateWeek);

            // Map exercises with their planned sets
            var plannedExercises = dayExercises.Select(exercise =>
                MapExerciseToPlannedDto(exercise, templateWeek, blockType, weekParams)
            ).ToList();

            var dto = new WeekPlanDto
            {
                WorkoutId = workout.Id.Value,
                WorkoutName = workout.Name,
                WeekNumber = request.WeekNumber,
                DayNumber = request.DayNumber,
                BlockNumber = blockType,
                IsDeloadWeek = weekParams.IsDeload,
                IntensityPercentage = weekParams.IntensityPercentage,
                Exercises = plannedExercises
            };

            return Result.Success(dto);
        }
        catch (Exception ex)
        {
            return Result.Failure<WeekPlanDto>($"Failed to get week plan: {ex.Message}");
        }
    }

    private static PlannedExerciseDto MapExerciseToPlannedDto(
        Exercise exercise,
        int weekNumber,
        int blockNumber,
        WeekParameters weekParams)
    {
        // Use domain method to calculate planned sets
        var plannedSets = exercise.CalculatePlannedSets(weekNumber, blockNumber).ToList();

        var setDtos = plannedSets.Select(ps => new PlannedSetDto
        {
            SetNumber = ps.SetNumber,
            WeightKg = RoundToGymIncrement(ps.Weight.ConvertTo(WeightUnit.Kilograms).Value, "Kilograms"),
            WeightLbs = RoundToGymIncrement(ps.Weight.ConvertTo(WeightUnit.Pounds).Value, "Pounds"),
            OriginalUnit = ps.Weight.Unit.ToString(),
            TargetReps = ps.TargetReps,
            IsAmrap = ps.IsAmrap
        }).ToList();

        var metadata = CreateMetadata(exercise, weekParams);

        return new PlannedExerciseDto
        {
            ExerciseId = exercise.Id.Value,
            Name = exercise.Name,
            Category = exercise.Category.ToString(),
            Equipment = exercise.Equipment.ToString(),
            OrderInDay = exercise.OrderInDay,
            HevyExerciseTemplateId = exercise.HevyExerciseTemplateId,
            ProgressionType = exercise.Progression.ProgressionType,
            PlannedSets = setDtos,
            Metadata = metadata
        };
    }

    private static PlannedExerciseMetadataDto CreateMetadata(Exercise exercise, WeekParameters weekParams)
    {
        var metadata = new PlannedExerciseMetadataDto();

        if (exercise.Progression is LinearProgressionStrategy linear)
        {
            return metadata with
            {
                TrainingMaxValue = linear.TrainingMax.Value,
                TrainingMaxUnit = linear.TrainingMax.Unit.ToString(),
                Notes = weekParams.IsDeload
                    ? $"DELOAD WEEK | TM: {linear.TrainingMax}"
                    : $"TM: {linear.TrainingMax} | Intensity: {weekParams.IntensityPercentage:0}%"
                        + (linear.UseAmrap ? " | AMRAP on last set" : "")
            };
        }
        else if (exercise.Progression is RepsPerSetStrategy reps)
        {
            var notes = reps.IsWeightPending
                ? "Weight pending - enter your working weight"
                : reps.IsUnilateral
                    ? $"Unilateral: {reps.CurrentSetCount} sets per side | Rep range: {reps.RepRange.Minimum}-{reps.RepRange.Maximum}"
                    : $"Rep range: {reps.RepRange.Minimum}-{reps.RepRange.Maximum}";
            return metadata with
            {
                RepRangeMinimum = reps.RepRange.Minimum,
                RepRangeTarget = reps.RepRange.Target,
                RepRangeMaximum = reps.RepRange.Maximum,
                IsUnilateral = reps.IsUnilateral,
                IsWeightPending = reps.IsWeightPending,
                Notes = notes
            };
        }
        else if (exercise.Progression is MinimalSetsStrategy minimal)
        {
            return metadata with
            {
                TargetTotalReps = minimal.TargetTotalReps,
                Notes = $"Target: {minimal.TargetTotalReps} total reps across {minimal.MinimumSets}-{minimal.MaximumSets} sets"
            };
        }

        return metadata;
    }

    /// <summary>
    /// Round weight to nearest gym increment (2.5kg or 5lbs).
    /// </summary>
    private static decimal RoundToGymIncrement(decimal weight, string unit)
    {
        var increment = unit == "Pounds" ? 5m : 2.5m;
        return Math.Round(weight / increment) * increment;
    }
}
