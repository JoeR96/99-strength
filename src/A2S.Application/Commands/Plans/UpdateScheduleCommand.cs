using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Replaces the plan's weekly schedule template.</summary>
public sealed record UpdateScheduleCommand(Guid PlanId, IReadOnlyList<ScheduledSessionDto> Schedule) : ICommand<Result<TrainingPlanDto>>;

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

public sealed class UpdateScheduleCommandHandler : IRequestHandler<UpdateScheduleCommand, Result<TrainingPlanDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateScheduleCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<TrainingPlanDto>> Handle(UpdateScheduleCommand request, CancellationToken cancellationToken)
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
            load.Value.ReplaceSchedule(request.Schedule.Select(s => s.ToDomain()));
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
