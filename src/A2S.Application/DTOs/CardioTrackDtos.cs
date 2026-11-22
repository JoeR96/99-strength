using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.User;

namespace A2S.Application.DTOs;

public sealed record ProgramWeekDto(int Number, string Label, int Minutes, string Description);

public sealed record WeekProgramSettingsDto(IReadOnlyList<ProgramWeekDto> Weeks, int SessionsToAdvance, int SessionsPerWeek);

public sealed record DoubleProgressionSettingsDto(
    VolumeUnit VolumeUnit,
    decimal VolumeMin,
    decimal VolumeMax,
    decimal VolumeStep,
    decimal? VolumeStepPercent,
    IntensityUnit IntensityUnit,
    decimal? IntensityStart,
    decimal? IntensityStep,
    decimal? IntensityMax,
    int SessionsToAdvance,
    int? WorkSeconds,
    int? RestSeconds);

public sealed record TrackStageDto(
    string Name,
    TrackStageKind Kind,
    TrackEffortTarget Target,
    WeekProgramSettingsDto? WeekProgram,
    DoubleProgressionSettingsDto? DoubleProgression,
    int? HrCeilingZone,
    decimal? ZoneTargetFraction,
    string? Note);

public sealed record TrackBenchmarkDto(string Name, string Unit, bool HigherIsBetter, int EveryWeeks);

public sealed record CardioPrescriptionDto(
    string Title,
    string StageName,
    int Minutes,
    decimal WorkMinutes,
    VolumeUnit? VolumeUnit,
    decimal? Volume,
    IntensityUnit IntensityUnit,
    decimal? Intensity,
    int? WorkSeconds,
    int? RestSeconds,
    int? Week,
    string? Note);

public sealed record CardioTrackStateDto(
    int StageIndex,
    int Week,
    decimal Volume,
    decimal? Intensity,
    int Qualifying,
    int ConsecutiveMisses,
    int WeekSessions,
    bool StageComplete,
    TrackSuggestion Suggestion,
    int Step);

/// <summary>A track as the Tracks page and Today show it.</summary>
public sealed record CardioTrackDto(
    Guid Id,
    string Name,
    CardioModality Modality,
    CardioTrackStatus Status,
    string? TemplateKey,
    string? Notes,
    DateOnly StartedOn,
    bool IsHard,
    IReadOnlyList<TrackStageDto> Stages,
    CardioTrackStateDto State,
    CardioPrescriptionDto Prescription,
    int SessionsToStepUp,
    TrackSuggestion Suggestion,
    string? SuggestionText,
    bool CanUndo,
    TrackBenchmarkDto? Benchmark,
    bool BenchmarkDue,
    string SafetyNote);

public sealed record CardioTrackSessionDto(
    string Key,
    DateOnly Date,
    string Prescription,
    int StageIndex,
    bool Completed,
    bool Qualified,
    TrackOutcome Outcome,
    int DurationSeconds,
    EffortRating Effort,
    int? Rpe,
    PainFlag Pain,
    string? PainArea,
    int? AvgHeartRate,
    int? HighIntensityMinutes,
    int? Zone5Minutes,
    int? Floors,
    decimal? DistanceMeters,
    decimal? IntensityValue,
    CardioLogSource Source);

public sealed record CardioTrackAuditEntryDto(DateTimeOffset AtUtc, DateOnly? Date, string Action, string Reason, string Before, string After);

public sealed record BenchmarkResultDto(DateOnly Date, decimal Value, string? Notes, bool IsPersonalBest);

/// <summary>One point per evaluated session for the Progress chart: the step and the workload the track was at.</summary>
public sealed record CardioTrackChartPointDto(DateOnly Date, int Step, decimal WorkMinutes, decimal? Intensity, int? AvgHeartRate, int? HighIntensityMinutes, string Prescription);

public sealed record CardioTrackDetailDto(
    CardioTrackDto Track,
    IReadOnlyList<CardioTrackSessionDto> Sessions,
    IReadOnlyList<CardioTrackAuditEntryDto> Audit,
    IReadOnlyList<BenchmarkResultDto> Benchmarks,
    IReadOnlyList<CardioTrackChartPointDto> Chart);

public sealed record CardioTrackTemplateDto(string Key, string Name, CardioModality Modality, string Description);

public sealed record TrackEvaluationDto(TrackOutcome Outcome, string Reason, bool Qualified, bool AlreadyRecorded, CardioTrackDto Track);

public sealed record HeartRateZonesDto(int? MaxHeartRate, IReadOnlyList<int> LowerPercents, IReadOnlyList<HeartRateZoneBandDto> Bands);

public sealed record HeartRateZoneBandDto(int Zone, int LowerPercent, int UpperPercent, int? LowerBpm, int? UpperBpm);

public sealed record CardioGuardrailDto(string Code, string Message);

/// <summary>Today's cardio: one prescription per active track, plus weekly guardrail warnings.</summary>
public sealed record TodayCardioDto(
    IReadOnlyList<CardioTrackDto> Tracks,
    int HighIntensityMinutesThisWeek,
    IReadOnlyList<CardioGuardrailDto> Warnings);

public static class CardioTrackMappings
{
    public static CardioTrackDto ToDto(this CardioTrack track, DateOnly today)
    {
        var p = track.Prescription;
        return new CardioTrackDto(
            track.Id.Value,
            track.Name,
            track.Modality,
            track.Status,
            track.TemplateKey,
            track.Notes,
            track.StartedOn,
            track.IsHard,
            track.Stages.Select(ToDto).ToList(),
            new CardioTrackStateDto(track.State.StageIndex, track.State.Week, track.State.Volume, track.State.Intensity,
                track.State.Qualifying, track.State.ConsecutiveMisses, track.State.WeekSessions, track.State.StageComplete,
                track.State.Suggestion, track.State.Step),
            p.ToDto(),
            track.SessionsToStepUp,
            track.State.Suggestion,
            SuggestionText(track),
            track.CanUndo,
            track.Benchmark is { } b ? new TrackBenchmarkDto(b.Name, b.Unit, b.HigherIsBetter, b.EveryWeeks) : null,
            track.IsBenchmarkDue(today),
            CardioTrackTemplates.SafetyLine);
    }

    public static CardioPrescriptionDto ToDto(this CardioPrescription p) => new(p.Title, p.StageName, p.Minutes, p.WorkMinutes,
        p.VolumeUnit, p.Volume, p.IntensityUnit, p.Intensity, p.WorkSeconds, p.RestSeconds, p.Week, p.Note);

    public static CardioTrackDetailDto ToDetailDto(this CardioTrack track, DateOnly today)
    {
        var chart = new List<CardioTrackChartPointDto>();
        var step = 0;
        foreach (var session in track.Sessions)
        {
            if (session.Outcome is TrackOutcome.SteppedUp or TrackOutcome.SteppedBack)
            {
                step += session.Outcome == TrackOutcome.SteppedUp ? 1 : -1;
            }

            chart.Add(new CardioTrackChartPointDto(session.Date, step, session.DurationSeconds / 60m, session.IntensityValue,
                session.AvgHeartRate, session.HighIntensityMinutes, session.Prescription));
        }

        return new CardioTrackDetailDto(
            track.ToDto(today),
            track.Sessions.Select(s => new CardioTrackSessionDto(s.Key, s.Date, s.Prescription, s.StageIndex, s.Completed, s.Qualified,
                s.Outcome, s.DurationSeconds, s.Effort, s.Rpe, s.Pain, s.PainArea, s.AvgHeartRate, s.HighIntensityMinutes,
                s.Zone5Minutes, s.Floors, s.DistanceMeters, s.IntensityValue, s.Source)).ToList(),
            track.Audit.Select(a => new CardioTrackAuditEntryDto(a.AtUtc, a.Date, a.Action, a.Reason, a.Before, a.After)).ToList(),
            track.Benchmarks.Select(b => new BenchmarkResultDto(b.Date, b.Value, b.Notes, b.IsPersonalBest)).ToList(),
            chart);
    }

    public static TrackStageDto ToDto(this TrackStage s) => new(
        s.Name,
        s.Kind,
        s.Target,
        s.WeekProgram is { } w
            ? new WeekProgramSettingsDto(w.Weeks.Select(x => new ProgramWeekDto(x.Number, x.Label, x.Minutes, x.Description)).ToList(),
                w.SessionsToAdvance, w.SessionsPerWeek)
            : null,
        s.DoubleProgression is { } d
            ? new DoubleProgressionSettingsDto(d.VolumeUnit, d.VolumeMin, d.VolumeMax, d.VolumeStep, d.VolumeStepPercent, d.IntensityUnit,
                d.IntensityStart, d.IntensityStep, d.IntensityMax, d.SessionsToAdvance, d.WorkSeconds, d.RestSeconds)
            : null,
        s.HrCeilingZone,
        s.ZoneTargetFraction,
        s.Note);

    public static TrackStage ToDomain(this TrackStageDto s) => new(
        s.Name,
        s.Kind,
        s.Target,
        s.WeekProgram is { } w
            ? new WeekProgramSettings(w.Weeks.Select(x => new ProgramWeek(x.Number, x.Label, x.Minutes, x.Description)).ToList(),
                w.SessionsToAdvance, w.SessionsPerWeek)
            : null,
        s.DoubleProgression is { } d
            ? new DoubleProgressionSettings(d.VolumeUnit, d.VolumeMin, d.VolumeMax, d.VolumeStep, d.VolumeStepPercent, d.IntensityUnit,
                d.IntensityStart, d.IntensityStep, d.IntensityMax, d.SessionsToAdvance, d.WorkSeconds, d.RestSeconds)
            : null,
        s.HrCeilingZone,
        s.ZoneTargetFraction,
        s.Note);

    public static HeartRateZonesDto ToZonesDto(int? maxHeartRate, IReadOnlyList<int>? lowerPercents)
    {
        var lowers = lowerPercents ?? HeartRateZones.WhoopLowerPercents;
        var zones = maxHeartRate is { } max ? new HeartRateZones(max, lowers) : null;
        var bands = Enumerable.Range(1, 5).Select(z => new HeartRateZoneBandDto(
            z,
            lowers[z - 1],
            z == 5 ? 100 : lowers[z],
            zones?.LowerBpm(z),
            zones?.UpperBpm(z))).ToList();
        return new HeartRateZonesDto(maxHeartRate, lowers.ToList(), bands);
    }

    private static string? SuggestionText(CardioTrack track) => track.State.Suggestion switch
    {
        TrackSuggestion.RepeatWeek => "Not every session this week counted. Repeat the week?",
        TrackSuggestion.DropBackWeek => "Pain flagged. Drop back a week?",
        TrackSuggestion.NextStage when track.State.StageIndex + 1 < track.Stages.Count =>
            $"Top of this stage. Move on to {track.Stages[track.State.StageIndex + 1].Name}?",
        _ => null
    };
}
