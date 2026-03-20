using A2S.Application.Common;
using A2S.Application.Interfaces;
using A2S.Application.Services;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Commands.SyncRoutineToHevy;

/// <summary>
/// Handler for SyncRoutineToHevyCommand.
/// Orchestrates the sync using the domain service.
/// </summary>
public sealed class SyncRoutineToHevyCommandHandler
    : IRequestHandler<SyncRoutineToHevyCommand, Result<SyncRoutineResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IHevyIntegrationService _hevyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SyncRoutineToHevyCommandHandler> _logger;

    public SyncRoutineToHevyCommandHandler(
        IWorkoutRepository workoutRepository,
        IHevyIntegrationService hevyService,
        IUnitOfWork unitOfWork,
        ILogger<SyncRoutineToHevyCommandHandler> logger)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _hevyService = hevyService ?? throw new ArgumentNullException(nameof(hevyService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<SyncRoutineResult>> Handle(
        SyncRoutineToHevyCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get workout
            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure<SyncRoutineResult>("Workout not found.");
            }

            // Validate week/day
            if (request.WeekNumber < 1 || request.WeekNumber > workout.TotalWeeks)
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

            // Get or create folder if needed
            if (string.IsNullOrEmpty(workout.HevyRoutineFolderId))
            {
                var folderId = await _hevyService.GetOrCreateRoutineFolderAsync(
                    workout.Name,
                    request.HevyApiKey,
                    cancellationToken);

                if (!string.IsNullOrEmpty(folderId))
                {
                    workout.SetHevyRoutineFolderId(folderId);
                    _workoutRepository.Update(workout);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }

            // Sync routine using domain service
            var result = await _hevyService.SyncRoutineForDayAsync(
                workout,
                request.WeekNumber,
                request.DayNumber,
                request.HevyApiKey,
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to sync routine: {Error}", result.ErrorMessage);
                return Result.Success(new SyncRoutineResult
                {
                    Success = false,
                    Message = result.ErrorMessage
                });
            }

            // Save synced routine ID to workout
            if (!string.IsNullOrEmpty(result.RoutineId))
            {
                workout.SetHevySyncedRoutine(request.WeekNumber, request.DayNumber, result.RoutineId);
                _workoutRepository.Update(workout);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
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
            _logger.LogError(ex, "Error syncing routine for workout {WorkoutId}", request.WorkoutId);
            return Result.Failure<SyncRoutineResult>($"Failed to sync routine: {ex.Message}");
        }
    }
}
