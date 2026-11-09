using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Sets the weekly zone 4–5 minutes band the plan aims for (default 15–30).</summary>
public sealed record SetHighIntensityTargetCommand(Guid PlanId, int MinMinutes, int MaxMinutes) : ICommand<Result<HighIntensityTargetDto>>;

public sealed class SetHighIntensityTargetCommandValidator : AbstractValidator<SetHighIntensityTargetCommand>
{
    public SetHighIntensityTargetCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.MinMinutes).InclusiveBetween(0, HighIntensityTarget.MaximumMinutes);
        RuleFor(x => x.MaxMinutes).InclusiveBetween(0, HighIntensityTarget.MaximumMinutes)
            .GreaterThanOrEqualTo(x => x.MinMinutes).WithMessage("Maximum must be at least the minimum.");
    }
}

public sealed class SetHighIntensityTargetCommandHandler : IRequestHandler<SetHighIntensityTargetCommand, Result<HighIntensityTargetDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SetHighIntensityTargetCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<HighIntensityTargetDto>> Handle(SetHighIntensityTargetCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<HighIntensityTargetDto>(user);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<HighIntensityTargetDto>(load);
        }

        try
        {
            load.Value.SetHighIntensityTarget(new HighIntensityTarget(request.MinMinutes, request.MaxMinutes));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<HighIntensityTargetDto>(ex);
        }

        _plans.Update(load.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var target = load.Value.HighIntensityTarget;
        return Result.Success(new HighIntensityTargetDto(target.MinMinutes, target.MaxMinutes));
    }
}
