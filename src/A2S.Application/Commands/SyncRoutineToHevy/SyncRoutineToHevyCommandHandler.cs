using A2S.Application.Common;
using A2S.Application.Interfaces;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Commands.SyncRoutineToHevy;

/// <summary>
/// Handler for SyncRoutineToHevyCommand.
/// Maps domain entities to Hevy DTOs and delegates to the integration service.
/// </summary>
public sealed class SyncRoutineToHevyCommandHandler
    : IRequestHandler<SyncRoutineToHevyCommand, Result<SyncRoutineResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IHevyIntegrationService _hevyService;
    private readonly IA2SProgramProvider _programProvider;
    private readonly ILogger<SyncRoutineToHevyCommandHandler> _logger;

    public SyncRoutineToHevyCommandHandler(
        IWorkoutRepository workoutRepository,
        IHevyIntegrationService hevyService,
        IA2SProgramProvider programProvider,
        ILogger<SyncRoutineToHevyCommandHandler> logger)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _hevyService = hevyService ?? throw new ArgumentNullException(nameof(hevyService));
        _programProvider = programProvider ?? throw new ArgumentNullException(nameof(programProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<SyncRoutineResult>> Handle(
        SyncRoutineToHevyCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // AuthorizedWorkoutBehavior guarantees the workout exists and is owned by the current user
            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (request.WeekNumber < 1 || request.WeekNumber > workout!.TotalWeeks)
            {
                return Result.Failure<SyncRoutineResult>(
                    $"Week number must be between 1 and {workout.TotalWeeks}.");
            }

            var daysPerWeek = workout.GetDaysPerWeek();
            if (request.DayNumber < 1 || request.DayNumber > daysPerWeek)
            {
                return Result.Failure<SyncRoutineResult>(
                    $"Day number must be between 1 and {daysPerWeek}.");
            }

            var weekParams = _programProvider.GetWeekParameters(request.WeekNumber);

            var dayExercises = workout.Exercises
                .Where(e => (int)e.AssignedDay == request.DayNumber)
                .OrderBy(e => e.OrderInDay)
                .Select(e =>
                {
                    var plannedSets = e.CalculatePlannedSets(request.WeekNumber, weekParams.BlockNumber).ToList();
                    return new HevySyncExerciseInfo
                    {
                        ExternalTemplateId = e.ExternalTemplateId,
                        ProgressionType = e.Progression.ProgressionType,
                        Notes = BuildExerciseNotes(e, weekParams, plannedSets),
                        PlannedSets = plannedSets.Select(s => new HevySyncSetInfo
                        {
                            WeightKg = s.Weight.ConvertTo(WeightUnit.Kilograms).Value,
                            TargetReps = s.TargetReps,
                            IsAmrap = s.IsAmrap
                        }).ToList()
                    };
                })
                .ToList();

            var syncRequest = new HevySyncRoutineRequest
            {
                WorkoutName = workout.Name,
                WeekNumber = request.WeekNumber,
                DayNumber = request.DayNumber,
                BlockNumber = weekParams.BlockNumber,
                IsDeload = weekParams.IsDeload,
                IntensityPercentage = weekParams.IntensityPercentage,
                Exercises = dayExercises
            };

            var result = await _hevyService.SyncRoutineForDayAsync(
                syncRequest, request.HevyApiKey, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to sync routine: {Error}", result.ErrorMessage);
                return Result.Success(new SyncRoutineResult
                {
                    Success = false,
                    RoutineId = null,
                    RoutineTitle = null,
                    AlreadyExists = false,
                    Message = result.ErrorMessage
                });
            }

            _logger.LogInformation(
                "Synced routine for workout {WorkoutId}, week {Week}, day {Day}: {RoutineId}",
                request.WorkoutId, request.WeekNumber, request.DayNumber, result.RoutineId);

            return Result.Success(new SyncRoutineResult
            {
                Success = true,
                RoutineId = result.RoutineId,
                RoutineTitle = result.RoutineTitle,
                AlreadyExists = result.AlreadyExists,
                Message = result.AlreadyExists
                    ? $"Routine '{result.RoutineTitle}' already exists in Hevy"
                    : $"Routine '{result.RoutineTitle}' created in Hevy"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync routine for workout {WorkoutId}", request.WorkoutId);
            return Result.Failure<SyncRoutineResult>($"Failed to sync routine: {ex.Message}");
        }
    }

    private static string BuildExerciseNotes(
        Domain.Aggregates.Workout.Exercise exercise,
        WeekParameters weekParams,
        IReadOnlyList<Domain.ValueObjects.PlannedSet> plannedSets)
    {
        var data = exercise.Progression.GetProgressionData();
        var trainingMax = exercise.Progression.GetTrainingMax();
        var currentWeight = exercise.Progression.GetCurrentWeight();
        var noteParts = new List<string>();

        if (trainingMax != null)
        {
            // Linear progression
            noteParts.Add($"TM: {trainingMax}");
            if (weekParams.IsDeload)
            {
                noteParts.Add("DELOAD");
            }
            else
            {
                var blockPhase = weekParams.BlockNumber switch
                {
                    1 => BlockDescriptions.Volume,
                    2 => BlockDescriptions.Intensity,
                    3 => BlockDescriptions.Peaking,
                    _ => ""
                };
                noteParts.Add($"Block {weekParams.BlockNumber} - {blockPhase}");
            }
            noteParts.Add($"{weekParams.IntensityPercentage:0}% × {weekParams.Sets} sets × {weekParams.TargetReps} reps");
            if (data.UseAmrap == true)
            {
                noteParts.Add($"AMRAP last set (target: {plannedSets.LastOrDefault()?.TargetReps ?? 0}+)");
            }
        }
        else if (data.RepRangeMinimum.HasValue && data.RepRangeMaximum.HasValue)
        {
            // RepsPerSet progression
            var effectiveMaxSets = Math.Min(data.TargetSets ?? 3, data.CurrentSetCount ?? 3);
            noteParts.Add($"Sets: {data.CurrentSetCount}/{data.TargetSets}");
            noteParts.Add($"Rep range: {data.RepRangeMinimum}-{data.RepRangeMaximum} (hit {data.RepRangeMaximum} to progress)");
            if (exercise.Progression.IsUnilateral)
            {
                noteParts.Add("Unilateral");
            }
        }
        else if (data.TargetTotalReps.HasValue)
        {
            // MinimalSets progression
            noteParts.Add($"Target: {data.TargetTotalReps} reps in {data.CurrentSetCount} sets (range: {data.MinimumSets}-{data.MaximumSets}) | Complete in fewer sets to progress");
        }

        return string.Join(" | ", noteParts);
    }

    private static class BlockDescriptions
    {
        public const string Volume = "Volume phase";
        public const string Intensity = "Intensity phase";
        public const string Peaking = "Peaking phase";
    }
}
