/**
 * Hevy API Types
 * Based on the official Hevy API specification
 * Documentation: https://api.hevyapp.com/docs/
 */

// Set types for workouts
export type HevySetType = 'warmup' | 'normal' | 'failure' | 'dropset';

// RPE values (Rate of Perceived Exertion)
export type HevyRPE = 6 | 7 | 7.5 | 8 | 8.5 | 9 | 9.5 | 10;

// Exercise types
export type HevyExerciseType =
  | 'weight_reps'
  | 'reps_only'
  | 'bodyweight_reps'
  | 'bodyweight_assisted_reps'
  | 'duration'
  | 'weight_duration'
  | 'distance_duration'
  | 'short_distance_weight';

// Equipment categories
export type HevyEquipmentCategory =
  | 'none'
  | 'barbell'
  | 'dumbbell'
  | 'kettlebell'
  | 'machine'
  | 'plate'
  | 'resistance_band'
  | 'suspension'
  | 'other';

// Muscle groups
export type HevyMuscleGroup =
  | 'abdominals'
  | 'shoulders'
  | 'biceps'
  | 'triceps'
  | 'forearms'
  | 'quadriceps'
  | 'hamstrings'
  | 'calves'
  | 'glutes'
  | 'abductors'
  | 'adductors'
  | 'lats'
  | 'upper_back'
  | 'traps'
  | 'lower_back'
  | 'chest'
  | 'cardio'
  | 'neck'
  | 'full_body'
  | 'other';

// Set schema for workouts
export interface HevyWorkoutSet {
  type: HevySetType;
  weight_kg?: number | null;
  reps?: number | null;
  distance_meters?: number | null;
  duration_seconds?: number | null;
  custom_metric?: number | null;
  rpe?: HevyRPE | null;
}

// Set schema for routines (includes rep_range)
export interface HevyRoutineSet {
  type: HevySetType;
  weight_kg?: number | null;
  reps?: number | null;
  distance_meters?: number | null;
  duration_seconds?: number | null;
  custom_metric?: number | null;
  rep_range?: {
    start?: number | null;
    end?: number | null;
  } | null;
}

// Exercise in a workout
export interface HevyWorkoutExercise {
  exercise_template_id: string;
  superset_id?: number | null;
  notes?: string | null;
  sets: HevyWorkoutSet[];
}

// Exercise in a routine
export interface HevyRoutineExercise {
  exercise_template_id: string;
  superset_id?: number | null;
  rest_seconds?: number | null;
  notes?: string | null;
  sets: HevyRoutineSet[];
}

// Create workout request
export interface HevyCreateWorkoutRequest {
  workout: {
    title: string;
    description?: string | null;
    start_time: string; // ISO 8601
    end_time: string; // ISO 8601
    is_private?: boolean;
    exercises: HevyWorkoutExercise[];
  };
}

// Create routine request
export interface HevyCreateRoutineRequest {
  routine: {
    title: string;
    folder_id?: number | null;
    notes?: string;
    exercises: HevyRoutineExercise[];
  };
}

// Create exercise template request
export interface HevyCreateExerciseTemplateRequest {
  exercise: {
    title: string;
    exercise_type: HevyExerciseType;
    equipment_category: HevyEquipmentCategory;
    muscle_group: HevyMuscleGroup;
    other_muscles?: HevyMuscleGroup[];
  };
}

// Create routine folder request - note: API expects wrapper object
export interface HevyCreateRoutineFolderRequest {
  routine_folder: {
    title: string;
  };
}

// Response types
export interface HevyWorkout {
  id: string;
  title: string;
  description?: string | null;
  start_time: string;
  end_time: string;
  is_private: boolean;
  exercises: HevyWorkoutExerciseResponse[];
  created_at: string;
  updated_at: string;
}

export interface HevyWorkoutExerciseResponse {
  index: number;
  exercise_template_id: string;
  superset_id?: number | null;
  notes?: string | null;
  sets: HevyWorkoutSetResponse[];
}

export interface HevyWorkoutSetResponse {
  index: number;
  type: HevySetType;
  weight_kg?: number | null;
  reps?: number | null;
  distance_meters?: number | null;
  duration_seconds?: number | null;
  custom_metric?: number | null;
  rpe?: HevyRPE | null;
}

export interface HevyRoutine {
  id: string;
  title: string;
  folder_id?: number | null;
  notes?: string;
  exercises: HevyRoutineExerciseResponse[];
  created_at: string;
  updated_at: string;
}

export interface HevyRoutineExerciseResponse {
  index: number;
  exercise_template_id: string;
  superset_id?: number | null;
  rest_seconds?: number | null;
  notes?: string | null;
  sets: HevyRoutineSetResponse[];
}

export interface HevyRoutineSetResponse {
  index: number;
  type: HevySetType;
  weight_kg?: number | null;
  reps?: number | null;
  distance_meters?: number | null;
  duration_seconds?: number | null;
  custom_metric?: number | null;
  rep_range?: {
    start?: number | null;
    end?: number | null;
  } | null;
}

export interface HevyExerciseTemplate {
  id: string;
  title: string;
  exercise_type: HevyExerciseType;
  equipment_category: HevyEquipmentCategory;
  muscle_group: HevyMuscleGroup;
  other_muscles?: HevyMuscleGroup[];
  is_custom: boolean;
}

export interface HevyRoutineFolder {
  id: number;
  title: string;
  created_at: string;
  updated_at: string;
}

// Paginated response wrapper
export interface HevyPaginatedResponse<T> {
  page: number;
  page_count: number;
  [key: string]: T[] | number; // The actual data array key varies
}

export interface HevyWorkoutsResponse extends HevyPaginatedResponse<HevyWorkout> {
  workouts: HevyWorkout[];
}

export interface HevyRoutinesResponse extends HevyPaginatedResponse<HevyRoutine> {
  routines: HevyRoutine[];
}

export interface HevyExerciseTemplatesResponse extends HevyPaginatedResponse<HevyExerciseTemplate> {
  exercise_templates: HevyExerciseTemplate[];
}

export interface HevyRoutineFoldersResponse extends HevyPaginatedResponse<HevyRoutineFolder> {
  routine_folders: HevyRoutineFolder[];
}

export interface HevyWorkoutCountResponse {
  workout_count: number;
}

// Error response
export interface HevyErrorResponse {
  error: string;
  message?: string;
}
