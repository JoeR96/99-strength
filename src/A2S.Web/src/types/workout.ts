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
  NotStarted: 0,
  InProgress: 1,
  Paused: 2,
  Completed: 3,
} as const;
export type WorkoutStatus = typeof WorkoutStatus[keyof typeof WorkoutStatus];

export interface RepRange {
  minimum: number;
  target: number;
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
  currentSetCount: number;
  targetSets: number;
  currentWeight: number;
  weightUnit: string; // Backend returns string "Kilograms" or "Pounds"
}

// Request DTOs
export interface CreateExerciseRequest {
  templateName: string;
  hevyExerciseTemplateId?: string; // Optional - defaults to empty string if not provided
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
  // For MinimalSets progression
  targetTotalReps?: number;
}

export interface CreateWorkoutRequest {
  name: string;
  variant: ProgramVariant;
  totalWeeks: number;
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
