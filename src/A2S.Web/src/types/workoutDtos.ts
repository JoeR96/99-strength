/**
 * Workout and Exercise DTOs matching backend response types
 * Imported by workout.ts and re-exported for backward compatibility
 */

import type {
  RepRange,
  TrainingMax,
} from './workout';
import {
  WeightUnit,
  ExerciseCategory,
  EquipmentType,
  DayNumber,
  ProgramVariant,
  WorkoutStatus,
} from './workout';

export interface WorkoutDto {
  id: string;
  name: string;
  variant: ProgramVariant;
  status: WorkoutStatus;
  currentWeek: number;
  currentBlock: number;
  currentDay: number;
  daysPerWeek: number;
  completedDaysInCurrentWeek: number[];
  isWeekComplete: boolean;
  totalWeeks: number;
  blockSequence: number[];
  startDate: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  exerciseCount: number;
  exercises: ExerciseDto[];
}

export type ExtraSetSide = 'None' | 'Left' | 'Right';

export interface ExerciseDto {
  id: string;
  name: string;
  category: ExerciseCategory;
  equipment: EquipmentType;
  assignedDay: DayNumber;
  orderInDay: number;
  hevyExerciseTemplateId: string;
  progression: ExerciseProgressionDto;
  /** Exercises on the same day with the same number are a superset (A = lower orderInDay). */
  supersetGroup?: number | null;
  /** One bonus set on that side (unilateral reps-per-set only). */
  extraSetSide?: ExtraSetSide;
  /** An A2S2 lift created without a training max: it needs one before its first session. */
  isTrainingMaxPending?: boolean;
  lastPerformance?: LastPerformanceDto | null;
}

// Most recent completed performance for an exercise ("what you hit last time")
export interface LastPerformanceDto {
  weekNumber: number;
  completedAt: string;
  sets: LastPerformanceSetDto[];
}

export interface LastPerformanceSetDto {
  setNumber: number;
  weight: number;
  weightUnit: string;
  reps: number;
  wasAmrap: boolean;
}

export interface ExerciseProgressionDto {
  type: "Linear" | "RepsPerSet" | "MinimalSets" | "CardioStage";
}

export interface LinearProgressionDto extends ExerciseProgressionDto {
  type: "Linear";
  trainingMax: TrainingMax;
  useAmrap: boolean;
  baseSetsPerExercise: number;
}

export interface RepsPerSetProgressionDto extends ExerciseProgressionDto {
  type: "RepsPerSet";
  repRange: RepRange;
  startingSets: number;
  currentSetCount: number;
  targetSets: number;
  currentWeight: number;
  weightUnit: string; // Backend returns string "Kilograms" or "Pounds"
  isUnilateral: boolean; // True if exercise is performed one side at a time
  isWeightPending: boolean; // True if starting weight has not yet been confirmed
  // True when progression raised a Cable/Machine weight and the gym's actual stack
  // weight hasn't been confirmed yet (confirmed automatically from the next session)
  pendingWeightConfirmation: boolean;
  suggestedWeight: number | null; // Suggested new weight while pendingWeightConfirmation
}

export interface MinimalSetsProgressionDto extends ExerciseProgressionDto {
  type: "MinimalSets";
  currentWeight: number;
  weightUnit: string;
  targetTotalReps: number;
  currentSetCount: number;
  minimumSets: number;
  maximumSets: number;
}

/** A cardio exercise (stair climber = Hard lane, incline walk = Easy lane): its stage and today's prescription. */
export interface CardioStageProgressionDto extends ExerciseProgressionDto {
  type: "CardioStage";
  lane: "Hard" | "Easy";
  stageNumber: number;
  stageName: string;
  prescription: string;
  /** The level/incline (or the walk's speed) is not set yet; gym mode asks for it. */
  isSettingsPending: boolean;
  /** What `intensity` is: the stair level or the walk's incline %. */
  intensityUnit: "Level" | "InclinePercent";
  intensity: number | null;
  /** Offered for the hard level while it is not set. */
  suggestedIntensity: number | null;
  /** The walk's speed (km/h). */
  speed: number | null;
}

// Hevy sync discrepancy types
export interface WeightDiscrepancy {
  exerciseId: string;
  exerciseName: string;
  prescribedWeight: number;  // in kg
  actualWeights: number[];   // Array of weights from Hevy (one per set, in kg)
  hasVaryingWeights: boolean; // True if sets have different weights
  sets: PulledSetData[];     // Include all set data
  progressionType: string;   // "Linear" | "RepsPerSet" | "MinimalSets"
}

export interface MissingExercise {
  exerciseId: string;
  exerciseName: string;
  prescribedSets: number;
  prescribedReps: number;
  prescribedWeight: number;  // in kg
}

export interface PulledSetData {
  setNumber: number;
  weight: number;  // in kg
  reps: number;
  isAmrap: boolean;
}

// Request DTOs
export interface CreateExerciseRequest {
  templateName: string;
  externalTemplateId?: string;
  category: ExerciseCategory;
  progressionType: "Linear" | "RepsPerSet" | "MinimalSets";
  assignedDay: DayNumber;
  orderInDay: number;
  // For Linear progression
  trainingMaxValue?: number;
  trainingMaxUnit?: WeightUnit;
  // For RepsPerSet progression
  startingWeight?: number;
  weightUnit?: WeightUnit;
  startingSets?: number;
  targetSets?: number;
  isUnilateral?: boolean;
  repRangeMinimum?: number;

  repRangeMaximum?: number;
  // For MinimalSets progression
  targetTotalReps?: number;
}

export interface CreateWorkoutRequest {
  name: string;
  variant: ProgramVariant;
  totalWeeks: number;
  blockSequence?: number[];
  exercises?: CreateExerciseRequest[];
}

export interface ExerciseWithTrainingMax {
  exerciseName: string;
  trainingMax: TrainingMax;
}

// Workout summary for list views
export interface WorkoutSummaryDto {
  id: string;
  name: string;
  variant: string;
  totalWeeks: number;
  currentWeek: number;
  currentBlock: number;
  currentDay: number;
  daysPerWeek: number;
  completedDaysInCurrentWeek: number[];
  isWeekComplete: boolean;
  blockSequence: number[];
  status: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  exerciseCount: number;
  isActive: boolean;
}

// Workout completion types
export interface CompletedSetRequest {
  setNumber: number;
  weight: number;
  weightUnit: WeightUnit;
  actualReps: number;
  wasAmrap: boolean;
}

/** How a cardio exercise went: Easy / Solid / Brutal (RPE 4 / 7 / 9), stopped early, pain, and what was done. */
export interface CardioResultRequest {
  finished: boolean;
  effort: "Easy" | "Solid" | "Brutal";
  pain: "None" | "Niggle" | "Stop";
  painArea?: string | null;
  totalSeconds: number;
  workMinutes?: number | null;
  /** Level (stairs) or incline % (walk). */
  intensity?: number | null;
  /** km/h (walk). */
  speed?: number | null;
  floors?: number | null;
}

export interface ExercisePerformanceRequest {
  exerciseId: string;
  /** Absent (or empty) for a cardio exercise, which sends `cardio` instead. */
  completedSets?: CompletedSetRequest[];
  /** Cardio exercises only. */
  cardio?: CardioResultRequest | null;
  /** The bonus set on one side (never part of completedSets, never affects progression). */
  extraSet?: CompletedSetRequest | null;
  /** When true, progression rules are skipped for this exercise (used for temporary substitutions) */
  wasTemporarySubstitution?: boolean;
}

export interface CompleteDayRequest {
  performances: ExercisePerformanceRequest[];
}

// ProgressionChangeDto used in CompleteDayResult (immediate post-session changes)
export interface ProgressionChangeSimpleDto {
  exerciseId: string;
  exerciseName: string;
  change: string;
}

// ProgressionChangeDto used in ExerciseHistoryDto (historical tracking with timestamps)
// Note: Consumer code expects this as "ProgressionChangeDto" for backward compatibility
export interface ProgressionChangeDto {
  occurredAt: string;
  weekNumber: number;
  oldProgressionType?: string;
  newProgressionType?: string;
  reason?: string;
}

export type ProgressionOutcome = 'Success' | 'Maintained' | 'Failed' | 'Deload';

export interface CompleteDayResult {
  workoutId: string;
  day: DayNumber;
  weekNumber: number;
  blockNumber: number;
  exercisesCompleted: number;
  progressionChanges: ProgressionChangeSimpleDto[];
  newCurrentWeek: number;
  newCurrentDay: number;
  weekProgressed: boolean;
  programComplete: boolean;
  isDeloadWeek: boolean;
  exercisesPendingWeightConfirmation: PendingWeightExerciseDto[];
  /** Plan for this day's next occurrence, computed server-side after progression. Empty when the program has no further week. */
  nextSessionExercises: NextSessionExerciseDto[];
}

export type WeightConfirmationType = "StartingWeight" | "WorkingWeight";

export interface PendingWeightExerciseDto {
  exerciseId: string;
  exerciseName: string;
  suggestedWeight: number;
  weightUnit: string;
  confirmationType: WeightConfirmationType;
}

export interface ProgressWeekResult {
  workoutId: string;
  previousWeek: number;
  newWeek: number;
  previousBlock: number;
  newBlock: number;
  isDeloadWeek: boolean;
  isProgramComplete: boolean;
}

// Next session preview (mirrors backend NextSessionExerciseDto)
export interface NextSessionExerciseDto {
  exerciseId: string;
  exerciseName: string;
  setCount: number;
  targetReps: number;
  weight: number;
  weightUnit: string;
  hasAmrap: boolean;
}

// Exercise update types
export interface ExerciseUpdateRequest {
  exerciseId: string;
  // For Linear progression
  trainingMaxValue?: number;
  trainingMaxUnit?: WeightUnit;
  // For RepsPerSet/MinimalSets progression
  weightValue?: number;
  weightUnit?: WeightUnit;
  reason?: string;
  /** Joins a superset (a number shared with the partner exercise on the same day). */
  supersetGroup?: number | null;
  /** True takes the exercise out of its superset. */
  clearSupersetGroup?: boolean;
  extraSetSide?: ExtraSetSide;
}

export interface UpdateExercisesRequest {
  updates: ExerciseUpdateRequest[];
}

export interface ExerciseUpdateResult {
  exerciseId: string;
  exerciseName: string;
  success: boolean;
  message?: string;
  previousValue?: string;
  newValue?: string;
}

export interface UpdateExercisesResult {
  workoutId: string;
  updatedCount: number;
  results: ExerciseUpdateResult[];
}

// Progression configuration for changing an exercise's progression type
export interface ProgressionConfigRequest {
  type: "Linear" | "RepsPerSet" | "MinimalSets";
  // Linear progression options
  trainingMaxValue?: number;
  trainingMaxUnit?: WeightUnit;
  useAmrap?: boolean;
  baseSetsPerExercise?: number;
  // RepsPerSet progression options
  repRangeMinimum?: number;

  repRangeMaximum?: number;
  targetSets?: number;
  startingSets?: number;
  currentSets?: number;
  startingWeight?: number;
  weightUnit?: WeightUnit;
  isUnilateral?: boolean;
  // MinimalSets progression options
  targetTotalReps?: number;
  minimumSets?: number;
  maximumSets?: number;
}

// Exercise substitution types
export interface SubstituteExerciseRequest {
  exerciseId: string;
  newExerciseName: string;
  newHevyExerciseTemplateId?: string;
  reason?: string;
  newProgressionConfig?: ProgressionConfigRequest;
}

export interface SubstituteExerciseResult {
  exerciseId: string;
  originalName: string;
  newName: string;
  success: boolean;
  progressionTypeChanged?: boolean;
  newProgressionType?: string;
  message?: string;
}

// === Workout History Types ===

export interface WorkoutHistoryResponse {
  workoutId: string;
  workoutName: string;
  variant: string;
  totalWeeks: number;
  currentWeek: number;
  currentBlock: number;
  daysPerWeek: number;
  startedAt?: string;
  totalWorkoutsCompleted: number;
  completedActivities: WorkoutActivityHistoryDto[];
  exerciseHistories: ExerciseHistoryDto[];
}

export interface WorkoutActivityHistoryDto {
  day: string;
  dayNumber: number;
  weekNumber: number;
  blockNumber: number;
  completedAt: string;
  isDeloadWeek: boolean;
  performances: ExercisePerformanceHistoryDto[];
}

export interface ExercisePerformanceHistoryDto {
  exerciseId: string;
  completedAt: string;
  completedSets: CompletedSetHistoryDto[];
  /** A cardio exercise's session in one line ("7 min @ level 6 · Easy"); null for lifts. */
  cardioSummary?: string | null;
}

export interface CompletedSetHistoryDto {
  setNumber: number;
  weight: number;
  weightUnit: string;
  actualReps: number;
  wasAmrap: boolean;
}

export interface CardioSettingsChangeDto {
  occurredAt: string;
  weekNumber: number;
  dayNumber: number;
  oldText: string;
  newText: string;
  reason?: string | null;
}

export interface ExerciseHistoryDto {
  exerciseId: string;
  name: string;
  progressionType: string;
  assignedDay: number;
  category: string;
  equipment: string;
  currentWeight: number;
  weightUnit: string;
  currentSets: number;
  targetSets: number;
  trainingMax?: number;
  weeklyHistory: WeeklyPerformanceDto[];
  progressionChanges?: ProgressionChangeDto[]; // History of progression type changes with timestamps
  /** Cardio exercises: level/incline/speed edits (old and new prescription text), oldest first. */
  settingsChanges?: CardioSettingsChangeDto[];
}

export interface WeeklyPerformanceDto {
  weekNumber: number;
  blockNumber: number;
  completedAt?: string;
  isDeloadWeek: boolean;
  totalVolume: number;
  averageWeight: number;
  totalReps: number;
  setsCompleted: number;
  amrapReps?: number;
  sets: CompletedSetHistoryDto[];
  /** A cardio exercise's session in one line ("7 min @ level 6 · Easy"); null for lifts. */
  cardioSummary?: string | null;
  // Progression state at this week (from ProgressionSnapshot)
  trainingMaxAtWeek?: number;
  trainingMaxUnitAtWeek?: string;
  weightAtWeek?: number;
  setCountAtWeek?: number;
  progressionTypeAtWeek?: string;
}
