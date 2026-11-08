/**
 * Hevy API Service
 * Handles all communication with the Hevy fitness tracking API via backend proxy
 * Documentation: https://api.hevyapp.com/docs/
 */

import { apiClient } from '@/api/apiClient';
import type {
  HevyCreateWorkoutRequest,
  HevyCreateRoutineRequest,
  HevyCreateExerciseTemplateRequest,
  HevyCreateRoutineFolderRequest,
  HevyWorkout,
  HevyRoutine,
  HevyExerciseTemplate,
  HevyRoutineFolder,
  HevyWorkoutsResponse,
  HevyRoutinesResponse,
  HevyExerciseTemplatesResponse,
  HevyRoutineFoldersResponse,
  HevyWorkoutCountResponse,
} from '@/types/hevy';

// Use backend proxy to avoid CORS issues
const HEVY_PROXY_BASE_URL = '/hevy';

class HevyApiService {
  /** Mirrors whether the server has a key stored for this user (set by HevyProvider). */
  private configured = false;

  setConfigured(configured: boolean) {
    this.configured = configured;
  }

  /**
   * Whether the server holds a Hevy API key for this user
   */
  isConfigured(): boolean {
    return this.configured;
  }

  /**
   * Make a request to the Hevy API via the backend proxy (which attaches the stored key)
   */
  private async request<T>(
    endpoint: string,
    options: { method?: string; body?: unknown } = {}
  ): Promise<T> {
    if (!this.configured) {
      throw new Error('Hevy API key not configured. Please set your API key in settings.');
    }

    // The server resolves the user's encrypted key; nothing secret travels with the request.
    const url = `${HEVY_PROXY_BASE_URL}${endpoint}`;

    try {
      let response;
      if (options.method === 'POST') {
        response = await apiClient.post(url, options.body);
      } else if (options.method === 'PUT') {
        response = await apiClient.put(url, options.body);
      } else if (options.method === 'DELETE') {
        response = await apiClient.delete(url);
      } else {
        response = await apiClient.get(url);
      }
      return response.data;
    } catch (error: unknown) {
      if (error && typeof error === 'object' && 'response' in error) {
        const axiosError = error as { response?: { data?: { error?: string; message?: string }; status?: number; statusText?: string } };
        const errorData = axiosError.response?.data;
        throw new Error(
          errorData?.message || errorData?.error || `Hevy API error: ${axiosError.response?.status} ${axiosError.response?.statusText}`
        );
      }
      throw error;
    }
  }

  // ==================== Workouts ====================

  /**
   * Get paginated list of workouts
   */
  async getWorkouts(page: number = 1, pageSize: number = 10): Promise<HevyWorkoutsResponse> {
    return this.request<HevyWorkoutsResponse>(
      `/workouts?page=${page}&page_size=${pageSize}`
    );
  }

  /**
   * Get a single workout by ID
   */
  async getWorkout(workoutId: string): Promise<HevyWorkout> {
    const response = await this.request<{ workout: HevyWorkout }>(
      `/workouts/${workoutId}`
    );
    return response.workout;
  }

  /**
   * Create a new workout
   */
  async createWorkout(workout: HevyCreateWorkoutRequest): Promise<HevyWorkout> {
    const response = await this.request<{ workout: HevyWorkout }>('/workouts', {
      method: 'POST',
      body: workout,
    });
    return response.workout;
  }

  /**
   * Update an existing workout
   */
  async updateWorkout(
    workoutId: string,
    workout: HevyCreateWorkoutRequest
  ): Promise<HevyWorkout> {
    const response = await this.request<{ workout: HevyWorkout }>(
      `/workouts/${workoutId}`,
      {
        method: 'PUT',
        body: workout,
      }
    );
    return response.workout;
  }

  /**
   * Get total workout count
   */
  async getWorkoutCount(): Promise<number> {
    const response = await this.request<HevyWorkoutCountResponse>('/workouts/count');
    return response.workout_count;
  }

  // ==================== Routines ====================

  /**
   * Get paginated list of routines
   * Note: Hevy API max pageSize for routines is 10
   */
  async getRoutines(page: number = 1, pageSize: number = 10): Promise<HevyRoutinesResponse> {
    // Clamp pageSize to max allowed by Hevy API
    const clampedPageSize = Math.min(pageSize, 10);
    return this.request<HevyRoutinesResponse>(
      `/routines?page=${page}&page_size=${clampedPageSize}`
    );
  }

  /**
   * Get a single routine by ID
   */
  async getRoutine(routineId: string): Promise<HevyRoutine> {
    const response = await this.request<{ routine: HevyRoutine }>(
      `/routines/${routineId}`
    );
    return response.routine;
  }

  /**
   * Create a new routine
   */
  async createRoutine(routine: HevyCreateRoutineRequest): Promise<HevyRoutine> {
    const response = await this.request<{ routine: HevyRoutine }>('/routines', {
      method: 'POST',
      body: routine,
    });
    return response.routine;
  }

  /**
   * Update an existing routine
   */
  async updateRoutine(
    routineId: string,
    routine: HevyCreateRoutineRequest
  ): Promise<HevyRoutine> {
    const response = await this.request<{ routine: HevyRoutine }>(
      `/routines/${routineId}`,
      {
        method: 'PUT',
        body: routine,
      }
    );
    return response.routine;
  }

  /**
   * Delete a routine
   */
  async deleteRoutine(routineId: string): Promise<void> {
    const encodedId = encodeURIComponent(routineId);
    await this.request<void>(`/routines/${encodedId}`, {
      method: 'DELETE',
    });
  }

  /**
   * Get all routines (handles pagination)
   * Note: Hevy API max pageSize for routines is 10
   */
  async getAllRoutines(): Promise<HevyRoutine[]> {
    const allRoutines: HevyRoutine[] = [];
    let page = 1;
    let hasMore = true;

    while (hasMore) {
      const response = await this.getRoutines(page, 10);
      allRoutines.push(...response.routines);
      hasMore = page < response.page_count;
      page++;
    }

    return allRoutines;
  }

  // ==================== Exercise Templates ====================

  /**
   * Get paginated list of exercise templates
   */
  async getExerciseTemplates(
    page: number = 1,
    pageSize: number = 100
  ): Promise<HevyExerciseTemplatesResponse> {
    return this.request<HevyExerciseTemplatesResponse>(
      `/exercise_templates?page=${page}&page_size=${pageSize}`
    );
  }

  /**
   * Get all exercise templates (handles pagination)
   */
  async getAllExerciseTemplates(): Promise<HevyExerciseTemplate[]> {
    const allTemplates: HevyExerciseTemplate[] = [];
    let page = 1;
    let hasMore = true;

    while (hasMore) {
      const response = await this.getExerciseTemplates(page, 100);
      allTemplates.push(...response.exercise_templates);
      hasMore = page < response.page_count;
      page++;
    }

    return allTemplates;
  }

  /**
   * Get a single exercise template by ID
   */
  async getExerciseTemplate(templateId: string): Promise<HevyExerciseTemplate> {
    const response = await this.request<{ exercise_template: HevyExerciseTemplate }>(
      `/exercise_templates/${templateId}`
    );
    return response.exercise_template;
  }

  /**
   * Create a custom exercise template
   */
  async createExerciseTemplate(
    template: HevyCreateExerciseTemplateRequest
  ): Promise<HevyExerciseTemplate> {
    const response = await this.request<{ exercise_template: HevyExerciseTemplate }>(
      '/exercise_templates',
      {
        method: 'POST',
        body: template,
      }
    );
    return response.exercise_template;
  }

  // ==================== Routine Folders ====================

  /**
   * Get paginated list of routine folders
   * Note: Hevy API max pageSize for routine_folders is 10
   */
  async getRoutineFolders(
    page: number = 1,
    pageSize: number = 10
  ): Promise<HevyRoutineFoldersResponse> {
    // Clamp pageSize to max allowed by Hevy API
    const clampedPageSize = Math.min(pageSize, 10);
    return this.request<HevyRoutineFoldersResponse>(
      `/routine_folders?page=${page}&page_size=${clampedPageSize}`
    );
  }

  /**
   * Get a single routine folder by ID
   */
  async getRoutineFolder(folderId: number): Promise<HevyRoutineFolder> {
    const response = await this.request<{ routine_folder: HevyRoutineFolder }>(
      `/routine_folders/${folderId}`
    );
    return response.routine_folder;
  }

  /**
   * Create a new routine folder
   */
  async createRoutineFolder(
    folder: HevyCreateRoutineFolderRequest
  ): Promise<HevyRoutineFolder> {
    const response = await this.request<{ routine_folder: HevyRoutineFolder } | HevyRoutineFolder>(
      '/routine_folders',
      {
        method: 'POST',
        body: folder,
      }
    );
    // Handle both wrapped and unwrapped response formats
    if ('routine_folder' in response) {
      return response.routine_folder;
    }
    return response as HevyRoutineFolder;
  }

  // ==================== Utility Methods ====================

  /**
   * Ask the server to validate the stored key against Hevy
   */
  async validateApiKey(): Promise<boolean> {
    const response = await apiClient.get(`${HEVY_PROXY_BASE_URL}/validate`);
    return response.data?.valid === true;
  }

  /**
   * Find an exercise template by name (case-insensitive)
   */
  async findExerciseTemplateByName(name: string): Promise<HevyExerciseTemplate | null> {
    const templates = await this.getAllExerciseTemplates();
    const normalizedName = name.toLowerCase().trim();
    return templates.find(
      (t) => t.title.toLowerCase().trim() === normalizedName
    ) || null;
  }

  /**
   * Find or create an exercise template
   */
  async findOrCreateExerciseTemplate(
    name: string,
    exerciseType: HevyCreateExerciseTemplateRequest['exercise']['exercise_type'],
    equipmentCategory: HevyCreateExerciseTemplateRequest['exercise']['equipment_category'],
    muscleGroup: HevyCreateExerciseTemplateRequest['exercise']['muscle_group']
  ): Promise<HevyExerciseTemplate> {
    const existing = await this.findExerciseTemplateByName(name);
    if (existing) {
      return existing;
    }

    return this.createExerciseTemplate({
      exercise: {
        title: name,
        exercise_type: exerciseType,
        equipment_category: equipmentCategory,
        muscle_group: muscleGroup,
      },
    });
  }
}

// Export singleton instance
export const hevyApi = new HevyApiService();

// Also export the class for testing
export { HevyApiService };
