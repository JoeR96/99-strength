using A2S.Application.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Removes the phase that starts on the given date.</summary>
public sealed record RemovePhaseCommand(Guid PlanId, DateOnly From) : ICommand<Result<bool>>;

public sealed class RemovePhaseCommandHandler : IRequestHandler<RemovePhaseCommand, Result<bool>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public RemovePhaseCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(RemovePhaseCommand request, CancellationToken cancellationToken)
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

        if (!load.Value.RemovePhase(request.From))
        {
            return Result.Failure<bool>("No phase starts on that date.", ErrorCode.NotFound);
        }

        _plans.Update(load.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
