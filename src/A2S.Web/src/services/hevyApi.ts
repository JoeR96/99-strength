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
  private apiKey: string | null = null;

  /**
   * Set the API key for authentication
   */
  setApiKey(apiKey: string) {
    this.apiKey = apiKey;
  }

  /**
   * Get the current API key
   */
  getApiKey(): string | null {
    return this.apiKey;
  }

  /**
   * Check if the API key is configured
   */
  isConfigured(): boolean {
    return this.apiKey !== null && this.apiKey.length > 0;
  }

  /**
   * Clear the API key
   */
  clearApiKey() {
    this.apiKey = null;
  }

  /**
   * Make an authenticated request to the Hevy API via backend proxy
   */
  private async request<T>(
    endpoint: string,
    options: { method?: string; body?: unknown } = {}
  ): Promise<T> {
    if (!this.apiKey) {
      throw new Error('Hevy API key not configured. Please set your API key in settings.');
    }

    const url = `${HEVY_PROXY_BASE_URL}${endpoint}`;
    const config = {
      headers: {
        'X-Hevy-Api-Key': this.apiKey,
      },
    };

    try {
      let response;
      if (options.method === 'POST') {
        response = await apiClient.post(url, options.body, config);
      } else if (options.method === 'PUT') {
        response = await apiClient.put(url, options.body, config);
      } else if (options.method === 'DELETE') {
        response = await apiClient.delete(url, config);
      } else {
        response = await apiClient.get(url, config);
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
      `/workouts?page=${page}&pageSize=${pageSize}`
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
   */
  async getRoutines(page: number = 1, pageSize: number = 10): Promise<HevyRoutinesResponse> {
    return this.request<HevyRoutinesResponse>(
      `/routines?page=${page}&pageSize=${pageSize}`
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
    await this.request<void>(`/routines/${routineId}`, {
      method: 'DELETE',
    });
  }

  /**
   * Get all routines (handles pagination)
   */
  async getAllRoutines(): Promise<HevyRoutine[]> {
    const allRoutines: HevyRoutine[] = [];
    let page = 1;
    let hasMore = true;

    while (hasMore) {
      const response = await this.getRoutines(page, 100);
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
      `/exercise_templates?page=${page}&pageSize=${pageSize}`
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
      `/routine_folders?page=${page}&pageSize=${clampedPageSize}`
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
    console.log('Creating routine folder with payload:', JSON.stringify(folder));
    const response = await this.request<{ routine_folder: HevyRoutineFolder } | HevyRoutineFolder>(
      '/routine_folders',
      {
        method: 'POST',
        body: folder,
      }
    );
    console.log('Create routine folder response:', JSON.stringify(response));
    // Handle both wrapped and unwrapped response formats
    if ('routine_folder' in response) {
      return response.routine_folder;
    }
    return response as HevyRoutineFolder;
  }

  // ==================== Utility Methods ====================

  /**
   * Validate the API key by making a test request
   */
  async validateApiKey(): Promise<boolean> {
    if (!this.apiKey) {
      console.log('Hevy validation: No API key set');
      return false;
    }

    try {
      console.log('Hevy validation: Calling backend proxy...');
      const response = await apiClient.get(`${HEVY_PROXY_BASE_URL}/validate`, {
        headers: {
          'X-Hevy-Api-Key': this.apiKey,
        },
      });
      console.log('Hevy validation response:', response.data);
      return response.data?.valid === true;
    } catch (error: unknown) {
      console.error('Hevy validation error:', error);
      // Re-throw to let the caller handle it
      throw error;
    }
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
