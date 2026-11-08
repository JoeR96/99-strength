using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Plans;

/// <summary>The current user's mobility routines.</summary>
public sealed record ListMobilityRoutinesQuery : IQuery<Result<IReadOnlyList<MobilityRoutineDto>>>;

public sealed class ListMobilityRoutinesQueryHandler : IRequestHandler<ListMobilityRoutinesQuery, Result<IReadOnlyList<MobilityRoutineDto>>>
{
    private readonly IMobilityRoutineRepository _routines;
    private readonly ICurrentUserService _currentUser;

    public ListMobilityRoutinesQueryHandler(IMobilityRoutineRepository routines, ICurrentUserService currentUser)
    {
        _routines = routines;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<MobilityRoutineDto>>> Handle(ListMobilityRoutinesQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<MobilityRoutineDto>>(user);
        }

        var routines = await _routines.ListForUserAsync(user.Value, cancellationToken);
        return Result.Success<IReadOnlyList<MobilityRoutineDto>>(routines.Select(r => r.ToDto()).ToList());
    }
}
