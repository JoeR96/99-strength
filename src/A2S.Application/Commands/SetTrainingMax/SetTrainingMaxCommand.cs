using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.SetTrainingMax;

/// <summary>
/// "Set your training max" for an A2S2 exercise: either the TM itself, or a recent set (weight × reps)
/// that <see cref="TrainingMaxEstimator"/> turns into one. Clears a pending TM. TM adjustment maths are
/// untouched: this is the same manual set as UpdateExercises.
/// </summary>
public sealed record SetTrainingMaxCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    decimal? TrainingMax,
    decimal? RecentWeight,
    int? RecentReps,
    WeightUnit Unit = WeightUnit.Kilograms) : ICommand<Result<SetTrainingMaxResult>>;

public sealed record SetTrainingMaxResult(Guid ExerciseId, decimal TrainingMax, string Unit, bool Estimated);

public sealed class SetTrainingMaxCommandValidator : AbstractValidator<SetTrainingMaxCommand>
{
    public SetTrainingMaxCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.Unit).IsInEnum();
        RuleFor(x => x)
            .Must(x => x.TrainingMax.HasValue != (x.RecentWeight.HasValue || x.RecentReps.HasValue))
            .WithMessage("Enter either a training max or a recent set (weight and reps).");
        RuleFor(x => x.TrainingMax).InclusiveBetween(1m, 1000m).When(x => x.TrainingMax.HasValue);
        When(x => !x.TrainingMax.HasValue, () =>
        {
            RuleFor(x => x.RecentWeight).NotNull().InclusiveBetween(1m, 1000m);
            RuleFor(x => x.RecentReps).NotNull().InclusiveBetween(1, TrainingMaxEstimator.MaximumReps);
        });
    }
}

public sealed class SetTrainingMaxCommandHandler : IRequestHandler<SetTrainingMaxCommand, Result<SetTrainingMaxResult>>
{
    private readonly IWorkoutRepository _workouts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SetTrainingMaxCommandHandler(IWorkoutRepository workouts, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<SetTrainingMaxResult>> Handle(SetTrainingMaxCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (userId is null)
        {
            return Result.Failure<SetTrainingMaxResult>("User must be authenticated.", ErrorCode.Unauthorized);
        }

        var workout = await _workouts.GetByIdAsync(new WorkoutId(request.WorkoutId), cancellationToken);
        if (workout is null || workout.UserId != userId.Value)
        {
            return Result.Failure<SetTrainingMaxResult>("Workout not found.", ErrorCode.NotFound);
        }

        var exerciseId = new ExerciseId(request.ExerciseId);
        var exercise = workout.GetExerciseById(exerciseId);
        if (exercise?.Progression is not LinearProgressionStrategy)
        {
            return Result.Failure<SetTrainingMaxResult>("Only A2S2 exercises have a training max.", ErrorCode.NotFound);
        }

        try
        {
            var estimated = !request.TrainingMax.HasValue;
            var tm = estimated
                ? TrainingMaxEstimator.FromRecentSet(Weight.Create(request.RecentWeight!.Value, request.Unit), request.RecentReps!.Value)
                : TrainingMax.Create(request.TrainingMax!.Value, request.Unit);

            workout.AdjustTrainingMax(exerciseId, tm, estimated ? "Estimated from a recent set" : "Training max entered");
            _workouts.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new SetTrainingMaxResult(request.ExerciseId, tm.Value, tm.Unit.ToString(), estimated));
        }
        catch (DomainException ex)
        {
            return Result.Failure<SetTrainingMaxResult>(ex.Message, ErrorCode.DomainRuleViolation);
        }
    }
}
