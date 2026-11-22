using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;

namespace A2S.Application.Cardio;

/// <summary>
/// Turns a lift session's <see cref="SessionFinisher"/> into a <see cref="PlannedCardioDto"/>, and a track
/// evaluation into a <see cref="FinisherOutcomeDto"/>. Read-only mapping of what the domain already decided
/// (prescription, sessions to step up, outcome); no progression maths here.
/// </summary>
public static class PlannedCardioBuilder
{
    public const string TrackKind = "Track";
    public const string ProtocolKind = "Protocol";
    public const string FixedKind = "Fixed";

    public static PlannedCardioDto? For(
        SessionFinisher? finisher,
        IReadOnlyCollection<CardioTrack> tracks,
        IReadOnlyCollection<CardioProtocol> protocols,
        int orderInDay)
    {
        if (finisher is null)
        {
            return null;
        }

        if (finisher.Fixed is { } cooldown)
        {
            return new PlannedCardioDto
            {
                Kind = FixedKind,
                Name = cooldown.DisplayName(),
                Prescription = cooldown.Describe(),
                ProgressionType = "Fixed",
                OrderInDay = orderInDay,
                Minutes = cooldown.Minutes,
                Modality = cooldown.Kind.ToString()
            };
        }

        if (finisher.CardioTrackId is { } trackId && tracks.FirstOrDefault(t => t.Id == trackId) is { } track)
        {
            return ForTrack(track, orderInDay);
        }

        if (finisher.CardioProtocolId is { } protocolId && protocols.FirstOrDefault(p => p.Id == protocolId) is { } protocol)
        {
            return new PlannedCardioDto
            {
                Kind = ProtocolKind,
                Name = protocol.Name,
                Prescription = string.IsNullOrWhiteSpace(protocol.Notes) ? $"{protocol.TotalMinutes} min" : $"{protocol.TotalMinutes} min · {protocol.Notes}",
                ProgressionType = "Fixed",
                OrderInDay = orderInDay,
                Minutes = protocol.TotalMinutes,
                CardioProtocolId = protocol.Id.Value
            };
        }

        return null;
    }

    public static PlannedCardioDto ForTrack(CardioTrack track, int orderInDay)
    {
        var prescription = track.Prescription;
        var toStep = track.SessionsToStepUp;
        return new PlannedCardioDto
        {
            Kind = TrackKind,
            Name = track.Name,
            Prescription = prescription.Title,
            ProgressionType = track.CurrentStage.Kind == TrackStageKind.WeekProgram ? "Cardio track: week program" : "Cardio track: double progression",
            OrderInDay = orderInDay,
            Minutes = prescription.Minutes,
            IntensityUnit = prescription.IntensityUnit == IntensityUnit.None ? null : prescription.IntensityUnit.ToString(),
            Intensity = prescription.Intensity,
            Modality = track.Modality.ToString(),
            StageName = prescription.StageName,
            SessionsToStepUp = toStep,
            StepUpNote = StepUpNote(toStep),
            Note = prescription.Note,
            CardioTrackId = track.Id.Value
        };
    }

    /// <summary>"2 more sessions to step up", "1 more session to step up", or "Steps up after this session" at 0.</summary>
    public static string StepUpNote(int sessions) => sessions switch
    {
        <= 0 => "Steps up after this session",
        1 => "1 more session to step up",
        _ => $"{sessions} more sessions to step up"
    };

    public static FinisherOutcomeDto ForEvaluation(CardioTrack track, string before, TrackEvaluationResult result)
    {
        var next = result.Next.Title;
        var progressed = result.Outcome is TrackOutcome.SteppedUp or TrackOutcome.SteppedBack or TrackOutcome.StageComplete
            || !string.Equals(before, next, StringComparison.Ordinal);
        var toStep = track.SessionsToStepUp;
        var plural = toStep == 1 ? "session" : "sessions";
        var summary = progressed
            ? $"{track.Name}: {before} → {next}"
            : result.Outcome == TrackOutcome.Qualified
                ? $"Hold: {toStep} more qualifying {plural} to step up"
                : $"Hold: {result.Reason}";
        return new FinisherOutcomeDto
        {
            Kind = TrackKind,
            Name = track.Name,
            Outcome = result.Outcome.ToString(),
            Reason = result.Reason,
            Qualified = result.Qualified,
            Progressed = progressed,
            Before = before,
            Next = next,
            SessionsToStepUp = toStep,
            Summary = summary,
            NextTime = $"Next time: {next}"
        };
    }

    public static FinisherOutcomeDto Logged(string kind, string name) => new()
    {
        Kind = kind,
        Name = name,
        Outcome = "Logged",
        Reason = "Fixed finisher: no progression",
        Summary = $"{name}: logged"
    };
}
