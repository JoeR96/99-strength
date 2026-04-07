using A2S.Application.Common;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.SimulateWorkout;

/// <summary>
/// Handler for SimulateWorkoutQuery.
/// Loads the workout, runs the domain simulator, and returns projected progression data.
/// </summary>
public sealed class SimulateWorkoutQueryHandler : IRequestHandler<SimulateWorkoutQuery, Result<SimulationResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;

    public SimulateWorkoutQueryHandler(
        IWorkoutRepository workoutRepository,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<SimulationResult>> Handle(SimulateWorkoutQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        if (userId == null)
        {
            return Result.Failure<SimulationResult>("User must be authenticated.");
        }

        var workoutId = new WorkoutId(request.WorkoutId);
        var workout = await _workoutRepository.GetByIdAsync(workoutId, cancellationToken);

        if (workout == null)
        {
            return Result.Failure<SimulationResult>("Workout not found.", ErrorCode.NotFound);
        }

        if (workout.UserId != userId.Value)
        {
            return Result.Failure<SimulationResult>("Access denied.", ErrorCode.Unauthorized);
        }

        var result = WorkoutSimulator.Simulate(workout, request.SessionCount);

        return Result.Success(result);
    }
}
