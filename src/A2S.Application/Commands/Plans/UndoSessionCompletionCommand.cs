using A2S.Application.Common;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Removes a recorded session completion.</summary>
public sealed record UndoSessionCompletionCommand(Guid PlanId, Guid CompletionId) : ICommand<Result<bool>>;

public sealed class UndoSessionCompletionCommandHandler : IRequestHandler<UndoSessionCompletionCommand, Result<bool>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UndoSessionCompletionCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UndoSessionCompletionCommand request, CancellationToken cancellationToken)
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

        if (!load.Value.UndoCompletion(new SessionCompletionId(request.CompletionId)))
        {
            return Result.Failure<bool>("Completion not found.", ErrorCode.NotFound);
        }

        _plans.Update(load.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
