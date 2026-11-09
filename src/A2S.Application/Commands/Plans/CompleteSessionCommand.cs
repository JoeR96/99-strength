using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>
/// Records a session as done on a local date. With a Hevy workout id the source is Hevy and the
/// call is idempotent on that id; otherwise it is a manual completion, idempotent on the slot.
/// </summary>
public sealed record CompleteSessionCommand(
    Guid PlanId,
    DateOnly Date,
    SessionKind Kind,
    DayNumber? LiftDay = null,
    Guid? CardioProtocolId = null,
    Guid? MobilityRoutineId = null,
    CardioLogDto? Cardio = null,
    IReadOnlyList<bool>? DrillTicks = null,
    string? HevyWorkoutId = null) : ICommand<Result<SessionCompletionDto>>;

public sealed class CompleteSessionCommandValidator : AbstractValidator<CompleteSessionCommand>
{
    public CompleteSessionCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.LiftDay).NotNull().When(x => x.Kind == SessionKind.Lift)
            .WithMessage("A lift completion needs the A2S2 day it trained.");
        RuleFor(x => x.HevyWorkoutId).MaximumLength(100);
        When(x => x.Cardio is not null, () =>
        {
            RuleFor(x => x.Cardio!.DurationSeconds).GreaterThan(0);
            RuleFor(x => x.Cardio!.Rpe).InclusiveBetween(1, 10).When(x => x.Cardio!.Rpe.HasValue);
            RuleFor(x => x.Cardio!.AvgHeartRate).InclusiveBetween(30, 250).When(x => x.Cardio!.AvgHeartRate.HasValue);
            RuleFor(x => x.Cardio!.HighIntensityMinutes).InclusiveBetween(0, 600).When(x => x.Cardio!.HighIntensityMinutes.HasValue);
        });
    }
}

public sealed class CompleteSessionCommandHandler : IRequestHandler<CompleteSessionCommand, Result<SessionCompletionDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;
    private readonly ICardioProtocolRepository _protocols;

    public CompleteSessionCommandHandler(
        ITrainingPlanRepository plans,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock,
        ICardioProtocolRepository protocols)
    {
        _protocols = protocols ?? throw new ArgumentNullException(nameof(protocols));
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<SessionCompletionDto>> Handle(CompleteSessionCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<SessionCompletionDto>(user);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<SessionCompletionDto>(load);
        }

        var source = string.IsNullOrWhiteSpace(request.HevyWorkoutId) ? CompletionSource.Manual : CompletionSource.Hevy;

        var log = request.Cardio?.ToDomain();
        if (request.Kind == SessionKind.Cardio && request.CardioProtocolId is { } protocolId
            && (log is null || log.HighIntensityMinutes is null))
        {
            log = await WithProtocolDefaultsAsync(log, new CardioProtocolId(protocolId), cancellationToken);
        }

        try
        {
            var completion = load.Value.CompleteSession(
                request.Date,
                request.Kind,
                source,
                liftDay: request.LiftDay,
                cardio: request.CardioProtocolId is { } c ? new CardioProtocolId(c) : null,
                mobility: request.MobilityRoutineId is { } m ? new MobilityRoutineId(m) : null,
                hevyWorkoutId: request.HevyWorkoutId,
                log: log,
                drillTicks: request.DrillTicks,
                recordedAt: _clock.NowUtc());

            _plans.Update(load.Value);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(completion.ToDto());
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<SessionCompletionDto>(ex);
        }
    }

    /// <summary>
    /// "Mark done" without a watch reading still credits the protocol's expected zone 4–5 minutes
    /// (16 for a Norwegian 4×4); a session with no log at all gets the protocol's length too.
    /// </summary>
    private async Task<CardioLog?> WithProtocolDefaultsAsync(CardioLog? log, CardioProtocolId protocolId, CancellationToken ct)
    {
        var protocol = await _protocols.GetByIdAsync(protocolId, ct);
        if (protocol is null)
        {
            return log;
        }

        var defaultMinutes = protocol.DefaultHighIntensityMinutes;
        if (log is null)
        {
            return defaultMinutes is null
                ? null
                : new CardioLog(protocol.TotalMinutes * 60, null, null, null, null, defaultMinutes);
        }

        return defaultMinutes is null
            ? log
            : new CardioLog(log.DurationSeconds, log.AvgHeartRate, log.Rpe, log.DistanceMeters, log.Notes,
                Math.Min(defaultMinutes.Value, (log.DurationSeconds + 59) / 60));
    }
}
