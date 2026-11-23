using A2S.Application.Common;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace A2S.Application.Push;

/// <summary>
/// One tick of the finisher nudge loop (every few seconds): for each live finisher schedule with a due
/// nudge, mark it sent and push it to the user's devices (tag per session, so each replaces the last;
/// deep link back into the wizard). A user with no subscription simply gets nothing. Exhausted
/// schedules are deleted. With no VAPID keys the tick is a no-op.
/// </summary>
public sealed class FinisherNudgeService
{
    private readonly IFinisherScheduleRepository _schedules;
    private readonly IPushSubscriptionRepository _pushes;
    private readonly ILocalClock _clock;
    private readonly IPushSender _sender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PushOptions _options;
    private readonly ILogger<FinisherNudgeService> _logger;

    public FinisherNudgeService(
        IFinisherScheduleRepository schedules,
        IPushSubscriptionRepository pushes,
        ILocalClock clock,
        IPushSender sender,
        IUnitOfWork unitOfWork,
        IOptions<PushOptions> options,
        ILogger<FinisherNudgeService> logger)
    {
        _schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
        _pushes = pushes ?? throw new ArgumentNullException(nameof(pushes));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Returns how many nudges were sent (one per schedule, to any number of devices).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            return 0;
        }

        var now = _clock.NowUtc();
        var sent = 0;
        foreach (var schedule in await _schedules.ListDueAsync(now, ct))
        {
            ct.ThrowIfCancellationRequested();
            var nudge = schedule.TakeDue(now);
            if (schedule.IsExhausted)
            {
                _schedules.Remove(schedule);
            }
            else
            {
                _schedules.Update(schedule);
            }

            // Marked sent before anything goes out: a crash after this save can drop a nudge, never repeat one.
            await _unitOfWork.SaveChangesAsync(ct);
            if (nudge is null)
            {
                continue;
            }

            var message = new PushMessage(schedule.Title, nudge.Body, schedule.Url, schedule.Tag);
            var removed = false;
            foreach (var subscription in await _pushes.ListForUserAsync(schedule.UserId, ct))
            {
                var status = await _sender.SendAsync(new PushTarget(subscription.Endpoint, subscription.P256dh, subscription.Auth), message, ct);
                if (status == PushSendStatus.Gone)
                {
                    _pushes.Remove(subscription);
                    removed = true;
                }
                else if (status == PushSendStatus.Failed)
                {
                    _logger.LogWarning("Finisher nudge to subscription {SubscriptionId} failed", subscription.Id.Value);
                }
            }

            if (removed)
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }

            sent++;
        }

        return sent;
    }
}
