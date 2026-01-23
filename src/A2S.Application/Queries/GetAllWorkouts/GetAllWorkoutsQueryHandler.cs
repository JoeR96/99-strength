using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.GetAllWorkouts;

/// <summary>
/// Handler for GetAllWorkoutsQuery.
/// Retrieves all workouts for the current user.
/// </summary>
public sealed class GetAllWorkoutsQueryHandler : IRequestHandler<GetAllWorkoutsQuery, Result<IReadOnlyList<WorkoutSummaryDto>>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetAllWorkoutsQueryHandler(
        IWorkoutRepository workoutRepository,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<IReadOnlyList<WorkoutSummaryDto>>> Handle(
        GetAllWorkoutsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<IReadOnlyList<WorkoutSummaryDto>>("User must be authenticated.");
            }

            var workouts = await _workoutRepository.GetAllByUserAsync(userId, cancellationToken);

            var summaries = workouts.Select(w => new WorkoutSummaryDto
            {
                Id = w.Id.Value,
                Name = w.Name,
                Variant = w.Variant.ToString(),
                TotalWeeks = w.TotalWeeks,
                CurrentWeek = w.CurrentWeek,
                CurrentBlock = w.CurrentBlock,
                Status = w.Status.ToString(),
                CreatedAt = w.CreatedAt,
                StartedAt = w.StartedAt,
                CompletedAt = w.CompletedAt,
                ExerciseCount = w.Exercises.Count,
                IsActive = w.Status == WorkoutStatus.Active
            }).ToList();

            return Result.Success<IReadOnlyList<WorkoutSummaryDto>>(summaries);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<WorkoutSummaryDto>>($"Failed to retrieve workouts: {ex.Message}");
        }
    }
}
