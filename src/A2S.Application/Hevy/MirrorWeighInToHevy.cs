using A2S.Application.Common;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Hevy;

/// <summary>
/// Mirrors a recorded weigh-in to Hevy's body measurements (POST, PUT on 409) after the entry is
/// saved. Best effort: no key means nothing happens, and any Hevy failure or exception is logged
/// and never surfaced, so a weigh-in always succeeds locally whatever Hevy is doing.
/// </summary>
public sealed class MirrorWeighInToHevy : INotificationHandler<DomainEventNotification>
{
    private readonly IHevyKeyResolver _keys;
    private readonly IHevyClient _hevy;
    private readonly ILogger<MirrorWeighInToHevy> _logger;

    public MirrorWeighInToHevy(IHevyKeyResolver keys, IHevyClient hevy, ILogger<MirrorWeighInToHevy> logger)
    {
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _hevy = hevy ?? throw new ArgumentNullException(nameof(hevy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DomainEventNotification notification, CancellationToken cancellationToken)
    {
        if (notification.DomainEvent is not BodyWeightRecorded weighIn)
        {
            return;
        }

        try
        {
            var apiKey = await _keys.ForUserAsync(weighIn.UserId, cancellationToken);
            if (string.IsNullOrEmpty(apiKey))
            {
                return;
            }

            var result = await _hevy.UpsertBodyMeasurementAsync(apiKey, weighIn.Date, weighIn.Kg, cancellationToken);
            if (!result.Ok)
            {
                _logger.LogWarning("Weigh-in for {Date} not mirrored to Hevy: {Error}", weighIn.Date, result.Error!.Message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weigh-in for {Date} not mirrored to Hevy", weighIn.Date);
        }
    }
}
