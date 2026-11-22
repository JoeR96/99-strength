using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>
/// Conditioning attached to the end of a lift session (brief § 3a): either a fixed cardio protocol or a
/// cardio progression track (exactly one). Immutable value object.
/// </summary>
public sealed record SessionFinisher
{
    public CardioProtocolId? CardioProtocolId { get; }

    /// <summary>A track finisher (e.g. the stair finisher): logging it evaluates the track.</summary>
    public CardioTrackId? CardioTrackId { get; }

    public SessionFinisher(CardioProtocolId? cardioProtocolId = null, CardioTrackId? cardioTrackId = null)
    {
        if (cardioProtocolId is { } protocol && protocol.Value == Guid.Empty || cardioTrackId is { } track && track.Value == Guid.Empty)
        {
            throw new BusinessRuleViolationException("A finisher needs a cardio protocol or track");
        }

        if ((cardioProtocolId is null) == (cardioTrackId is null))
        {
            throw new BusinessRuleViolationException("A finisher points at exactly one cardio protocol or track");
        }

        CardioProtocolId = cardioProtocolId;
        CardioTrackId = cardioTrackId;
    }

    public static SessionFinisher ForTrack(CardioTrackId track) => new(cardioTrackId: track);
}
