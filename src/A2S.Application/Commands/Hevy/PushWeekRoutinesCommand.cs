using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Hevy;

/// <summary>One session the week push could not send, with the user-facing reason.</summary>
public sealed record PushRoutineFailure(DateOnly Date, string Label, string Error);

/// <summary>
/// One session the week push sent: the day it is scheduled for and its label, then what Hevy now holds
/// (the same three fields as <see cref="PushRoutineResult"/>), so the result can be read day by day.
/// </summary>
public sealed record PushedSession(DateOnly Date, string Label, string RoutineId, string Title, string Outcome);

/// <summary>Everything the week push did. Paused days are skipped; failures do not stop the rest.</summary>
public sealed record PushWeekResult(DateOnly WeekStart, IReadOnlyList<PushedSession> Pushed, IReadOnlyList<PushRoutineFailure> Failed);

/// <summary>Pushes every non-rest session of the Monday–Sunday week containing <paramref name="Date"/>.</summary>
public sealed record PushWeekRoutinesCommand(Guid PlanId, DateOnly Date) : ICommand<Result<PushWeekResult>>;

public sealed class PushWeekRoutinesCommandValidator : AbstractValidator<PushWeekRoutinesCommand>
{
    public PushWeekRoutinesCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
    }
}

public sealed class PushWeekRoutinesCommandHandler : IRequestHandler<PushWeekRoutinesCommand, Result<PushWeekResult>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly ICurrentUserService _currentUser;
    private readonly IHevyKeyResolver _keys;
    private readonly HevyRoutinePusher _pusher;

    public PushWeekRoutinesCommandHandler(
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

    public async Task<Result<PushWeekResult>> Handle(PushWeekRoutinesCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<PushWeekResult>(user);
        }

        var apiKey = await _keys.ForUserAsync(user.Value, cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            return Result.Failure<PushWeekResult>(HevyErrors.KeyRequired, ErrorCode.ValidationFailed);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<PushWeekResult>(load);
        }

        var plan = load.Value;
        var monday = LocalDates.StartOfWeek(request.Date);
        var pushed = new List<PushedSession>();
        var failed = new List<PushRoutineFailure>();

        for (var offset = 0; offset < 7; offset++)
        {
            var date = monday.AddDays(offset);
            if (plan.IsPaused(date))
            {
                continue;
            }

            foreach (var session in plan.SessionsOn(date).Where(s => s.Kind != SessionKind.Rest))
            {
                var result = await _pusher.PushAsync(apiKey, plan, session, date, cancellationToken);
                if (result.IsSuccess)
                {
                    pushed.Add(new PushedSession(date, session.Label, result.Value.RoutineId, result.Value.Title, result.Value.Outcome));
                }
                else
                {
                    failed.Add(new PushRoutineFailure(date, session.Label, result.Error));
                }
            }
        }

        return Result.Success(new PushWeekResult(monday, pushed, failed));
    }
}
