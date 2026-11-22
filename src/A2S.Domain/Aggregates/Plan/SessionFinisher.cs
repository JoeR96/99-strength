using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>
/// Conditioning attached to the end of a lift session (brief § 3a): either a fixed cardio protocol or a
/// cardio progression track or a fixed activity such as a 10-minute treadmill cool-down (exactly one).
/// Immutable value object.
/// </summary>
public sealed record SessionFinisher
{
    public CardioProtocolId? CardioProtocolId { get; }

    /// <summary>A track finisher (e.g. the stair finisher): logging it evaluates the track.</summary>
    public CardioTrackId? CardioTrackId { get; }

    /// <summary>A fixed cool-down: ticked and logged, no protocol rules, no track evaluation.</summary>
    public FixedActivity? Fixed { get; }

    public SessionFinisher(CardioProtocolId? cardioProtocolId = null, CardioTrackId? cardioTrackId = null, FixedActivity? @fixed = null)
    {
        if (cardioProtocolId is { } protocol && protocol.Value == Guid.Empty || cardioTrackId is { } track && track.Value == Guid.Empty)
        {
            throw new BusinessRuleViolationException("A finisher needs a cardio protocol or track");
        }

        var count = (cardioProtocolId is null ? 0 : 1) + (cardioTrackId is null ? 0 : 1) + (@fixed is null ? 0 : 1);
        if (count != 1)
        {
            throw new BusinessRuleViolationException("A finisher points at exactly one cardio protocol, track or fixed cool-down");
        }

        CardioProtocolId = cardioProtocolId;
        CardioTrackId = cardioTrackId;
        Fixed = @fixed;
    }

    public static SessionFinisher ForFixed(FixedActivity activity) => new(@fixed: activity);

    public static SessionFinisher ForTrack(CardioTrackId track) => new(cardioTrackId: track);
}
