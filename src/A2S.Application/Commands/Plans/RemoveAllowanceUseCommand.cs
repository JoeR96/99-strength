using A2S.Application.Common;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Takes back one allowance use recorded on a date.</summary>
public sealed record RemoveAllowanceUseCommand(Guid PlanId, DateOnly Date, AllowanceKind Kind) : ICommand<Result<bool>>;

public sealed class RemoveAllowanceUseCommandValidator : AbstractValidator<RemoveAllowanceUseCommand>
{
    public RemoveAllowanceUseCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
    }
}

public sealed class RemoveAllowanceUseCommandHandler : IRequestHandler<RemoveAllowanceUseCommand, Result<bool>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public RemoveAllowanceUseCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(RemoveAllowanceUseCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<bool>(user);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<bool>(load);
        }

        if (!load.Value.RemoveAllowanceUse(request.Date, request.Kind))
        {
            return Result.Failure<bool>("No allowance use of that kind on that date.", ErrorCode.NotFound);
        }

        _plans.Update(load.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
