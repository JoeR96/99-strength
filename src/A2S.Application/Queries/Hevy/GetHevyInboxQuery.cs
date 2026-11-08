using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Hevy;

/// <summary>The current user's pulled Hevy workouts that no session fitted, newest first.</summary>
public sealed record GetHevyInboxQuery : IQuery<Result<IReadOnlyList<HevyInboxItemDto>>>;

public sealed class GetHevyInboxQueryHandler : IRequestHandler<GetHevyInboxQuery, Result<IReadOnlyList<HevyInboxItemDto>>>
{
    private readonly IHevyWorkoutSnapshotRepository _snapshots;
    private readonly ICurrentUserService _currentUser;

    public GetHevyInboxQueryHandler(IHevyWorkoutSnapshotRepository snapshots, ICurrentUserService currentUser)
    {
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<IReadOnlyList<HevyInboxItemDto>>> Handle(GetHevyInboxQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<HevyInboxItemDto>>(user);
        }

        var unmatched = await _snapshots.ListAsync(user.Value, SnapshotStatus.Unmatched, cancellationToken);
        return Result.Success<IReadOnlyList<HevyInboxItemDto>>(unmatched.Select(HevyInboxItemDto.From).ToList());
    }
}
