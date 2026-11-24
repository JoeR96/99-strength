using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Queries.GetWorkout;
using A2S.Application.Sync;
using A2S.Domain.Aggregates.Sync;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.CardioSettings;

/// <summary>
/// Sets a cardio exercise's level/incline and/or speed (null leaves a value as it is): the values gym mode asks
/// for before the first session, and any later edit. Each change is a progression history entry, and the other
/// exercises on the same lane get it too.
/// </summary>
[ChangesSyncArea(SyncArea.Workout)]
public sealed record SetCardioSettingsCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    decimal? Intensity,
    decimal? Speed) : ICommand<Result<ExerciseDto>>;

public sealed class SetCardioSettingsCommandValidator : AbstractValidator<SetCardioSettingsCommand>
{
    public SetCardioSettingsCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.Intensity.HasValue || x.Speed.HasValue)
            .WithMessage("Enter a level/incline or a speed.");
        RuleFor(x => x.Intensity).GreaterThan(0m).LessThanOrEqualTo(100m).When(x => x.Intensity.HasValue);
        RuleFor(x => x.Speed).InclusiveBetween(0.5m, 25m).When(x => x.Speed.HasValue);
    }
}

public sealed class SetCardioSettingsCommandHandler : IRequestHandler<SetCardioSettingsCommand, Result<ExerciseDto>>
{
    private readonly IWorkoutRepository _workouts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SetCardioSettingsCommandHandler(IWorkoutRepository workouts, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<ExerciseDto>> Handle(SetCardioSettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (userId is null)
        {
            return Result.Failure<ExerciseDto>("User must be authenticated.", ErrorCode.Unauthorized);
        }

        var workout = await _workouts.GetByIdAsync(new WorkoutId(request.WorkoutId), cancellationToken);
        if (workout is null || workout.UserId != userId.Value)
        {
            return Result.Failure<ExerciseDto>("Workout not found.", ErrorCode.NotFound);
        }

        var exerciseId = new ExerciseId(request.ExerciseId);
        var exercise = workout.GetExerciseById(exerciseId);
        if (exercise?.Progression is not CardioStageStrategy)
        {
            return Result.Failure<ExerciseDto>("Only cardio exercises have cardio settings.", ErrorCode.NotFound);
        }

        try
        {
            workout.SetCardioSettings(exerciseId, request.Intensity, request.Speed);
            _workouts.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(GetCurrentWorkoutQueryHandler.MapExerciseToDto(exercise, workout));
        }
        catch (DomainException ex)
        {
            return Result.Failure<ExerciseDto>(ex.Message, ErrorCode.DomainRuleViolation);
        }
    }
}
