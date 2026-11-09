using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Plans;

/// <summary>The current user's cardio protocols.</summary>
public sealed record ListCardioProtocolsQuery : IQuery<Result<IReadOnlyList<CardioProtocolDto>>>;

public sealed class ListCardioProtocolsQueryHandler : IRequestHandler<ListCardioProtocolsQuery, Result<IReadOnlyList<CardioProtocolDto>>>
{
    private readonly ICardioProtocolRepository _protocols;
    private readonly ICurrentUserService _currentUser;

    public ListCardioProtocolsQueryHandler(ICardioProtocolRepository protocols, ICurrentUserService currentUser)
    {
        _protocols = protocols;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<CardioProtocolDto>>> Handle(ListCardioProtocolsQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<CardioProtocolDto>>(user);
        }

        var protocols = await _protocols.ListForUserAsync(user.Value, cancellationToken);
        return Result.Success<IReadOnlyList<CardioProtocolDto>>(protocols.Select(p => p.ToDto()).ToList());
    }
}
