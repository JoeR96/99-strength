using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Sets the label, the free-text changes and the exercise swaps of the phase starting on <see cref="From"/>.</summary>
public sealed record SetPhaseChangesCommand(
    Guid PlanId,
    DateOnly From,
    string? Label,
    IReadOnlyList<string> Changes,
    IReadOnlyList<PhaseSwapDto>? Swaps) : ICommand<Result<TrainingPlanDto>>;

public sealed class SetPhaseChangesCommandValidator : AbstractValidator<SetPhaseChangesCommand>
{
    public SetPhaseChangesCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Label).MaximumLength(50);
        RuleFor(x => x.Changes).NotNull().Must(c => c.Count <= PlanPhase.MaxChanges);
        RuleForEach(x => x.Changes).MaximumLength(200);
        RuleFor(x => x.Swaps).Must(s => s is null || s.Count <= PlanPhase.MaxChanges);
    }
}

public sealed class SetPhaseChangesCommandHandler : IRequestHandler<SetPhaseChangesCommand, Result<TrainingPlanDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SetPhaseChangesCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<TrainingPlanDto>> Handle(SetPhaseChangesCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<TrainingPlanDto>(user);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<TrainingPlanDto>(load);
        }

        try
        {
            load.Value.SetPhaseChanges(request.From, request.Label, request.Changes,
                (request.Swaps ?? []).Select(s => new PhaseSwap(s.FromExercise, s.ToExercise)).ToList());
            _plans.Update(load.Value);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(load.Value.ToDto());
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<TrainingPlanDto>(ex);
        }
    }
}

/// <summary>
/// A swap Today can offer at the phase start. <see cref="ExerciseId"/> is the exercise in the linked workout to replace
/// (via the existing permanent SubstituteExercise); null when it isn't there. <see cref="Done"/> when the workout already
/// has the replacement and not the original.
/// </summary>
public sealed record PhaseSwapActionDto(string FromExercise, string ToExercise, Guid? WorkoutId, Guid? ExerciseId, bool Done);

/// <summary>The heads-up for a phase that starts within a week (or today), with its changes and swaps.</summary>
public sealed record PhaseHeadsUpDto(
    PhaseType Type,
    string? Label,
    DateOnly From,
    DateOnly To,
    int DaysUntil,
    IReadOnlyList<string> Changes,
    IReadOnlyList<PhaseSwapActionDto> Swaps);

/// <summary>Today's phase heads-up, or null outside every heads-up window.</summary>
public sealed record GetPhaseHeadsUpQuery(DateOnly? Date = null) : IQuery<Result<PhaseHeadsUpDto?>>;

public sealed class GetPhaseHeadsUpQueryHandler : IRequestHandler<GetPhaseHeadsUpQuery, Result<PhaseHeadsUpDto?>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public GetPhaseHeadsUpQueryHandler(ITrainingPlanRepository plans, IWorkoutRepository workouts, ICurrentUserService currentUser, ILocalClock clock)
    {
        _plans = plans;
        _workouts = workouts;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<PhaseHeadsUpDto?>> Handle(GetPhaseHeadsUpQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<PhaseHeadsUpDto?>(user);
        }

        var date = request.Date ?? _clock.Today();
        var plan = await _plans.GetActiveForUserAsync(user.Value, cancellationToken);
        var phase = plan is null ? null : PlanPhase.HeadsUpOn(plan.Phases, date);
        if (phase is null)
        {
            return Result.Success<PhaseHeadsUpDto?>(null);
        }

        var workout = plan!.LinkedWorkoutId is { } workoutId ? await _workouts.GetByIdAsync(workoutId, cancellationToken) : null;
        var swaps = phase.Swaps.Select(s =>
        {
            var original = workout?.Exercises.FirstOrDefault(e => string.Equals(e.Name, s.FromExercise, StringComparison.OrdinalIgnoreCase));
            var replaced = workout?.Exercises.Any(e => string.Equals(e.Name, s.ToExercise, StringComparison.OrdinalIgnoreCase)) ?? false;
            return new PhaseSwapActionDto(s.FromExercise, s.ToExercise, workout?.Id.Value, original?.Id.Value, original is null && replaced);
        }).ToList();

        return Result.Success<PhaseHeadsUpDto?>(new PhaseHeadsUpDto(
            phase.Type, phase.Label, phase.From, phase.To, phase.From.DayNumber - date.DayNumber, phase.Changes, swaps));
    }
}
