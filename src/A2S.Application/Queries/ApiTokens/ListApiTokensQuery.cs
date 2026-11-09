using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.ApiTokens;

/// <summary>The caller's personal API tokens, revoked ones included, newest first. No secrets.</summary>
public sealed record ListApiTokensQuery : IQuery<Result<IReadOnlyList<ApiTokenDto>>>;

public sealed class ListApiTokensQueryHandler : IRequestHandler<ListApiTokensQuery, Result<IReadOnlyList<ApiTokenDto>>>
{
    private readonly IApiTokenRepository _tokens;
    private readonly ICurrentUserService _currentUser;

    public ListApiTokensQueryHandler(IApiTokenRepository tokens, ICurrentUserService currentUser)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<IReadOnlyList<ApiTokenDto>>> Handle(ListApiTokensQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<ApiTokenDto>>(user);
        }

        var tokens = await _tokens.ListForUserAsync(user.Value, cancellationToken);
        return Result.Success<IReadOnlyList<ApiTokenDto>>(tokens.Select(ApiTokenDto.From).ToList());
    }
}
