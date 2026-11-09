using A2S.Application.Hevy;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Services;

namespace A2S.Application.DTOs;

/// <summary>Domain ↔ DTO mapping for the plan, cardio and mobility aggregates.</summary>
public static class PlanMappings
{
    public static TrainingPlanDto ToDto(this TrainingPlan plan) => new(
        plan.Id.Value,
        plan.Name,
        plan.StartDate,
        plan.IsActive,
        plan.LinkedWorkoutId?.Value,
        plan.StartWeightKg,
        new WeeklyAllowancesDto(plan.Allowances.FlexibleMeals, plan.Allowances.AlcoholOccasions),
        new HighIntensityTargetDto(plan.HighIntensityTarget.MinMinutes, plan.HighIntensityTarget.MaxMinutes),
        plan.Schedule.Select(ToDto).ToList(),
        plan.Phases.Select(p => new PlanPhaseDto(p.Type, p.From, p.To, p.Note)).ToList(),
        plan.Checkpoints.Select(c => new CheckpointDto(c.Date, c.TargetKg, c.Note)).ToList(),
        plan.Completions.Select(ToDto).ToList(),
        plan.AllowanceUses.Select(u => new AllowanceUseDto(u.Date, u.Kind)).ToList());

    public static ScheduledSessionDto ToDto(this ScheduledSession session) => new(
        session.Day,
        session.Kind,
        session.LiftDay,
        session.CardioProtocolId?.Value,
        session.MobilityRoutineId?.Value,
        session.Label);

    public static ScheduledSession ToDomain(this ScheduledSessionDto dto) => new(
        dto.Day,
        dto.Kind,
        dto.LiftDay,
        dto.CardioProtocolId is { } cardio ? new CardioProtocolId(cardio) : null,
        dto.MobilityRoutineId is { } mobility ? new MobilityRoutineId(mobility) : null,
        dto.Label);

    public static SessionCompletionDto ToDto(this SessionCompletion completion) => new(
        completion.Id.Value,
        completion.Date,
        completion.Kind,
        completion.LiftDay,
        completion.CardioProtocolId?.Value,
        completion.MobilityRoutineId?.Value,
        completion.Source,
        completion.HevyWorkoutId,
        completion.RecordedAtUtc,
        completion.Cardio?.ToDto(),
        completion.DrillTicks);

    public static CardioLogDto ToDto(this CardioLog log) =>
        new(log.DurationSeconds, log.AvgHeartRate, log.Rpe, log.DistanceMeters, log.Notes, log.HighIntensityMinutes);

    public static CardioLog ToDomain(this CardioLogDto dto) =>
        new(dto.DurationSeconds, dto.AvgHeartRate, dto.Rpe, dto.DistanceMeters, dto.Notes, dto.HighIntensityMinutes);

    public static TodaySessionDto ToDto(this PlanDaySession session) => session.ToDto(hevyRoutineId: null);

    public static TodaySessionDto ToDto(this PlanDaySession session, string? hevyRoutineId) => new(
        session.Session.Kind,
        session.Session.Label,
        session.Status,
        session.Session.LiftDay,
        session.Session.CardioProtocolId?.Value,
        session.Session.MobilityRoutineId?.Value,
        session.Completion?.Id.Value,
        hevyRoutineId);

    public static PlanDayDto ToDto(this PlanDay day) =>
        new(day.Date, day.Phase, day.Sessions.Select(s => s.ToDto()).ToList());

    /// <summary>Maps a day with each session's pushed Hevy routine id (<see cref="HevyRoutineLookup"/>) filled in.</summary>
    public static PlanDayDto ToDto(this PlanDay day, TrainingPlanId planId, HevyRoutineLookup routines) =>
        new(day.Date, day.Phase, day.Sessions.Select(s => s.ToDto(routines.For(planId, day.Date, s.Session))).ToList());

    public static WeeklyGoalsDto ToDto(this WeeklyGoals goals) => new(
        goals.WeekStart,
        goals.LiftsPlanned, goals.LiftsDone,
        goals.CardioPlanned, goals.CardioDone,
        goals.MobilityPlanned, goals.MobilityDone,
        goals.FlexibleMealsUsed, goals.FlexibleMealsAllowed,
        goals.AlcoholOccasions, goals.AlcoholAllowed,
        goals.HighIntensityMinutesDone, goals.HighIntensityMinutesMin, goals.HighIntensityMinutesMax);

    public static CardioProtocolDto ToDto(this CardioProtocol protocol) => new(
        protocol.Id.Value,
        protocol.Name,
        protocol.Kind,
        protocol.Intervals.Select(i => new CardioIntervalDto(i.Label, i.Seconds, i.Effort)).ToList(),
        protocol.TotalMinutes,
        protocol.Notes,
        protocol.WeekIndex,
        protocol.RepeatWeek);

    public static MobilityRoutineDto ToDto(this MobilityRoutine routine) => new(
        routine.Id.Value,
        routine.Name,
        routine.TargetMinutes,
        routine.Drills.Select(ToDto).ToList());

    public static MobilityDrillDto ToDto(this MobilityDrill drill) => new(
        drill.Name,
        drill.Sets,
        new DrillDoseDto(drill.Dose.Unit, drill.Dose.Amount),
        drill.ExtraSetSide,
        drill.Cues,
        drill.StandingAlternative,
        drill.PerSide);

    public static MobilityDrill ToDomain(this MobilityDrillDto dto) => new(
        dto.Name,
        dto.Sets,
        new DrillDose(dto.Dose.Unit, dto.Dose.Amount),
        dto.ExtraSetSide,
        dto.Cues,
        dto.StandingAlternative,
        dto.PerSide);
}
