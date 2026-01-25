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
} from "../types/workout";

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
};
