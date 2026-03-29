using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Commands.ConfirmWorkingWeight;

public sealed class ConfirmWorkingWeightCommandHandler : IRequestHandler<ConfirmWorkingWeightCommand, Result>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmWorkingWeightCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result> Handle(ConfirmWorkingWeightCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // AuthorizedWorkoutBehavior guarantees the workout exists and is owned by the current user
            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            var exerciseId = new ExerciseId(request.ExerciseId);
            var weight = Weight.Create(request.Weight, request.Unit);
            workout!.ConfirmExerciseWorkingWeight(exerciseId, weight);

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
