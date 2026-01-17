using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.GetWorkout;

/// <summary>
/// Handler for GetCurrentWorkoutQuery.
/// Retrieves the currently active workout.
/// </summary>
public sealed class GetCurrentWorkoutQueryHandler : IRequestHandler<GetCurrentWorkoutQuery, Result<WorkoutDto?>>
{
    private readonly IWorkoutRepository _workoutRepository;

    public GetCurrentWorkoutQueryHandler(IWorkoutRepository workoutRepository)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
    }

    public async Task<Result<WorkoutDto?>> Handle(GetCurrentWorkoutQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var workout = await _workoutRepository.GetActiveWorkoutAsync(cancellationToken);

            if (workout == null)
            {
                return Result.Success<WorkoutDto?>(null);
            }

            var dto = new WorkoutDto
            {
                Id = workout.Id.Value,
                Name = workout.Name,
                Variant = workout.Variant.ToString(),
                TotalWeeks = workout.TotalWeeks,
                CurrentWeek = workout.CurrentWeek,
                CurrentBlock = workout.CurrentBlock,
                Status = workout.Status.ToString(),
                CreatedAt = workout.CreatedAt,
                StartedAt = workout.StartedAt,
                CompletedAt = workout.CompletedAt,
                ExerciseCount = workout.Exercises.Count
            };

            return Result.Success<WorkoutDto?>(dto);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkoutDto?>($"Failed to retrieve current workout: {ex.Message}");
        }
    }
}
