using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Plans;

/// <summary>The current user's active training plan, with its completions.</summary>
public sealed record GetActivePlanQuery : IQuery<Result<TrainingPlanDto>>;

public sealed class GetActivePlanQueryHandler : IRequestHandler<GetActivePlanQuery, Result<TrainingPlanDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly ICurrentUserService _currentUser;

    public GetActivePlanQueryHandler(ITrainingPlanRepository plans, ICurrentUserService currentUser)
    {
        _plans = plans;
        _currentUser = currentUser;
    }

    public async Task<Result<TrainingPlanDto>> Handle(GetActivePlanQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<TrainingPlanDto>(user);
        }

        var load = await PlanAccess.LoadActiveAsync(_plans, user.Value, cancellationToken);
        return load.IsFailure ? PlanAccess.Fail<TrainingPlanDto>(load) : Result.Success(load.Value.ToDto());
    }
}
