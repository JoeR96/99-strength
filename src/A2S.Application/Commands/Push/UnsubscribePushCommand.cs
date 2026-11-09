using A2S.Application.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Push;

/// <summary>Forgets the calling browser's subscription. Idempotent: an unknown endpoint is a success.</summary>
public sealed record UnsubscribePushCommand(string Endpoint) : ICommand<Result>;

public sealed class UnsubscribePushCommandValidator : AbstractValidator<UnsubscribePushCommand>
{
    public UnsubscribePushCommandValidator()
    {
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(2048);
    }
}

public sealed class UnsubscribePushCommandHandler : IRequestHandler<UnsubscribePushCommand, Result>
{
    private readonly IPushSubscriptionRepository _pushes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UnsubscribePushCommandHandler(IPushSubscriptionRepository pushes, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _pushes = pushes ?? throw new ArgumentNullException(nameof(pushes));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(UnsubscribePushCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return Result.Failure(user.Error, user.ErrorCode);
        }

        var existing = await _pushes.GetByEndpointAsync(user.Value, request.Endpoint.Trim(), cancellationToken);
        if (existing is null)
        {
            return Result.Success();
        }

        _pushes.Remove(existing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
