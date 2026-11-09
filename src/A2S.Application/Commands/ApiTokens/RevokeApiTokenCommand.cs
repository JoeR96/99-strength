using A2S.Application.Common;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.ApiTokens;

/// <summary>
/// Revokes one of the caller's tokens; it stops authenticating at once. Revoking an already revoked
/// token succeeds and keeps the first revocation time. Someone else's token is reported as not found.
/// </summary>
public sealed record RevokeApiTokenCommand(Guid Id) : ICommand<Result>;

public sealed class RevokeApiTokenCommandHandler : IRequestHandler<RevokeApiTokenCommand, Result>
{
    private readonly IApiTokenRepository _tokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public RevokeApiTokenCommandHandler(
        IApiTokenRepository tokens,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result> Handle(RevokeApiTokenCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return Result.Failure(user.Error, user.ErrorCode);
        }

        var token = await _tokens.GetByIdAsync(new ApiTokenId(request.Id), cancellationToken);
        if (token is null || token.UserId != user.Value)
        {
            return Result.Failure("Token not found.", ErrorCode.NotFound);
        }

        if (token.Revoke(_clock.NowUtc()))
        {
            _tokens.Update(token);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
