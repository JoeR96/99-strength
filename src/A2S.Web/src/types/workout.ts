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
  totalWeeks: number;
  startDate: string;
  exercises: ExerciseDto[];
}

export interface ExerciseDto {
  id: string;
  name: string;
  category: ExerciseCategory;
  equipment: EquipmentType;
  assignedDay: DayNumber;
  orderInDay: number;
  progression: ExerciseProgressionDto;
}

export interface ExerciseProgressionDto {
  type: "Linear" | "RepsPerSet";
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
  category: ExerciseCategory;
  progressionType: "Linear" | "RepsPerSet";
  assignedDay: DayNumber;
  orderInDay: number;
  // For Linear progression
  trainingMaxValue?: number;
  trainingMaxUnit?: WeightUnit;
  // For RepsPerSet progression
  startingWeight?: number;
  weightUnit?: WeightUnit;
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
  status: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  exerciseCount: number;
  isActive: boolean;
}
