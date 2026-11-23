using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.AddExercise;

public sealed class AddExerciseCommandHandler : IRequestHandler<AddExerciseCommand, Result<AddExerciseResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IExerciseLibraryProvider _library;

    public AddExerciseCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IExerciseLibraryProvider library)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    public async Task<Result<AddExerciseResult>> Handle(AddExerciseCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        if (userId == null)
        {
            return Result.Failure<AddExerciseResult>("User must be authenticated.");
        }

        var workout = await _workoutRepository.GetByIdAsync(new WorkoutId(request.WorkoutId), cancellationToken);
        if (workout == null)
        {
            return Result.Failure<AddExerciseResult>("Workout not found.");
        }

        if (workout.UserId != userId.Value)
        {
            return Result.Failure<AddExerciseResult>("You can only modify your own workouts.");
        }

        if (_library.GetByName(request.Exercise.TemplateName) is null)
        {
            return Result.Failure<AddExerciseResult>(
                $"Exercise template '{request.Exercise.TemplateName}' was not found in the exercise library.");
        }

        try
        {
            var exercise = ConfiguredExerciseFactory.Create(_library, [request.Exercise]).Single();
            workout.InsertExercise(exercise);

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new AddExerciseResult(
                exercise.Id.Value, exercise.Name, (int)exercise.AssignedDay, exercise.OrderInDay));
        }
        catch (Exception ex) when (ex is BusinessRuleViolationException or ArgumentException or InvalidOperationException)
        {
            return Result.Failure<AddExerciseResult>($"Failed to add exercise: {ex.Message}");
        }
    }
}
