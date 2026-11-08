using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Hevy;

/// <summary>
/// Pushes one scheduled session (the plan's session of that kind on that date) to Hevy as a routine.
/// Idempotent: a second push updates the same routine.
/// </summary>
public sealed record PushSessionRoutineCommand(Guid PlanId, DateOnly Date, SessionKind Kind, DayNumber? LiftDay = null)
    : ICommand<Result<PushRoutineResult>>;

public sealed class PushSessionRoutineCommandValidator : AbstractValidator<PushSessionRoutineCommand>
{
    public PushSessionRoutineCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum().NotEqual(SessionKind.Rest).WithMessage("A rest day has nothing to push to Hevy.");
        RuleFor(x => x.LiftDay).NotNull().When(x => x.Kind == SessionKind.Lift).WithMessage("A lift push needs the A2S2 day.");
    }
}

public sealed class PushSessionRoutineCommandHandler : IRequestHandler<PushSessionRoutineCommand, Result<PushRoutineResult>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly ICurrentUserService _currentUser;
    private readonly IHevyKeyResolver _keys;
    private readonly HevyRoutinePusher _pusher;

    public PushSessionRoutineCommandHandler(
        ITrainingPlanRepository plans,
        ICurrentUserService currentUser,
        IHevyKeyResolver keys,
        HevyRoutinePusher pusher)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _pusher = pusher ?? throw new ArgumentNullException(nameof(pusher));
    }

    public async Task<Result<PushRoutineResult>> Handle(PushSessionRoutineCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<PushRoutineResult>(user);
        }

        var apiKey = await _keys.ForUserAsync(user.Value, cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            return Result.Failure<PushRoutineResult>(HevyErrors.KeyRequired, ErrorCode.ValidationFailed);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<PushRoutineResult>(load);
        }

        var session = load.Value.SessionsOn(request.Date)
            .FirstOrDefault(s => s.Kind == request.Kind && (request.Kind != SessionKind.Lift || s.LiftDay == request.LiftDay));
        if (session is null)
        {
            return Result.Failure<PushRoutineResult>(
                $"No {request.Kind} session is scheduled on {request.Date:yyyy-MM-dd}.", ErrorCode.NotFound);
        }

        return await _pusher.PushAsync(apiKey, load.Value, session, request.Date, cancellationToken);
    }
}
