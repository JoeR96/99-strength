using A2S.Application.Commands.Hevy;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Hevy;

/// <summary>
/// One tick of the Hevy poll (the hosted service calls <see cref="RunOnceAsync"/> every 15 minutes
/// inside a scope). For every user with a stored key whose active plan has a session today, pulls
/// their events as that user (<see cref="AmbientUser"/>). Users without a key are never listed, a
/// user without a plan or a session today is skipped, and only counts are logged.
/// </summary>
public sealed class HevyPollRunner
{
    private readonly IUserRepository _users;
    private readonly ITrainingPlanRepository _plans;
    private readonly ISender _sender;
    private readonly AmbientUser _ambient;
    private readonly ILocalClock _clock;
    private readonly ILogger<HevyPollRunner> _logger;

    public HevyPollRunner(
        IUserRepository users,
        ITrainingPlanRepository plans,
        ISender sender,
        AmbientUser ambient,
        ILocalClock clock,
        ILogger<HevyPollRunner> logger)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _ambient = ambient ?? throw new ArgumentNullException(nameof(ambient));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Pulls for every due user; returns how many users were pulled.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var today = _clock.Today();
        var pulled = 0;

        foreach (var userId in await _users.ListIdsWithHevyKeyAsync(ct))
        {
            ct.ThrowIfCancellationRequested();

            var plan = await _plans.GetActiveForUserAsync(userId, ct);
            if (plan is null || plan.IsPaused(today) || !plan.SessionsOn(today).Any(s => s.Kind != SessionKind.Rest))
            {
                continue;
            }

            _ambient.UserId = userId.Value;
            try
            {
                var result = await _sender.Send(new PullHevyEventsCommand(), ct);
                if (result.IsSuccess)
                {
                    var s = result.Value;
                    _logger.LogInformation("Hevy poll: {Pulled} event(s), {Matched} matched, {Unmatched} unmatched, {Skipped} skipped{Reason}",
                        s.Pulled, s.Matched, s.Unmatched, s.Skipped, s.SkippedReason is null ? string.Empty : $" ({s.SkippedReason})");
                }
                else
                {
                    _logger.LogWarning("Hevy poll failed for a user: {Error}", result.Error);
                }

                pulled++;
            }
            finally
            {
                _ambient.UserId = null;
            }
        }

        return pulled;
    }
}
