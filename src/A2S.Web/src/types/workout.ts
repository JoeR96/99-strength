/**
 * Workout and Exercise types matching backend DTOs
 */

export const WeightUnit = {
  Kilograms: 1,
  Pounds: 2,
} as const;
export type WeightUnit = typeof WeightUnit[keyof typeof WeightUnit];

// DEPRECATED: Keep for backend compatibility but no longer used in UI
export const ExerciseCategory = {
  MainLift: 1,
  Auxiliary: 2,
  Accessory: 3,
} as const;
export type ExerciseCategory = typeof ExerciseCategory[keyof typeof ExerciseCategory];

export const A2SProgressionType = {
  Linear: 'Linear',
  RepsPerSet: 'RepsPerSet',
} as const;
export type A2SProgressionType = typeof A2SProgressionType[keyof typeof A2SProgressionType];

export const EquipmentType = {
  Barbell: 0,
  Dumbbell: 1,
  Cable: 2,
  Machine: 3,
  Bodyweight: 4,
  SmithMachine: 5,
} as const;
export type EquipmentType = typeof EquipmentType[keyof typeof EquipmentType];

export const DayNumber = {
  Day1: 1,
  Day2: 2,
  Day3: 3,
  Day4: 4,
  Day5: 5,
  Day6: 6,
} as const;
export type DayNumber = typeof DayNumber[keyof typeof DayNumber];

export const ProgramVariant = {
  FourDay: 4,
  FiveDay: 5,
  SixDay: 6,
} as const;
export type ProgramVariant = typeof ProgramVariant[keyof typeof ProgramVariant];

export const WorkoutStatus = {
  NotStarted: 1,
  Active: 2,
  Paused: 3,
  Completed: 4,
} as const;
export type WorkoutStatus = typeof WorkoutStatus[keyof typeof WorkoutStatus];

export interface RepRange {
  minimum: number;
  maximum: number;
}

// Exercise template from library (no day/order/category/progression)
export interface ExerciseTemplate {
  name: string;
  equipment: EquipmentType;
  defaultRepRange?: RepRange;
  defaultSets?: number;
  description: string;
}

// Exercise library response
export interface ExerciseLibrary {
  templates: ExerciseTemplate[];
}

// User's configured exercise (template + configuration)
export interface SelectedExercise {
  id: string; // Unique ID for this selection (for React keys and DnD)
  hevyExerciseTemplateId: string; // Hevy exercise template ID for syncing
  template: ExerciseTemplate;
  category: ExerciseCategory; // Keep for backend compatibility
  progressionType: A2SProgressionType;
  assignedDay: DayNumber;
  orderInDay: number;
  // Hypertrophy progression config
  trainingMax?: TrainingMax;
  isPrimary?: boolean; // true = Primary lift, false = Auxiliary lift (both use AMRAP)
  baseSetsPerExercise?: number;
  // RepsPerSet progression config
  repRange?: RepRange;
  currentSets?: number;
  targetSets?: number;
  startingWeight?: number;
  weightUnit?: WeightUnit;
  isUnilateral?: boolean;
}

// DEPRECATED: For backwards compatibility with existing stories
// TODO: Remove after updating all stories
export interface ExerciseDefinition {
  name: string;
  equipment: EquipmentType;
  suggestedDay?: DayNumber;
  suggestedOrder?: number;
  defaultRepRange?: RepRange;
  defaultSets?: number;
  description: string;
}

export const ProgressionType = {
  Linear: 'Linear',
  RepsPerSet: 'RepsPerSet',
} as const;
export type ProgressionType = typeof ProgressionType[keyof typeof ProgressionType];

export interface TrainingMax {
  value: number;
  unit: WeightUnit;
}

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
  // Hevy integration
  hevyRoutineFolderId?: string;
  hevySyncedRoutines?: Record<string, string>;
}

export interface ExerciseDto {
  id: string;
  name: string;
  category: ExerciseCategory;
  equipment: EquipmentType;
  assignedDay: DayNumber;
  orderInDay: number;
  hevyExerciseTemplateId: string;
  progression: ExerciseProgressionDto;
}

export interface ExerciseProgressionDto {
  type: "Linear" | "RepsPerSet" | "MinimalSets";
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

// MinimalSets progression DTO
export interface MinimalSetsProgressionDto extends ExerciseProgressionDto {
  type: "MinimalSets";
  currentWeight: number;
  weightUnit: string;
  targetTotalReps: number;
  currentSetCount: number;
  minimumSets: number;
  maximumSets: number;
}

// Workout completion types
export interface CompletedSetRequest {
  setNumber: number;
  weight: number;
  weightUnit: WeightUnit;
  actualReps: number;
  wasAmrap: boolean;
}

export interface ExercisePerformanceRequest {
  exerciseId: string;
  completedSets: CompletedSetRequest[];
  /** When true, progression rules are skipped for this exercise (used for temporary substitutions) */
  wasTemporarySubstitution?: boolean;
}

export interface CompleteDayRequest {
  performances: ExercisePerformanceRequest[];
}

export interface ProgressionChangeDto {
  exerciseId: string;
  exerciseName: string;
  progressionType: string;
  change: string;
  previousValue?: string;
  newValue?: string;
}

export type ProgressionOutcome = 'Success' | 'Maintained' | 'Failed' | 'Deload';

export interface CompleteDayResult {
  workoutId: string;
  day: DayNumber;
  weekNumber: number;
  blockNumber: number;
  exercisesCompleted: number;
  progressionChanges: ProgressionChangeDto[];
  newCurrentWeek: number;
  newCurrentDay: number;
  weekProgressed: boolean;
  programComplete: boolean;
  isDeloadWeek: boolean;
  exercisesPendingWeightConfirmation: PendingWeightExerciseDto[];
}

export interface PendingWeightExerciseDto {
  exerciseId: string;
  exerciseName: string;
  suggestedWeight: number;
  weightUnit: string;
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

// Next session preview
export interface NextSessionExercise {
  id: string;
  name: string;
  progressionType: string;
  sets: number;
  reps: string;
  weight: string;
  outcome: ProgressionOutcome;
  changeDescription: string;
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
  // For RepsPerSet progression - unilateral toggle
  isUnilateral?: boolean;
  reason?: string;
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
}

export interface CompletedSetHistoryDto {
  setNumber: number;
  weight: number;
  weightUnit: string;
  actualReps: number;
  wasAmrap: boolean;
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
  progressionChanges?: ProgressionChangeDto[];
}

export interface ProgressionChangeDto {
  occurredAt: string;
  weekNumber: number;
  oldProgressionType?: string;
  newProgressionType?: string;
  reason?: string;
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
  // Progression state at this week (from ProgressionSnapshot)
  trainingMaxAtWeek?: number;
  trainingMaxUnitAtWeek?: string;
  weightAtWeek?: number;
  setCountAtWeek?: number;
  progressionTypeAtWeek?: string;
}
