using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Renames one of the signed-in user's plans. Another user's plan is not found.</summary>
public sealed record RenamePlanCommand(Guid PlanId, string Name) : ICommand<Result<TrainingPlanDto>>;

public sealed class RenamePlanCommandValidator : AbstractValidator<RenamePlanCommand>
{
    public RenamePlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Plan name is required.")
            .Must(n => n is null || n.Trim().Length <= TrainingPlan.MaximumNameLength)
            .WithMessage($"Plan name must be {TrainingPlan.MaximumNameLength} characters or fewer.");
    }
}

public sealed class RenamePlanCommandHandler : IRequestHandler<RenamePlanCommand, Result<TrainingPlanDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public RenamePlanCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<TrainingPlanDto>> Handle(RenamePlanCommand request, CancellationToken cancellationToken)
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
            load.Value.Rename(request.Name);
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<TrainingPlanDto>(ex);
        }

        _plans.Update(load.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(load.Value.ToDto());
    }
}
