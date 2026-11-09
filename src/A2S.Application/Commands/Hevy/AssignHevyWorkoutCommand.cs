using System.Text.Json;
using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Hevy;

/// <summary>
/// Places an inbox workout by hand: completes the session of that kind (and lift day) on that date as
/// the pull would have. <paramref name="Extra"/> records the plan completion only, leaving A2S2
/// progression alone (an additional session, not the programmed one).
/// </summary>
public sealed record AssignHevyWorkoutCommand(string HevyWorkoutId, DateOnly Date, SessionKind Kind, DayNumber? LiftDay = null, bool Extra = false)
    : ICommand<Result<AssignHevyWorkoutResult>>;

public sealed class AssignHevyWorkoutCommandValidator : AbstractValidator<AssignHevyWorkoutCommand>
{
    public AssignHevyWorkoutCommandValidator()
    {
        RuleFor(x => x.HevyWorkoutId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Kind).IsInEnum().NotEqual(SessionKind.Rest).WithMessage("A rest day is not completed.");
        RuleFor(x => x.LiftDay).NotNull().When(x => x.Kind == SessionKind.Lift).WithMessage("A lift completion needs the A2S2 day it trained.");
    }
}

public sealed class AssignHevyWorkoutCommandHandler : IRequestHandler<AssignHevyWorkoutCommand, Result<AssignHevyWorkoutResult>>
{
    private readonly IHevyWorkoutSnapshotRepository _snapshots;
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly HevyTemplateCatalogue _catalogue;
    private readonly HevySessionCompleter _completer;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalClock _clock;

    public AssignHevyWorkoutCommandHandler(
        IHevyWorkoutSnapshotRepository snapshots,
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        HevyTemplateCatalogue catalogue,
        HevySessionCompleter completer,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILocalClock clock)
    {
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        _completer = completer ?? throw new ArgumentNullException(nameof(completer));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<AssignHevyWorkoutResult>> Handle(AssignHevyWorkoutCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<AssignHevyWorkoutResult>(user);
        }

        var snapshot = await _snapshots.GetByIdAsync(new HevyWorkoutId(request.HevyWorkoutId), cancellationToken);
        if (snapshot is null || snapshot.UserId != user.Value)
        {
            return Result.Failure<AssignHevyWorkoutResult>("Hevy workout not found.", ErrorCode.NotFound);
        }

        if (snapshot.Status == SnapshotStatus.Matched)
        {
            return Result.Failure<AssignHevyWorkoutResult>("That workout already completed a session; undo the completion first.", ErrorCode.Conflict);
        }

        var load = await PlanAccess.LoadActiveAsync(_plans, user.Value, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<AssignHevyWorkoutResult>(load);
        }

        var plan = load.Value;
        HevyWorkout? workout;
        try
        {
            workout = JsonSerializer.Deserialize<HevyWorkout>(snapshot.RawJson);
        }
        catch (JsonException)
        {
            workout = null;
        }

        if (workout is null)
        {
            return Result.Failure<AssignHevyWorkoutResult>("The stored workout could not be read.", ErrorCode.DomainRuleViolation);
        }

        var session = SessionFor(plan, request);
        var program = plan.LinkedWorkoutId is { } workoutId ? await _workouts.GetByIdAsync(workoutId, cancellationToken) : null;
        var templateLinks = await _catalogue.TemplateIdsAsync(user.Value, cancellationToken);

        var completion = await _completer.CompleteAsync(plan, program, session, request.Date, workout, templateLinks, request.Extra, cancellationToken);
        if (completion.IsFailure)
        {
            return PlanAccess.Fail<AssignHevyWorkoutResult>(completion);
        }

        snapshot.MarkMatched(completion.Value, _clock.NowUtc());
        _snapshots.Update(snapshot);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AssignHevyWorkoutResult(snapshot.Id.Value, completion.Value.Value, request.Date, request.Kind, request.LiftDay));
    }

    /// <summary>
    /// The scheduled session the person means: the one of that kind on that date, else the same slot
    /// elsewhere in the week (a Lower A done on Saturday), else a bare session of that kind.
    /// </summary>
    private static ScheduledSession SessionFor(TrainingPlan plan, AssignHevyWorkoutCommand request)
    {
        bool Fits(ScheduledSession s) => s.Kind == request.Kind && (request.Kind != SessionKind.Lift || s.LiftDay == request.LiftDay);

        return plan.SessionsOn(request.Date).FirstOrDefault(Fits)
            ?? plan.Schedule.FirstOrDefault(Fits)
            ?? new ScheduledSession(request.Date.DayOfWeek, request.Kind, request.LiftDay, null, null, request.Kind.ToString());
    }
}
