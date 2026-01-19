import apiClient from "./apiClient";
import type {
  WorkoutDto,
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
   * Get the exercise library
   */
  getExerciseLibrary: async (): Promise<ExerciseLibrary> => {
    const response = await apiClient.get<ExerciseLibrary>(
      "/workouts/exercises/library"
    );
    return response.data;
  },
};
