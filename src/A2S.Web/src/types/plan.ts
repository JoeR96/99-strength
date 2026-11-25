/**
 * Training plan types mirroring `A2S.Application.DTOs.PlanDtos` and the
 * `PlansController` request bodies. Plan enums serialise as **integers**
 * (System.Text.Json default); dates are `yyyy-MM-dd` Europe/London local dates.
 */

import type { LocalDate } from '@/lib/localDate';

export const SessionKind = { Rest: 0, Lift: 1, Cardio: 2, Mobility: 3 } as const;
export type SessionKind = (typeof SessionKind)[keyof typeof SessionKind];

export const SessionStatus = { Planned: 0, Done: 1, Missed: 2, Paused: 3, Rest: 4 } as const;
export type SessionStatus = (typeof SessionStatus)[keyof typeof SessionStatus];

export const PhaseType = { Cut: 0, Maintenance: 1, Pause: 2, Deload: 3 } as const;
export type PhaseType = (typeof PhaseType)[keyof typeof PhaseType];

export const AllowanceKind = { FlexibleMeal: 0, Alcohol: 1 } as const;
export type AllowanceKind = (typeof AllowanceKind)[keyof typeof AllowanceKind];

export const CompletionSource = { Manual: 0, Hevy: 1 } as const;
export type CompletionSource = (typeof CompletionSource)[keyof typeof CompletionSource];

/** .NET `DayOfWeek`: Sunday = 0 … Saturday = 6. */
export type DayOfWeek = 0 | 1 | 2 | 3 | 4 | 5 | 6;

/** A2S2 lifting day 1–6. */
export type PlanDayNumber = 1 | 2 | 3 | 4 | 5 | 6;

/** A fixed cool-down done after the lifts: logged as cardio, no track or protocol. */
export interface FixedFinisherDto {
  kind: 'Walk' | 'Treadmill';
  minutes: number;
  notes: string | null;
}

export interface ScheduledSessionDto {
  day: DayOfWeek;
  kind: SessionKind;
  liftDay: PlanDayNumber | null;
  cardioProtocolId: string | null;
  mobilityRoutineId: string | null;
  label: string;
  /** Cardio slots only: the cardio track this slot follows. */
  cardioTrackId?: string | null;
  /** Lift slots only: the warm-up routine; null falls back to the routine named "Mobility A". */
  warmUpRoutineId?: string | null;
  /** Lift slots only: the cardio protocol done as a finisher straight after the lifts. */
  finisherCardioProtocolId?: string | null;
  /** A lift finisher that is a cardio track (stair finisher). */
  finisherCardioTrackId?: string | null;
  /** A fixed cool-down finisher (e.g. treadmill, easy incline, 10 min); excludes the other two. */
  finisherFixed?: FixedFinisherDto | null;
}

export interface PlanPhaseDto {
  type: PhaseType;
  from: LocalDate;
  to: LocalDate;
  note: string | null;
  /** "Base", "Build", "Transition". */
  label?: string | null;
  /** What changes when this phase starts (shown the week before and on its first day). */
  changes?: string[] | null;
  swaps?: PhaseSwapDto[] | null;
}

export interface PhaseSwapDto {
  fromExercise: string;
  toExercise: string;
}

/** A swap Today offers at the phase start; `exerciseId` is the workout exercise to replace (null when it isn't there). */
export interface PhaseSwapActionDto extends PhaseSwapDto {
  workoutId: string | null;
  exerciseId: string | null;
  done: boolean;
}

export interface PhaseHeadsUpDto {
  type: PhaseType;
  label: string | null;
  from: LocalDate;
  to: LocalDate;
  daysUntil: number;
  changes: string[];
  swaps: PhaseSwapActionDto[];
}

export interface CheckpointDto {
  date: LocalDate;
  targetKg: number;
  note: string | null;
}

export interface WeeklyAllowancesDto {
  flexibleMeals: number;
  alcoholOccasions: number;
}

export interface CardioLogDto {
  durationSeconds: number;
  avgHeartRate: number | null;
  rpe: number | null;
  distanceMeters: number | null;
  notes: string | null;
  /** Minutes in HR zones 4–5; when omitted the server fills the protocol default (16 for a 4×4). */
  highIntensityMinutes?: number | null;
}

/** Weekly zone 4–5 minutes band (default 15–30). */
export interface HighIntensityTargetDto {
  minMinutes: number;
  maxMinutes: number;
}

/** Body for `PUT /plans/{id}/high-intensity-target`. */
export interface SetHighIntensityTargetRequest {
  minMinutes: number;
  maxMinutes: number;
}

export interface SessionCompletionDto {
  id: string;
  date: LocalDate;
  kind: SessionKind;
  liftDay: PlanDayNumber | null;
  cardioProtocolId: string | null;
  mobilityRoutineId: string | null;
  source: CompletionSource;
  hevyWorkoutId: string | null;
  recordedAtUtc: string;
  cardio: CardioLogDto | null;
  drillTicks: boolean[] | null;
}

export interface AllowanceUseDto {
  date: LocalDate;
  kind: AllowanceKind;
}

export interface TrainingPlanDto {
  id: string;
  name: string;
  startDate: LocalDate;
  isActive: boolean;
  linkedWorkoutId: string | null;
  startWeightKg: number;
  allowances: WeeklyAllowancesDto;
  highIntensityTarget: HighIntensityTargetDto;
  schedule: ScheduledSessionDto[];
  phases: PlanPhaseDto[];
  checkpoints: CheckpointDto[];
  completions: SessionCompletionDto[];
  allowanceUses: AllowanceUseDto[];
}

export interface TodaySessionDto {
  kind: SessionKind;
  label: string;
  status: SessionStatus;
  liftDay: PlanDayNumber | null;
  cardioProtocolId: string | null;
  mobilityRoutineId: string | null;
  completionId: string | null;
  /** The Hevy routine a push left for this session, or null when it was never pushed. */
  hevyRoutineId: string | null;
  /** Gym mode: the phone's client session id when this lift was finished in gym mode. */
  gymSessionId?: string | null;
  /** Gym mode: where the finished session stands in Hevy; null when not finished in gym mode. */
  hevyPostStatus?: HevyPostStatus | null;
  /** Lift slots only: the warm-up routine (null = "Mobility A"). */
  warmUpRoutineId?: string | null;
  /** Lift slots only: the attached finisher's cardio protocol. */
  finisherCardioProtocolId?: string | null;
  /** A lift finisher that is a cardio track (stair finisher). */
  finisherCardioTrackId?: string | null;
  /** A fixed cool-down finisher (e.g. treadmill, easy incline, 10 min); excludes the other two. */
  finisherFixed?: FixedFinisherDto | null;
}

/** A gym session's Hevy post: Skipped means no Hevy key is stored. */
/** UpdatePending: posted, and a later change (a cardio exercise's settings) is still to be sent; shown as Posted. */
export type HevyPostStatus = 'Pending' | 'Posted' | 'Failed' | 'Skipped' | 'UpdatePending';

export interface WeeklyGoalsDto {
  weekStart: LocalDate;
  liftsPlanned: number;
  liftsDone: number;
  cardioPlanned: number;
  cardioDone: number;
  mobilityPlanned: number;
  mobilityDone: number;
  flexibleMealsUsed: number;
  flexibleMealsAllowed: number;
  alcoholOccasions: number;
  alcoholAllowed: number;
  highIntensityMinutesDone: number;
  highIntensityMinutesMin: number;
  highIntensityMinutesMax: number;
}

export interface TodayDto {
  date: LocalDate;
  phase: PhaseType | null;
  sessions: TodaySessionDto[];
  week: WeeklyGoalsDto;
}

export interface PlanDayDto {
  date: LocalDate;
  phase: PhaseType | null;
  sessions: TodaySessionDto[];
}

/** A lift's planned sets on `GET /plans/upcoming` (current A2S2 week). An AMRAP set's `reps` is its rep-out target. */
export interface UpcomingLiftDto {
  liftDay: PlanDayNumber;
  label: string;
  weekNumber: number;
  isDeloadWeek: boolean;
  exercises: {
    name: string;
    category: string;
    sets: { weightKg: number; reps: number; isAmrap: boolean }[];
    /** A2S2 lift with no training max yet: sets weigh 0, show reps and intensity instead. */
    isTrainingMaxPending?: boolean;
    /** This week's A2S2 intensity (0.70 = 70 %), when the lift is A2S2. */
    intensityPercentage?: number | null;
  }[];
  finisher: { name: string; prescription: string } | null;
}

/** A day on `GET /plans/upcoming`: the calendar day plus its lifts' planned sets. */
export interface UpcomingDayDto extends PlanDayDto {
  lifts: UpcomingLiftDto[];
}

/** Body for `POST /plans/{id}/completions`. */
export interface CompleteSessionRequest {
  date: LocalDate;
  kind: SessionKind;
  liftDay?: PlanDayNumber | null;
  cardioProtocolId?: string | null;
  mobilityRoutineId?: string | null;
  cardio?: CardioLogDto | null;
  drillTicks?: boolean[] | null;
  hevyWorkoutId?: string | null;
  /** Mobility: minutes actually done (a follow-along video); omitted means the routine's target. */
  durationMinutes?: number | null;
}

/** Body for `POST /plans/{id}/allowances`; the DELETE takes the same two values as query params. */
export interface UseAllowanceRequest {
  date: LocalDate;
  kind: AllowanceKind;
}

export const sessionKindLabel: Record<SessionKind, string> = {
  [SessionKind.Rest]: 'Rest',
  [SessionKind.Lift]: 'Lift',
  [SessionKind.Cardio]: 'Cardio',
  [SessionKind.Mobility]: 'Mobility',
};

export const phaseTypeLabel: Record<PhaseType, string> = {
  [PhaseType.Cut]: 'Cut',
  [PhaseType.Maintenance]: 'Maintenance',
  [PhaseType.Pause]: 'Paused',
  [PhaseType.Deload]: 'Deload',
};
