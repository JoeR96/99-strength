import apiClient from "./apiClient";
import type {
  WorkoutDto,
  WorkoutSummaryDto,
  ExerciseLibrary,
  CreateWorkoutRequest,
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
};
