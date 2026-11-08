using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>
/// Replaces the plan's weekly schedule template. Soft rules (brief § 5: no 4×4 the day before Lower A/B)
/// come back as warnings on the result; they never block the change.
/// </summary>
public sealed record UpdateScheduleCommand(Guid PlanId, IReadOnlyList<ScheduledSessionDto> Schedule) : ICommand<Result<UpdateScheduleResult>>;

public sealed class UpdateScheduleCommandValidator : AbstractValidator<UpdateScheduleCommand>
{
    public UpdateScheduleCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Schedule).NotEmpty();
        RuleForEach(x => x.Schedule).ChildRules(session =>
        {
            session.RuleFor(s => s.Label).NotEmpty().MaximumLength(100);
            session.RuleFor(s => s.Kind).IsInEnum();
            session.RuleFor(s => s.Day).IsInEnum();
        });
    }
}

public sealed class UpdateScheduleCommandHandler : IRequestHandler<UpdateScheduleCommand, Result<UpdateScheduleResult>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly ICardioProtocolRepository _protocols;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateScheduleCommandHandler(
        ITrainingPlanRepository plans,
        ICardioProtocolRepository protocols,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _plans = plans;
        _protocols = protocols;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<UpdateScheduleResult>> Handle(UpdateScheduleCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<UpdateScheduleResult>(user);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<UpdateScheduleResult>(load);
        }

        try
        {
            var plan = load.Value;
            plan.ReplaceSchedule(request.Schedule.Select(s => s.ToDomain()));
            _plans.Update(plan);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var protocols = await _protocols.ListForUserAsync(user.Value, cancellationToken);
            var warnings = PlanScheduleRules.Warnings(
                plan.Schedule,
                id => protocols.FirstOrDefault(p => p.Id == id)?.Kind);

            return Result.Success(new UpdateScheduleResult(
                plan.ToDto(),
                warnings.Select(w => new ScheduleWarningDto(w.Code, w.Message)).ToList()));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<UpdateScheduleResult>(ex);
        }
    }
}
