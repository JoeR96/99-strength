using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Push;

public sealed class SubscribePushCommandHandler : IRequestHandler<SubscribePushCommand, Result<PushSubscriptionDto>>
{
    private readonly IPushSubscriptionRepository _pushes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public SubscribePushCommandHandler(
        IPushSubscriptionRepository pushes,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock)
    {
        _pushes = pushes ?? throw new ArgumentNullException(nameof(pushes));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<PushSubscriptionDto>> Handle(SubscribePushCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<PushSubscriptionDto>(user);
        }

        try
        {
            var existing = await _pushes.GetByEndpointAsync(user.Value, request.Endpoint.Trim(), cancellationToken);
            PushSubscription subscription;
            if (existing is null)
            {
                subscription = PushSubscription.Create(
                    user.Value,
                    request.Endpoint,
                    request.P256dh,
                    request.Auth,
                    request.UserAgent,
                    _clock.NowUtc(),
                    request.SessionStartTime);
                await _pushes.AddAsync(subscription, cancellationToken);
            }
            else
            {
                subscription = existing;
                subscription.RefreshKeys(request.P256dh, request.Auth, request.UserAgent);
                if (request.SessionStartTime is { } time)
                {
                    subscription.SetSessionStartTime(time);
                }

                _pushes.Update(subscription);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(PushSubscriptionDto.From(subscription));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<PushSubscriptionDto>(ex);
        }
    }
}
