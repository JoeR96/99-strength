using A2S.Application.Common;
using A2S.Application.Sync;
using A2S.Domain.Aggregates.Sync;
using MediatR;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Behaviors;

/// <summary>
/// Bumps the caller's change stamp after a successful <see cref="ChangesSyncAreaAttribute"/> command and tells their
/// connected clients. A cross-cutting rule kept out of every handler: commands only declare which areas they
/// write.
/// </summary>
/// <remarks>
/// Why a pipeline behaviour keyed off a marker attribute rather than a domain event: half of the writes (habit
/// logs, plan completions, track logs) raise no domain events, and an event per aggregate would bump once per
/// aggregate saved rather than once per write. The bump runs right after the handler has committed (handlers
/// own their unit of work), as its own atomic upsert; a bump that fails is logged and never fails the write,
/// because the write is already saved and clients still catch up on focus or on the next bump. Nested commands
/// (gym finish → CompleteDay) bump once each, which only means one extra version.
/// </remarks>
public sealed class ChangeVersionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Read once per command type.
    private static readonly SyncArea Areas =
        typeof(TRequest).GetCustomAttributes(typeof(ChangesSyncAreaAttribute), inherit: false)
            .OfType<ChangesSyncAreaAttribute>().FirstOrDefault()?.Areas ?? SyncArea.None;

    private readonly IUserChangeLog _log;
    private readonly ISyncNotifier _notifier;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ChangeVersionBehavior<TRequest, TResponse>> _logger;

    public ChangeVersionBehavior(
        IUserChangeLog log,
        ISyncNotifier notifier,
        ICurrentUserService currentUser,
        ILogger<ChangeVersionBehavior<TRequest, TResponse>> logger)
    {
        _log = log;
        _notifier = notifier;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();
        var areas = Areas;
        if (areas == SyncArea.None)
        {
            return response;
        }

        if (response is Result { IsFailure: true })
        {
            return response;
        }

        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return response;
        }

        try
        {
            // Not the request's token: the write is saved, so a client hanging up must not skip the bump.
            var stamp = await _log.BumpAsync(userId, areas, CancellationToken.None);
            _notifier.Publish(userId, new SyncChange(stamp.Version, areas));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Change stamp bump failed after {Command}", typeof(TRequest).Name);
        }

        return response;
    }
}
