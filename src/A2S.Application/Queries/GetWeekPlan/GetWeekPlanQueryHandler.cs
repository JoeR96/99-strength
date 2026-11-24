using A2S.Application.Cardio;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
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
    private readonly ITrainingPlanRepository? _plans;
    private readonly ICardioTrackRepository? _tracks;
    private readonly ICardioProtocolRepository? _protocols;

    public GetWeekPlanQueryHandler(
        IWorkoutRepository workoutRepository,
        ICurrentUserService currentUserService,
        IA2SProgramProvider programProvider,
        ITrainingPlanRepository? plans = null,
        ICardioTrackRepository? tracks = null,
        ICardioProtocolRepository? protocols = null)
    {
        _plans = plans;
        _tracks = tracks;
        _protocols = protocols;
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _programProvider = programProvider ?? throw new ArgumentNullException(nameof(programProvider));
    }

    public async Task<Result<WeekPlanDto>> Handle(GetWeekPlanQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure<WeekPlanDto>("User must be authenticated.");
            }

            Workout? workout;
            if (request.WorkoutId.HasValue)
            {
                workout = await _workoutRepository.GetByIdAsync(new WorkoutId(request.WorkoutId.Value), cancellationToken);
            }
            else
            {
                workout = await _workoutRepository.GetActiveWorkoutAsync(userId.Value, cancellationToken);
            }

            if (workout == null)
            {
                return Result.Failure<WeekPlanDto>("Workout not found.");
            }

            if (request.WeekNumber < 1 || request.WeekNumber > workout.TotalWeeks)
            {
                return Result.Failure<WeekPlanDto>($"Week number must be between 1 and {workout.TotalWeeks}.");
            }

            var daysPerWeek = workout.GetDaysPerWeek();
            if (request.DayNumber < 1 || request.DayNumber > daysPerWeek)
            {
                return Result.Failure<WeekPlanDto>($"Day number must be between 1 and {daysPerWeek}.");
            }

            // Translate program week to template week using block sequence
            var templateWeek = workout.GetTemplateWeek(request.WeekNumber);
            var blockType = workout.GetBlockType(request.WeekNumber);

            var dayNumber = (DayNumber)request.DayNumber;
            var dayExercises = workout.Exercises
                .Where(e => e.AssignedDay == dayNumber)
                .OrderBy(e => e.OrderInDay)
                .ToList();

            var weekParams = _programProvider.GetWeekParameters(templateWeek);

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
                Exercises = plannedExercises,
                IsCurrent = workout.CurrentWeek == request.WeekNumber && workout.CurrentDay == request.DayNumber
            };

            return Result.Success(await WithSessionAsync(dto, workout, userId.Value, dayNumber, cancellationToken));
        }
        catch (Exception ex)
        {
            return Result.Failure<WeekPlanDto>($"Failed to get week plan: {ex.Message}");
        }
    }

    /// <summary>Adds the linked plan's slot for this A2S2 day: label, warm-up, paired routine and the cardio finisher item.</summary>
    private async Task<WeekPlanDto> WithSessionAsync(WeekPlanDto dto, Workout workout, UserId userId, DayNumber day, CancellationToken ct)
    {
        if (_plans is null)
        {
            return dto;
        }

        var plan = await _plans.GetActiveForUserAsync(userId, ct);
        if (plan is null || plan.LinkedWorkoutId != workout.Id)
        {
            return dto;
        }

        var slot = plan.Schedule.FirstOrDefault(s => s.Kind == SessionKind.Lift && s.LiftDay == day);
        if (slot is null)
        {
            return dto;
        }

        var paired = plan.Schedule.FirstOrDefault(s => s.Kind == SessionKind.Mobility && s.Day == slot.Day && s.MobilityRoutineId is not null);
        PlannedCardioDto? cardio = null;
        if (slot.Finisher is { } finisher && dto.WeekNumber >= workout.CurrentWeek)
        {
            var tracks = finisher.CardioTrackId is not null && _tracks is not null ? await _tracks.ListForUserAsync(userId, ct) : [];
            var protocols = finisher.CardioProtocolId is not null && _protocols is not null ? await _protocols.ListForUserAsync(userId, ct) : [];
            cardio = PlannedCardioBuilder.For(finisher, tracks, protocols, dto.Exercises.Count + 1);
        }

        return dto with
        {
            SessionLabel = slot.Label,
            WarmUpRoutineId = slot.WarmUpRoutineId?.Value,
            PairedRoutineId = paired?.MobilityRoutineId?.Value,
            Cardio = cardio
        };
    }

    private static PlannedExerciseDto MapExerciseToPlannedDto(
        Exercise exercise,
        int weekNumber,
        int blockNumber,
        WeekParameters weekParams)
    {
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

        var metadata = CreateMetadata(exercise, weekParams, weekNumber);

        return new PlannedExerciseDto
        {
            ExerciseId = exercise.Id.Value,
            Name = exercise.Name,
            Category = exercise.Category.ToString(),
            Equipment = exercise.Equipment.ToString(),
            OrderInDay = exercise.OrderInDay,
            ExternalTemplateId = exercise.ExternalTemplateId,
            ProgressionType = exercise.Progression.ProgressionType,
            RestSeconds = RestDefaults.For(exercise),
            PlannedSets = setDtos,
            Metadata = metadata,
            SupersetGroup = exercise.SupersetGroup,
            ExtraSetSide = exercise.ExtraSetSide.ToString(),
            CardioBlock = exercise.Progression.CalculateCardioBlock() is { } block ? ToDto(block) : null
        };
    }

    private static PlannedCardioBlockDto ToDto(PlannedCardioBlock block) => new(
        block.Lane.ToString(),
        block.StageName,
        block.StageNumber,
        block.StageCount,
        block.WarmUp is { } warmUp ? new TrackSegmentDto(warmUp.Minutes, warmUp.Level, null) : null,
        block.CoolDown is { } coolDown ? new TrackSegmentDto(coolDown.Minutes, coolDown.Level, null) : null,
        block.Kind.ToString(),
        block.WorkMinutes,
        block.Intervals is { } intervals ? new IntervalFormatDto(intervals.Rounds, intervals.HardMinutes, intervals.EasyMinutes) : null,
        block.RecoveryLevel,
        block.IntensityUnit.ToString(),
        block.Intensity,
        block.SuggestedIntensity,
        block.Speed,
        block.IsSettingsPending,
        block.TotalMinutes,
        block.Text);

    private static PlannedExerciseMetadataDto CreateMetadata(Exercise exercise, WeekParameters weekParams, int templateWeek)
    {
        var metadata = new PlannedExerciseMetadataDto();
        var progression = exercise.Progression;
        var data = progression.GetProgressionData();

        var tm = progression.GetTrainingMax();
        var currentWeight = progression.GetCurrentWeight();

        return progression.ProgressionType switch
        {
            "Linear" => metadata with
            {
                TrainingMaxValue = tm?.Value,
                TrainingMaxUnit = tm?.Unit.ToString(),
                IsTrainingMaxPending = exercise.IsTrainingMaxPending,
                Tier = (progression as LinearProgressionStrategy)?.Tier.ToString(),
                IntensityPercentage = A2SHypertrophyProgram.GetWeekData(templateWeek, (progression as LinearProgressionStrategy)?.Tier ?? ProgramTier.Primary).Intensity,
                Notes = exercise.IsTrainingMaxPending
                    ? "Set your training max"
                    : weekParams.IsDeload
                    ? $"DELOAD WEEK | TM: {tm}"
                    : $"TM: {tm} | Intensity: {weekParams.IntensityPercentage:0}%"
                        + (data.UseAmrap == true ? " | AMRAP on last set" : "")
            },
            "RepsPerSet" => metadata with
            {
                RepRangeMinimum = data.RepRangeMinimum,
                RepRangeMaximum = data.RepRangeMaximum,
                IsUnilateral = progression.IsUnilateral,
                IsWeightPending = progression.IsWeightPending,
                Notes = progression.IsWeightPending
                    ? "Weight pending - enter your working weight"
                    : progression.IsUnilateral
                        ? $"Unilateral: {data.CurrentSetCount} sets per side | Rep range: {data.RepRangeMinimum}-{data.RepRangeMaximum}"
                        : $"Rep range: {data.RepRangeMinimum}-{data.RepRangeMaximum}"
            },
            "MinimalSets" => metadata with
            {
                TargetTotalReps = data.TargetTotalReps,
                Notes = $"Target: {data.TargetTotalReps} total reps across {data.MinimumSets}-{data.MaximumSets} sets"
            },
            CardioStageStrategy.TypeName => metadata with
            {
                IsCardioSettingsPending = exercise.IsCardioSettingsPending,
                Notes = data.CardioPrescription
            },
            _ => metadata
        };
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
