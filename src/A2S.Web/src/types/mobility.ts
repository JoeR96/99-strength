/**
 * Mobility routine types mirroring `A2S.Application.DTOs.PlanDtos` (`MobilityRoutineDto`,
 * `MobilityDrillDto`, `DrillDoseDto`). Enums serialise as integers (System.Text.Json default).
 */

export const DoseUnit = { Reps: 0, Seconds: 1, Breaths: 2 } as const;
export type DoseUnit = (typeof DoseUnit)[keyof typeof DoseUnit];

export const SideBias = { None: 0, Left: 1, Right: 2 } as const;
export type SideBias = (typeof SideBias)[keyof typeof SideBias];

export interface DrillDoseDto {
  unit: DoseUnit;
  amount: number;
}

export interface MobilityDrillDto {
  name: string;
  sets: number;
  dose: DrillDoseDto;
  /** The side that gets one extra set (left-side compensation work). */
  extraSetSide: SideBias;
  cues: string | null;
  standingAlternative: string | null;
  /** The dose is done on each side (always left side first). */
  perSide: boolean;
  /** Progression chain, easiest first. Empty or missing when the drill doesn't progress. */
  levels?: DrillLevelDto[] | null;
  /** Index into `levels`; `sets` and `dose` are this level's current prescription. */
  currentLevel?: number;
  /** Clean sessions in a row since the last change. */
  cleanStreak?: number;
  /** A level the app suggests moving to, waiting for accept/decline. */
  suggestedLevel?: number | null;
}

export const MobilityRoutineKind = { WarmUp: 0, Corrective: 1, Stretch: 2, YogaFlow: 3, FollowAlong: 4 } as const;
export type MobilityRoutineKind = (typeof MobilityRoutineKind)[keyof typeof MobilityRoutineKind];

export const routineKindLabel: Record<MobilityRoutineKind, string> = {
  [MobilityRoutineKind.WarmUp]: 'Warm-up',
  [MobilityRoutineKind.Corrective]: 'Corrective',
  [MobilityRoutineKind.Stretch]: 'Stretch',
  [MobilityRoutineKind.YogaFlow]: 'Yoga flow',
  [MobilityRoutineKind.FollowAlong]: 'Follow-along video',
};

/** A dose range a level progresses through: hold 15 to 30 s, +5 s a step. */
export interface DoseRangeDto {
  min: number;
  max: number;
  step: number;
}

export interface DrillLevelDto {
  name: string;
  description: string | null;
  sets: number;
  dose: DrillDoseDto;
  range: DoseRangeDto | null;
  cues: string | null;
}

/** How a drill felt. Clean = the cues held without strain. */
export const DrillOutcome = { Clean: 0, Strained: 1, Stopped: 2, Skipped: 3 } as const;
export type DrillOutcome = (typeof DrillOutcome)[keyof typeof DrillOutcome];

export const DrillChangeKind = {
  DoseIncreased: 0,
  LevelUpSuggested: 1,
  LevelUp: 2,
  SuggestionDeclined: 3,
  SteppedBack: 4,
  Held: 5,
  HoldIncreased: 6,
  SessionRecorded: 7,
} as const;
export type DrillChangeKind = (typeof DrillChangeKind)[keyof typeof DrillChangeKind];

export interface DrillProgressionEntryDto {
  changeId: string;
  at: string;
  drillName: string;
  kind: DrillChangeKind;
  reason: string;
  undone: boolean;
}

export interface MobilityRoutineDto {
  id: string;
  name: string;
  targetMinutes: number;
  drills: MobilityDrillDto[];
  kind?: MobilityRoutineKind;
  /** Follow-along routines: the video or app session to open. */
  followAlongUrl?: string | null;
  /** Newest first. */
  recentChanges?: DrillProgressionEntryDto[] | null;
  canUndo?: boolean;
}

/** PUT `/mobility-routines/{id}`: rename and change target minutes (and, with a kind, the kind and link). */
export interface UpdateMobilityRoutineRequest {
  name: string;
  targetMinutes: number;
  kind?: MobilityRoutineKind;
  followAlongUrl?: string | null;
}

/** Body for `POST /mobility-routines`. */
export interface CreateMobilityRoutineRequest {
  name: string;
  targetMinutes: number;
  drills: MobilityDrillDto[];
  kind?: MobilityRoutineKind;
  followAlongUrl?: string | null;
}

export interface DrillFeedbackDto {
  drillName: string;
  outcome: DrillOutcome;
}

/** Body for `POST /mobility-routines/{id}/feedback`; `sessionKey` makes a replay count once. */
export interface DrillFeedbackRequest {
  sessionKey: string;
  feedback: DrillFeedbackDto[];
}

export interface DrillFeedbackResultDto {
  routine: MobilityRoutineDto;
  changes: DrillProgressionEntryDto[];
}

export const doseUnitLabel: Record<DoseUnit, { one: string; many: string }> = {
  [DoseUnit.Reps]: { one: 'rep', many: 'reps' },
  [DoseUnit.Seconds]: { one: 'second', many: 'seconds' },
  [DoseUnit.Breaths]: { one: 'breath', many: 'breaths' },
};

export const sideBiasLabel: Record<SideBias, string> = {
  [SideBias.None]: 'No extra set',
  [SideBias.Left]: 'Extra set left',
  [SideBias.Right]: 'Extra set right',
};
