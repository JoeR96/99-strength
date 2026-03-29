using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.UpdateBlockSequence;

public sealed class UpdateBlockSequenceCommandHandler
    : IRequestHandler<UpdateBlockSequenceCommand, Result<UpdateBlockSequenceResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateBlockSequenceCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<UpdateBlockSequenceResult>> Handle(
        UpdateBlockSequenceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure<UpdateBlockSequenceResult>("User must be authenticated.");
            }

            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure<UpdateBlockSequenceResult>("Workout not found.");
            }

            if (workout.UserId != userId.Value)
            {
                return Result.Failure<UpdateBlockSequenceResult>("You can only modify your own workouts.");
            }

            workout.UpdateBlockSequence(request.BlockSequence);

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new UpdateBlockSequenceResult
            {
                WorkoutId = workout.Id.Value,
                BlockSequence = workout.BlockSequence.ToList(),
                TotalWeeks = workout.TotalWeeks,
                CurrentWeek = workout.CurrentWeek,
                CurrentBlock = workout.CurrentBlock
            });
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<UpdateBlockSequenceResult>(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<UpdateBlockSequenceResult>($"Failed to update block sequence: {ex.Message}");
        }
    }
}
