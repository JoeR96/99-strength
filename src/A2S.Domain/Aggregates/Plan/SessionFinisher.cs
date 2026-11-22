using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>
/// Conditioning attached to the end of a lift session (brief § 3a). Today a finisher is a cardio
/// protocol; a later change adds cardio-track finishers alongside it. Immutable value object.
/// </summary>
public sealed record SessionFinisher
{
    public CardioProtocolId CardioProtocolId { get; }

    public SessionFinisher(CardioProtocolId cardioProtocolId)
    {
        if (cardioProtocolId.Value == Guid.Empty)
        {
            throw new BusinessRuleViolationException("A finisher needs a cardio protocol");
        }

        CardioProtocolId = cardioProtocolId;
    }
}
