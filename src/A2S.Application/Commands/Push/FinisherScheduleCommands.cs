using A2S.Application.Common;
using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Push;

/// <summary>One phase change the phone wants announced: absolute UTC time and the text.</summary>
public sealed record FinisherNudgeInput(DateTimeOffset At, string Body);

/// <summary>What the server holds for a finisher wizard (echoed back after a replace).</summary>
public sealed record FinisherScheduleDto(string ClientSessionId, int Day, int Revision, int Pending, DateTimeOffset? NextDueAtUtc)
{
    public static FinisherScheduleDto From(FinisherSchedule s) =>
        new(s.ClientSessionId, s.Day, s.Revision, s.Nudges.Count(n => n.SentAtUtc is null), s.NextDueAtUtc);
}

/// <summary>
/// Stores (or replaces) the phase-change nudges of one live finisher wizard. Posted on Start and again
/// on every pause, resume, skip or ±30 s; the old schedule is discarded, so nothing stale is sent.
/// </summary>
public sealed record ReplaceFinisherScheduleCommand(string ClientSessionId, int Day, string? Title, IReadOnlyList<FinisherNudgeInput> Nudges)
    : ICommand<Result<FinisherScheduleDto>>;

public sealed class ReplaceFinisherScheduleCommandValidator : AbstractValidator<ReplaceFinisherScheduleCommand>
{
    public ReplaceFinisherScheduleCommandValidator()
    {
        RuleFor(x => x.ClientSessionId).NotEmpty().MaximumLength(FinisherSchedule.MaxClientSessionIdLength);
        RuleFor(x => x.Day).InclusiveBetween(1, 7);
        RuleFor(x => x.Title).MaximumLength(FinisherSchedule.MaxTextLength);
        RuleFor(x => x.Nudges).NotNull();
        RuleFor(x => x.Nudges.Count).LessThanOrEqualTo(FinisherSchedule.MaxNudges).When(x => x.Nudges is not null);
        RuleForEach(x => x.Nudges).ChildRules(n => n.RuleFor(i => i.Body).NotEmpty().MaximumLength(FinisherSchedule.MaxTextLength));
    }
}

public sealed class ReplaceFinisherScheduleCommandHandler : IRequestHandler<ReplaceFinisherScheduleCommand, Result<FinisherScheduleDto>>
{
    private readonly IFinisherScheduleRepository _schedules;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public ReplaceFinisherScheduleCommandHandler(IFinisherScheduleRepository schedules, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<FinisherScheduleDto>> Handle(ReplaceFinisherScheduleCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<FinisherScheduleDto>(user);
        }

        try
        {
            var nudges = request.Nudges.Select(n => new FinisherNudge(n.At, n.Body));
            var now = _clock.NowUtc();
            var existing = await _schedules.GetAsync(user.Value, request.ClientSessionId.Trim(), cancellationToken);
            FinisherSchedule schedule;
            if (existing is null)
            {
                schedule = FinisherSchedule.Create(user.Value, request.ClientSessionId, request.Day, request.Title, nudges, now);
                await _schedules.AddAsync(schedule, cancellationToken);
            }
            else
            {
                schedule = existing;
                schedule.Replace(request.Day, request.Title, nudges, now);
                _schedules.Update(schedule);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(FinisherScheduleDto.From(schedule));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<FinisherScheduleDto>(ex);
        }
    }
}

/// <summary>Finish or abandon: forget the schedule so nothing more is sent. Idempotent.</summary>
public sealed record DeleteFinisherScheduleCommand(string ClientSessionId) : ICommand<Result>;

public sealed class DeleteFinisherScheduleCommandValidator : AbstractValidator<DeleteFinisherScheduleCommand>
{
    public DeleteFinisherScheduleCommandValidator()
    {
        RuleFor(x => x.ClientSessionId).NotEmpty().MaximumLength(FinisherSchedule.MaxClientSessionIdLength);
    }
}

public sealed class DeleteFinisherScheduleCommandHandler : IRequestHandler<DeleteFinisherScheduleCommand, Result>
{
    private readonly IFinisherScheduleRepository _schedules;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public DeleteFinisherScheduleCommandHandler(IFinisherScheduleRepository schedules, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(DeleteFinisherScheduleCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return Result.Failure(user.Error, user.ErrorCode);
        }

        var existing = await _schedules.GetAsync(user.Value, request.ClientSessionId.Trim(), cancellationToken);
        if (existing is null)
        {
            return Result.Success();
        }

        _schedules.Remove(existing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
