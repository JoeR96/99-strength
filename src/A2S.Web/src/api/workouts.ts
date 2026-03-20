import apiClient from "./apiClient";
import type {
  WorkoutDto,
  WorkoutSummaryDto,
  ExerciseLibrary,
  CreateWorkoutRequest,
  CompleteDayRequest,
  CompleteDayResult,
  ProgressWeekResult,
  DayNumber,
  UpdateExercisesRequest,
  UpdateExercisesResult,
  SubstituteExerciseRequest,
  SubstituteExerciseResult,
  WorkoutHistoryResponse,
} from "../types/workout";

export interface UndoCompletionResult {
  day: number;
  weekNumber: number;
  weekRolledBack: boolean;
  newCurrentWeek: number;
  newCurrentDay: number;
  message?: string;
}

/**
 * API client for workout operations
 */
export const workoutsApi = {
  /**
   * Get the current active workout
   */
  getCurrentWorkout: async (): Promise<WorkoutDto | null> => {
    try {
      const response = await apiClient.get<WorkoutDto>("/workouts/current");
      return response.data;
    } catch (error: any) {
      if (error.response?.status === 404) {
        return null; // No active workout
      }
      throw error;
    }
  },

  /**
   * Get all workouts for the current user
   */
  getAllWorkouts: async (): Promise<WorkoutSummaryDto[]> => {
    const response = await apiClient.get<WorkoutSummaryDto[]>("/workouts");
    return response.data;
  },

  /**
   * Create a new workout
   */
  createWorkout: async (
    request: CreateWorkoutRequest
  ): Promise<{ id: string }> => {
    const response = await apiClient.post<{ id: string }>(
      "/workouts",
      request
    );
    return response.data;
  },

  /**
   * Set a workout as active
   */
  setActiveWorkout: async (workoutId: string): Promise<void> => {
    await apiClient.post(`/workouts/${workoutId}/activate`);
  },

  /**
   * Delete a workout
   */
  deleteWorkout: async (workoutId: string): Promise<void> => {
    await apiClient.delete(`/workouts/${workoutId}`);
  },

  /**
   * Get the exercise library
   */
  getExerciseLibrary: async (): Promise<ExerciseLibrary> => {
    const response = await apiClient.get<ExerciseLibrary>(
      "/workouts/exercises/library"
    );
    return response.data;
  },

  /**
   * Complete a training day with exercise performances
   */
  completeDay: async (
    workoutId: string,
    day: DayNumber,
    request: CompleteDayRequest
  ): Promise<CompleteDayResult> => {
    const response = await apiClient.post<CompleteDayResult>(
      `/workouts/${workoutId}/days/${day}/complete`,
      request
    );
    return response.data;
  },

  /**
   * Progress to the next week
   */
  progressWeek: async (workoutId: string): Promise<ProgressWeekResult> => {
    const response = await apiClient.post<ProgressWeekResult>(
      `/workouts/${workoutId}/progress-week`
    );
    return response.data;
  },

  /**
   * Set the Hevy routine folder ID for a workout
   */
  setHevyFolderId: async (workoutId: string, folderId: string): Promise<void> => {
    await apiClient.put(`/workouts/${workoutId}/hevy-folder`, { folderId });
  },

  /**
   * Record that a routine was synced to Hevy for a specific week/day
   */
  setHevySyncedRoutine: async (
    workoutId: string,
    weekNumber: number,
    dayNumber: number,
    routineId: string
  ): Promise<void> => {
    await apiClient.post(`/workouts/${workoutId}/hevy-synced-routine`, {
      weekNumber,
      dayNumber,
      routineId,
    });
  },

  /**
   * Update exercises in a workout (training max, weight, etc.)
   */
  updateExercises: async (
    workoutId: string,
    request: UpdateExercisesRequest
  ): Promise<UpdateExercisesResult> => {
    const response = await apiClient.put<UpdateExercisesResult>(
      `/workouts/${workoutId}/exercises`,
      request
    );
    return response.data;
  },

  /**
   * Substitute an exercise permanently in the workout
   */
  substituteExercise: async (
    workoutId: string,
    request: SubstituteExerciseRequest
  ): Promise<SubstituteExerciseResult> => {
    const response = await apiClient.put<SubstituteExerciseResult>(
      `/workouts/${workoutId}/exercises/${request.exerciseId}/substitute`,
      request
    );
    return response.data;
  },

  /**
   * Remove an exercise from the workout permanently.
   */
  removeExercise: async (
    workoutId: string,
    exerciseId: string
  ): Promise<void> => {
    await apiClient.delete(`/workouts/${workoutId}/exercises/${exerciseId}`);
  },

  /**
   * Update the working weight for an accessory exercise (RepsPerSet or MinimalSets).
   * Linear progression exercises must use skip progression instead.
   */
  updateWorkingWeight: async (
    workoutId: string,
    exerciseId: string,
    newWeight: number,
    unit: 1 | 2, // 1 = Kilograms, 2 = Pounds
    reason?: string
  ): Promise<void> => {
    await apiClient.put(
      `/workouts/${workoutId}/exercises/${exerciseId}/working-weight`,
      {
        newWeight,
        unit,
        reason,
      }
    );
  },

  /**
   * Undo the last completed day, rolling back progress
   */
  undoLastCompletion: async (workoutId: string): Promise<UndoCompletionResult> => {
    const response = await apiClient.post<UndoCompletionResult>(
      `/workouts/${workoutId}/undo-last-completion`
    );
    return response.data;
  },

  /**
   * Update the block sequence for a workout
   */
  updateBlockSequence: async (
    workoutId: string,
    blockSequence: number[]
  ): Promise<{ workoutId: string; blockSequence: number[]; totalWeeks: number; currentWeek: number; currentBlock: number }> => {
    const response = await apiClient.put(
      `/workouts/${workoutId}/block-sequence`,
      { blockSequence }
    );
    return response.data;
  },

  /**
   * Get workout history including exercise progression data
   */
  getWorkoutHistory: async (workoutId?: string): Promise<WorkoutHistoryResponse | null> => {
    try {
      const params = workoutId ? `?id=${workoutId}` : '';
      const response = await apiClient.get<WorkoutHistoryResponse>(`/workouts/history${params}`);
      return response.data;
    } catch (error: any) {
      if (error.response?.status === 404) {
        return null;
      }
      throw error;
    }
  },

  /**
   * Get the planned workout for a specific week and day.
   * Returns calculated working weights, sets, and reps from the backend.
   * This is the single source of truth - use this instead of frontend calculations.
   */
  getWeekPlan: async (
    weekNumber: number,
    dayNumber: number,
    workoutId?: string
  ): Promise<WeekPlanDto> => {
    const params = workoutId ? `?id=${workoutId}` : '';
    const response = await apiClient.get<WeekPlanDto>(
      `/workouts/weeks/${weekNumber}/days/${dayNumber}/plan${params}`
    );
    return response.data;
  },
};

/**
 * Week plan response from the backend.
 * Contains all information needed to display and execute a workout day.
 */
export interface WeekPlanDto {
  workoutId: string;
  workoutName: string;
  weekNumber: number;
  dayNumber: number;
  blockNumber: number;
  isDeloadWeek: boolean;
  intensityPercentage: number;
  exercises: PlannedExerciseDto[];
}

export interface PlannedExerciseDto {
  exerciseId: string;
  name: string;
  category: string;
  equipment: string;
  orderInDay: number;
  hevyExerciseTemplateId: string;
  progressionType: string;
  plannedSets: PlannedSetDto[];
  metadata: PlannedExerciseMetadataDto;
}

export interface PlannedSetDto {
  setNumber: number;
  weightKg: number;
  weightLbs: number;
  originalUnit: string;
  targetReps: number;
  isAmrap: boolean;
}

export interface PlannedExerciseMetadataDto {
  trainingMaxValue?: number;
  trainingMaxUnit?: string;
  repRangeMinimum?: number;
  repRangeTarget?: number;
  repRangeMaximum?: number;
  isUnilateral?: boolean;
  targetTotalReps?: number;
  notes?: string;
}
