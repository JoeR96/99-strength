import type {
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  CompleteDayResult,
  WeightUnit,
  DayNumber,
  WorkoutDto,
  ProgressionConfigRequest,
} from "@/types/workout";
import type { PulledWorkoutData, DetectedSubstitution, WeightDiscrepancy, MissingExercise } from "@/services/hevySyncService";

export interface SetEntry {
  setNumber: number;
  weight: number;
  reps: number;
  isAmrap: boolean;
  completed: boolean;
}

export interface TemporarySubstitution {
  originalExerciseId: string;
  originalName: string;
  substituteName: string;
}

export interface ExerciseEntry {
  exercise: ExerciseDto;
  sets: SetEntry[];
  targetSets: number;
  targetReps: number;
  targetWeight: number;
  weightUnit: string;
  isAmrapExercise: boolean;
  skipProgression?: boolean;
}

export interface SavedSetProgress {
  setNumber: number;
  weight: number;
  reps: number;
  isAmrap: boolean;
  completed: boolean;
}

export interface SavedExerciseProgress {
  exerciseId: string;
  sets: SavedSetProgress[];
}

export interface SavedWorkoutProgress {
  workoutId: string;
  dayNumber: number;
  weekNumber: number;
  savedAt: string;
  exercises: SavedExerciseProgress[];
}

export type {
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  CompleteDayResult,
  WeightUnit,
  DayNumber,
  WorkoutDto,
  ProgressionConfigRequest,
  PulledWorkoutData,
  DetectedSubstitution,
  WeightDiscrepancy,
  MissingExercise,
};
