using A2S.Application.Common;
using MediatR;

namespace A2S.Application.Sync;

/// <summary>The caller's change stamp (version and per-area versions).</summary>
public sealed record GetSyncVersionQuery : IQuery<Result<SyncVersionDto>>;

public sealed class GetSyncVersionQueryHandler : IRequestHandler<GetSyncVersionQuery, Result<SyncVersionDto>>
{
    private readonly IUserChangeLog _log;
    private readonly ICurrentUserService _currentUser;

    public GetSyncVersionQueryHandler(IUserChangeLog log, ICurrentUserService currentUser)
    {
        _log = log;
        _currentUser = currentUser;
    }

    public async Task<Result<SyncVersionDto>> Handle(GetSyncVersionQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Result.Failure<SyncVersionDto>("Not signed in.", ErrorCode.Unauthenticated);
        }

        return Result.Success(SyncVersionDto.From(await _log.GetAsync(_currentUser.UserId, cancellationToken)));
    }
}
