/**
 * Cardio progression tracks (`/api/v1/cardio-tracks`). Enums serialise as **integers**, matching
 * the C# enums in `A2S.Domain.Aggregates.Cardio`.
 */

export const CardioModality = { Walk: 0, Run: 1, StairClimber: 2, Bike: 3, Row: 4, Other: 5 } as const;
export type CardioModality = (typeof CardioModality)[keyof typeof CardioModality];

export const CardioTrackStatus = { Active: 0, Paused: 1, Completed: 2, Gated: 3 } as const;
export type CardioTrackStatus = (typeof CardioTrackStatus)[keyof typeof CardioTrackStatus];

/** The three effort buttons; the value is the RPE stored for each. */
export const EffortRating = { Easy: 4, Solid: 7, Brutal: 9 } as const;
export type EffortRating = (typeof EffortRating)[keyof typeof EffortRating];

export const PainFlag = { None: 0, Niggle: 1, Stop: 2 } as const;
export type PainFlag = (typeof PainFlag)[keyof typeof PainFlag];

export const CardioLogSource = { Manual: 0, Hevy: 1, Whoop: 2 } as const;
export type CardioLogSource = (typeof CardioLogSource)[keyof typeof CardioLogSource];

export const IntensityUnit = { None: 0, Level: 1, InclinePercent: 2, SpeedKmh: 3 } as const;
export type IntensityUnit = (typeof IntensityUnit)[keyof typeof IntensityUnit];

export const VolumeUnit = { Minutes: 0, Rounds: 1 } as const;
export type VolumeUnit = (typeof VolumeUnit)[keyof typeof VolumeUnit];

export const TrackStageKind = { WeekProgram: 0, DoubleProgression: 1 } as const;
export type TrackStageKind = (typeof TrackStageKind)[keyof typeof TrackStageKind];

export const TrackEffortTarget = { Easy: 0, Hard: 1 } as const;
export type TrackEffortTarget = (typeof TrackEffortTarget)[keyof typeof TrackEffortTarget];

export const TrackSuggestion = { None: 0, RepeatWeek: 1, DropBackWeek: 2, NextStage: 3 } as const;
export type TrackSuggestion = (typeof TrackSuggestion)[keyof typeof TrackSuggestion];

export const TrackOutcome = {
  Qualified: 0,
  SteppedUp: 1,
  Held: 2,
  SteppedBack: 3,
  StageComplete: 4,
  SuggestRepeat: 5,
  SuggestDropBack: 6,
  AwaitingDecision: 7,
  Paused: 8,
  AtTop: 9,
} as const;
export type TrackOutcome = (typeof TrackOutcome)[keyof typeof TrackOutcome];

export interface ProgramWeekDto {
  number: number;
  label: string;
  minutes: number;
  description: string;
}

export interface WeekProgramSettingsDto {
  weeks: ProgramWeekDto[];
  sessionsToAdvance: number;
  sessionsPerWeek: number;
}

export interface DoubleProgressionSettingsDto {
  volumeUnit: VolumeUnit;
  volumeMin: number;
  volumeMax: number;
  volumeStep: number;
  volumeStepPercent: number | null;
  intensityUnit: IntensityUnit;
  intensityStart: number | null;
  intensityStep: number | null;
  intensityMax: number | null;
  sessionsToAdvance: number;
  workSeconds: number | null;
  restSeconds: number | null;
}

export interface TrackStageDto {
  name: string;
  kind: TrackStageKind;
  target: TrackEffortTarget;
  weekProgram: WeekProgramSettingsDto | null;
  doubleProgression: DoubleProgressionSettingsDto | null;
  hrCeilingZone: number | null;
  zoneTargetFraction: number | null;
  note: string | null;
}

export interface TrackBenchmarkDto {
  name: string;
  unit: string;
  higherIsBetter: boolean;
  everyWeeks: number;
}

export interface CardioPrescriptionDto {
  title: string;
  stageName: string;
  minutes: number;
  workMinutes: number;
  volumeUnit: VolumeUnit | null;
  volume: number | null;
  intensityUnit: IntensityUnit;
  intensity: number | null;
  workSeconds: number | null;
  restSeconds: number | null;
  week: number | null;
  note: string | null;
}

export interface CardioTrackStateDto {
  stageIndex: number;
  week: number;
  volume: number;
  intensity: number | null;
  qualifying: number;
  consecutiveMisses: number;
  weekSessions: number;
  stageComplete: boolean;
  suggestion: TrackSuggestion;
  step: number;
}

export interface CardioTrackDto {
  id: string;
  name: string;
  modality: CardioModality;
  status: CardioTrackStatus;
  templateKey: string | null;
  notes: string | null;
  startedOn: string;
  isHard: boolean;
  stages: TrackStageDto[];
  state: CardioTrackStateDto;
  prescription: CardioPrescriptionDto;
  sessionsToStepUp: number;
  suggestion: TrackSuggestion;
  suggestionText: string | null;
  canUndo: boolean;
  benchmark: TrackBenchmarkDto | null;
  benchmarkDue: boolean;
  safetyNote: string;
  /** Conditions to meet before the track starts (empty: no gate). */
  startGate?: GateConditionDto[] | null;
  /** Progress per condition while the track is gated. */
  gate?: GateProgressDto | null;
  /** Client only: sessions logged on this phone that have not reached the server yet. */
  pendingSessions?: number;
}

export interface CardioTrackSessionDto {
  key: string;
  date: string;
  prescription: string;
  stageIndex: number;
  completed: boolean;
  qualified: boolean;
  outcome: TrackOutcome;
  durationSeconds: number;
  effort: EffortRating;
  rpe: number | null;
  pain: PainFlag;
  painArea: string | null;
  avgHeartRate: number | null;
  highIntensityMinutes: number | null;
  zone5Minutes: number | null;
  floors: number | null;
  distanceMeters: number | null;
  intensityValue: number | null;
  source: CardioLogSource;
}

export interface CardioTrackAuditEntryDto {
  atUtc: string;
  date: string | null;
  action: string;
  reason: string;
  before: string;
  after: string;
}

export interface BenchmarkResultDto {
  date: string;
  value: number;
  notes: string | null;
  isPersonalBest: boolean;
}

export interface CardioTrackChartPointDto {
  date: string;
  step: number;
  workMinutes: number;
  intensity: number | null;
  avgHeartRate: number | null;
  highIntensityMinutes: number | null;
  prescription: string;
}

export interface CardioTrackDetailDto {
  track: CardioTrackDto;
  sessions: CardioTrackSessionDto[];
  audit: CardioTrackAuditEntryDto[];
  benchmarks: BenchmarkResultDto[];
  chart: CardioTrackChartPointDto[];
}

export interface CardioTrackTemplateDto {
  key: string;
  name: string;
  modality: CardioModality;
  description: string;
}

export interface TrackEvaluationDto {
  outcome: TrackOutcome;
  reason: string;
  qualified: boolean;
  alreadyRecorded: boolean;
  track: CardioTrackDto;
}

export interface HeartRateZoneBandDto {
  zone: number;
  lowerPercent: number;
  upperPercent: number;
  lowerBpm: number | null;
  upperBpm: number | null;
}

export interface HeartRateZonesDto {
  maxHeartRate: number | null;
  lowerPercents: number[];
  bands: HeartRateZoneBandDto[];
}

export interface CardioGuardrailDto {
  code: string;
  message: string;
}

export interface TodayCardioDto {
  tracks: CardioTrackDto[];
  highIntensityMinutesThisWeek: number;
  warnings: CardioGuardrailDto[];
  /** Tracks waiting on a start gate, each with its progress. */
  gated?: CardioTrackDto[] | null;
}

/** A start-gate condition (the server sends the type as a string). */
export type GateConditionType = 'TrendWeightAtOrBelow' | 'TrackAtOrAbove' | 'NoPainInAreas';

export interface GateConditionDto {
  type: GateConditionType;
  kg?: number | null;
  trackTemplateKey?: string | null;
  minutes?: number | null;
  intensity?: number | null;
  effort?: number | null;
  weeks?: number | null;
  areas?: string[] | null;
}

export interface GateConditionResultDto {
  condition: GateConditionDto;
  met: boolean;
  label: string;
  progress: string;
}

/** A gated track's progress; `allMet` means Today suggests Start (the person accepts). */
export interface GateProgressDto {
  conditions: GateConditionResultDto[];
  allMet: boolean;
  metCount: number;
}

/** What the log sheet sends: the effort button plus anything else the person chose to fill in. */
export interface TrackLogDto {
  durationSeconds: number;
  avgHeartRate: number | null;
  rpe: number | null;
  distanceMeters: number | null;
  notes: string | null;
  highIntensityMinutes?: number | null;
  effort?: EffortRating | null;
  intensityUnit?: IntensityUnit | null;
  intensityValue?: number | null;
  floors?: number | null;
  steps?: number | null;
  maxHeartRate?: number | null;
  zone5Minutes?: number | null;
  pain?: PainFlag;
  painArea?: string | null;
  source?: CardioLogSource;
}

export interface LogTrackSessionRequest {
  log: TrackLogDto;
  completedAsPrescribed: boolean;
  date?: string | null;
  clientKey?: string | null;
}

export interface UpdateCardioTrackRequest {
  name: string;
  notes: string | null;
  stages: TrackStageDto[];
  benchmark: TrackBenchmarkDto | null;
}

export const modalityLabel: Record<CardioModality, string> = {
  [CardioModality.Walk]: 'Walk',
  [CardioModality.Run]: 'Run',
  [CardioModality.StairClimber]: 'Stairs',
  [CardioModality.Bike]: 'Bike',
  [CardioModality.Row]: 'Row',
  [CardioModality.Other]: 'Other',
};

export const intensityLabel: Record<IntensityUnit, string> = {
  [IntensityUnit.None]: 'None',
  [IntensityUnit.Level]: 'Level',
  [IntensityUnit.InclinePercent]: 'Incline %',
  [IntensityUnit.SpeedKmh]: 'Speed km/h',
};

/** "Stepped up", "Held"… for the audit log. */
export function outcomeLabel(action: string): string {
  const labels: Record<string, string> = {
    Qualified: 'Counted',
    SteppedUp: 'Stepped up',
    Held: 'Held',
    SteppedBack: 'Stepped back',
    StageComplete: 'Stage complete',
    SuggestRepeat: 'Repeat suggested',
    SuggestDropBack: 'Drop back suggested',
    AwaitingDecision: 'Waiting for you',
    Paused: 'Paused',
    AtTop: 'At the top',
    RepeatAccepted: 'Repeating the week',
    DropBackAccepted: 'Dropped back',
    StageAccepted: 'Next stage',
    SuggestionOverridden: 'Overridden',
    Edited: 'Settings changed',
  };
  return labels[action] ?? action;
}
