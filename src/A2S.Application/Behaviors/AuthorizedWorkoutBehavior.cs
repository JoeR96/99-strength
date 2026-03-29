using A2S.Application.Common;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Behaviors;

/// <summary>
/// Pipeline behavior that automatically loads and authorizes workout access
/// for commands implementing IWorkoutCommand.
/// Eliminates copy-pasted auth/fetch/ownership boilerplate from handlers.
/// </summary>
public sealed class AuthorizedWorkoutBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IWorkoutCommand<TResponse>, IFailureFactory<TResponse>
    where TResponse : Result
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;

    public AuthorizedWorkoutBehavior(
        IWorkoutRepository workoutRepository,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        if (userId == null)
        {
            return CreateFailure("User must be authenticated.", ErrorCode.Unauthenticated);
        }

        var workout = await _workoutRepository.GetByIdAsync(
            new WorkoutId(request.WorkoutId),
            cancellationToken);

        if (workout == null)
        {
            return CreateFailure("Workout not found.", ErrorCode.NotFound);
        }

        if (workout.UserId != userId.Value)
        {
            return CreateFailure("You can only modify your own workouts.", ErrorCode.Unauthorized);
        }

        return await next();
    }

    private static TResponse CreateFailure(string error, ErrorCode errorCode)
    {
        return TRequest.CreateFailure(error, errorCode);
    }
}
