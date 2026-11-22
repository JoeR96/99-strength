namespace A2S.Application.DTOs;

/// <summary>
/// The cardio finisher of a lift session as a planned item, shown after the exercises like one more card
/// (week plan, <c>/plans/upcoming</c>, gym mode). <see cref="Kind"/> is Track (a progression track, e.g. the
/// stair finisher), Protocol (a fixed cardio protocol) or Fixed (a fixed cool-down such as 10 min treadmill).
/// <see cref="Prescription"/> is what to do ("5 min continuous @ level 6"); <see cref="ProgressionType"/> is
/// "Cardio track: double progression", "Cardio track: week program" or "Fixed"; <see cref="StepUpNote"/> is
/// "2 more sessions to step up" for a track (null otherwise). <see cref="Minutes"/>, <see cref="IntensityUnit"/>
/// and <see cref="Intensity"/> prefill the log (Level 6 → the level field).
/// </summary>
public sealed record PlannedCardioDto
{
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Prescription { get; init; } = string.Empty;
    public string ProgressionType { get; init; } = string.Empty;
    public int OrderInDay { get; init; }
    public int Minutes { get; init; }
    public string? IntensityUnit { get; init; }
    public decimal? Intensity { get; init; }
    public string? Modality { get; init; }
    public string? StageName { get; init; }
    public int? SessionsToStepUp { get; init; }
    public string? StepUpNote { get; init; }
    public string? Note { get; init; }
    public Guid? CardioTrackId { get; init; }
    public Guid? CardioProtocolId { get; init; }

    /// <summary>A track's fixed easy warm-up before the work block (null: none). <see cref="Minutes"/> includes it.</summary>
    public TrackSegmentDto? WarmUp { get; init; }

    /// <summary>A track's fixed easy cool-down after the work block (null: none). <see cref="Minutes"/> includes it.</summary>
    public TrackSegmentDto? CoolDown { get; init; }

    /// <summary>Minutes of the work block alone (tracks only); <see cref="Minutes"/> is the whole session.</summary>
    public decimal? WorkMinutes { get; init; }

    /// <summary>The whole session as one line: "3 min easy (L3) → 5 min continuous @ level 6 → 3 min easy (L3)".</summary>
    public string? Text { get; init; }
}

/// <summary>
/// What finishing a session did to its cardio finisher, like a lift's progression change. <see cref="Outcome"/> is
/// the track outcome (SteppedUp, Qualified, Held, …) or Logged for a fixed finisher. <see cref="Summary"/> reads
/// "Stair finisher: 5 min continuous @ level 6 → 6 min continuous @ level 6" or "Hold: 1 more qualifying session
/// to step up"; <see cref="NextTime"/> is "Next time: 6 min continuous @ level 6" (null for a fixed finisher).
/// </summary>
public sealed record FinisherOutcomeDto
{
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool Qualified { get; init; }
    public bool Progressed { get; init; }
    public string? Before { get; init; }
    public string? Next { get; init; }
    public int? SessionsToStepUp { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string? NextTime { get; init; }
}
