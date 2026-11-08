using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Spends a weekly allowance (flexible meal, alcohol) on a local date. Going over is allowed and reported.</summary>
public sealed record UseAllowanceCommand(Guid PlanId, DateOnly Date, AllowanceKind Kind) : ICommand<Result<AllowanceUseDto>>;

public sealed class UseAllowanceCommandValidator : AbstractValidator<UseAllowanceCommand>
{
    public UseAllowanceCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
    }
}

public sealed class UseAllowanceCommandHandler : IRequestHandler<UseAllowanceCommand, Result<AllowanceUseDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UseAllowanceCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<AllowanceUseDto>> Handle(UseAllowanceCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<AllowanceUseDto>(user);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<AllowanceUseDto>(load);
        }

        var use = load.Value.UseAllowance(request.Date, request.Kind);
        _plans.Update(load.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new AllowanceUseDto(use.Date, use.Kind));
    }
}
