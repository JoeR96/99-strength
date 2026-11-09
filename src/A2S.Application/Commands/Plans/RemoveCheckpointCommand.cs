using A2S.Application.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Removes the checkpoint on the given date.</summary>
public sealed record RemoveCheckpointCommand(Guid PlanId, DateOnly Date) : ICommand<Result<bool>>;

public sealed class RemoveCheckpointCommandHandler : IRequestHandler<RemoveCheckpointCommand, Result<bool>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public RemoveCheckpointCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(RemoveCheckpointCommand request, CancellationToken cancellationToken)
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

        if (!load.Value.RemoveCheckpoint(request.Date))
        {
            return Result.Failure<bool>("No checkpoint on that date.", ErrorCode.NotFound);
        }

        _plans.Update(load.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
