using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Push;

/// <summary>The caller's push subscriptions (one per device), so Settings can show this device's nudge time.</summary>
public sealed record ListPushSubscriptionsQuery : IQuery<Result<IReadOnlyList<PushSubscriptionDto>>>;

public sealed class ListPushSubscriptionsQueryHandler : IRequestHandler<ListPushSubscriptionsQuery, Result<IReadOnlyList<PushSubscriptionDto>>>
{
    private readonly IPushSubscriptionRepository _pushes;
    private readonly ICurrentUserService _currentUser;

    public ListPushSubscriptionsQueryHandler(IPushSubscriptionRepository pushes, ICurrentUserService currentUser)
    {
        _pushes = pushes ?? throw new ArgumentNullException(nameof(pushes));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<IReadOnlyList<PushSubscriptionDto>>> Handle(ListPushSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<PushSubscriptionDto>>(user);
        }

        var subscriptions = await _pushes.ListForUserAsync(user.Value, cancellationToken);
        return Result.Success<IReadOnlyList<PushSubscriptionDto>>(subscriptions.Select(PushSubscriptionDto.From).ToList());
    }
}
