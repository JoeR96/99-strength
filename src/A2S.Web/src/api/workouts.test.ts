/**
 * Workouts API Tests
 * Tests for the workoutsApi service layer
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { workoutsApi } from './workouts';
import apiClient from './apiClient';
import type { WorkoutDto, WorkoutSummaryDto, CompleteDayRequest } from '../types/workout';

// Mock the apiClient
vi.mock('./apiClient', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

// Mock workout data
const mockWorkout: WorkoutDto = {
  id: 'workout-123',
  name: 'Test A2S Program',
  variant: 4,
  status: 2,
  currentWeek: 1,
  currentBlock: 1,
  currentDay: 1,
  daysPerWeek: 4,
  completedDaysInCurrentWeek: [],
  isWeekComplete: false,
  totalWeeks: 21,
  startDate: '2024-01-01',
  createdAt: '2024-01-01',
  exerciseCount: 8,
  exercises: [],
};

const mockWorkoutSummary: WorkoutSummaryDto = {
  id: 'workout-123',
  name: 'Test A2S Program',
  variant: '4',
  status: 'Active',
  currentWeek: 1,
  currentBlock: 1,
  currentDay: 1,
  daysPerWeek: 4,
  completedDaysInCurrentWeek: [],
  isWeekComplete: false,
  totalWeeks: 21,
  createdAt: '2024-01-01',
  exerciseCount: 8,
  isActive: true,
};

describe('workoutsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getCurrentWorkout', () => {
    it('should fetch current workout successfully', async () => {
      vi.mocked(apiClient.get).mockResolvedValue({ data: mockWorkout });

      const result = await workoutsApi.getCurrentWorkout();

      expect(apiClient.get).toHaveBeenCalledWith('/workouts/current');
      expect(result).toEqual(mockWorkout);
    });

    it('should return null when no active workout (404)', async () => {
      const error = { response: { status: 404 } };
      vi.mocked(apiClient.get).mockRejectedValue(error);

      const result = await workoutsApi.getCurrentWorkout();

      expect(result).toBeNull();
    });

    it('should throw error for other HTTP errors', async () => {
      const error = { response: { status: 500 }, message: 'Server error' };
      vi.mocked(apiClient.get).mockRejectedValue(error);

      await expect(workoutsApi.getCurrentWorkout()).rejects.toEqual(error);
    });
  });

  describe('getAllWorkouts', () => {
    it('should fetch all workouts', async () => {
      const mockWorkouts = [mockWorkoutSummary];
      vi.mocked(apiClient.get).mockResolvedValue({ data: mockWorkouts });

      const result = await workoutsApi.getAllWorkouts();

      expect(apiClient.get).toHaveBeenCalledWith('/workouts');
      expect(result).toEqual(mockWorkouts);
    });
  });

  describe('createWorkout', () => {
    it('should create a new workout', async () => {
      const request = { name: 'New Program', variant: 4 as const, totalWeeks: 21 };
      const response = { id: 'new-workout-id' };
      vi.mocked(apiClient.post).mockResolvedValue({ data: response });

      const result = await workoutsApi.createWorkout(request);

      expect(apiClient.post).toHaveBeenCalledWith('/workouts', request);
      expect(result).toEqual(response);
    });

    it('should include exercises in request if provided', async () => {
      const request = {
        name: 'New Program',
        variant: 4 as const,
        totalWeeks: 21,
        exercises: [
          {
            templateName: 'Squat',
            category: 1 as const,
            progressionType: 'Linear' as const,
            assignedDay: 1 as const,
            orderInDay: 1,
            trainingMaxValue: 100,
            trainingMaxUnit: 1 as const,
          },
        ],
      };
      vi.mocked(apiClient.post).mockResolvedValue({ data: { id: 'new-id' } });

      await workoutsApi.createWorkout(request);

      expect(apiClient.post).toHaveBeenCalledWith('/workouts', request);
    });
  });

  describe('setActiveWorkout', () => {
    it('should activate a workout', async () => {
      vi.mocked(apiClient.post).mockResolvedValue({ data: undefined });

      await workoutsApi.setActiveWorkout('workout-123');

      expect(apiClient.post).toHaveBeenCalledWith('/workouts/workout-123/activate');
    });
  });

  describe('deleteWorkout', () => {
    it('should delete a workout', async () => {
      vi.mocked(apiClient.delete).mockResolvedValue({ data: undefined });

      await workoutsApi.deleteWorkout('workout-123');

      expect(apiClient.delete).toHaveBeenCalledWith('/workouts/workout-123');
    });
  });

  describe('getExerciseLibrary', () => {
    it('should fetch exercise library', async () => {
      const mockLibrary = {
        templates: [
          { name: 'Squat', equipment: 0, description: 'Barbell squat' },
        ],
      };
      vi.mocked(apiClient.get).mockResolvedValue({ data: mockLibrary });

      const result = await workoutsApi.getExerciseLibrary();

      expect(apiClient.get).toHaveBeenCalledWith('/workouts/exercises/library');
      expect(result).toEqual(mockLibrary);
    });
  });

  describe('completeDay', () => {
    it('should complete a training day', async () => {
      const request: CompleteDayRequest = {
        performances: [
          {
            exerciseId: 'ex-1',
            completedSets: [
              { setNumber: 1, weight: 100, weightUnit: 1, actualReps: 10, wasAmrap: false },
            ],
          },
        ],
      };
      const response = {
        workoutId: 'workout-123',
        day: 1,
        weekNumber: 1,
        blockNumber: 1,
        exercisesCompleted: 1,
        progressionChanges: [],
        newCurrentWeek: 1,
        newCurrentDay: 2,
        weekProgressed: false,
        programComplete: false,
        isDeloadWeek: false,
      };
      vi.mocked(apiClient.post).mockResolvedValue({ data: response });

      const result = await workoutsApi.completeDay('workout-123', 1, request);

      expect(apiClient.post).toHaveBeenCalledWith(
        '/workouts/workout-123/days/1/complete',
        request
      );
      expect(result).toEqual(response);
    });

    it('should handle temporary substitutions in request', async () => {
      const request: CompleteDayRequest = {
        performances: [
          {
            exerciseId: 'ex-1',
            completedSets: [
              { setNumber: 1, weight: 100, weightUnit: 1, actualReps: 10, wasAmrap: false },
            ],
            wasTemporarySubstitution: true, // Skip progression for this exercise
          },
        ],
      };
      vi.mocked(apiClient.post).mockResolvedValue({ data: {} });

      await workoutsApi.completeDay('workout-123', 1, request);

      expect(apiClient.post).toHaveBeenCalledWith(
        '/workouts/workout-123/days/1/complete',
        expect.objectContaining({
          performances: expect.arrayContaining([
            expect.objectContaining({ wasTemporarySubstitution: true }),
          ]),
        })
      );
    });
  });

  describe('progressWeek', () => {
    it('should progress to next week', async () => {
      const response = {
        workoutId: 'workout-123',
        previousWeek: 1,
        newWeek: 2,
        previousBlock: 1,
        newBlock: 1,
        isDeloadWeek: false,
        isProgramComplete: false,
      };
      vi.mocked(apiClient.post).mockResolvedValue({ data: response });

      const result = await workoutsApi.progressWeek('workout-123');

      expect(apiClient.post).toHaveBeenCalledWith('/workouts/workout-123/progress-week');
      expect(result).toEqual(response);
    });
  });

  describe('setHevyFolderId', () => {
    it('should set Hevy folder ID', async () => {
      vi.mocked(apiClient.put).mockResolvedValue({ data: undefined });

      await workoutsApi.setHevyFolderId('workout-123', 'folder-456');

      expect(apiClient.put).toHaveBeenCalledWith(
        '/workouts/workout-123/hevy-folder',
        { folderId: 'folder-456' }
      );
    });
  });

  describe('setHevySyncedRoutine', () => {
    it('should record synced routine', async () => {
      vi.mocked(apiClient.post).mockResolvedValue({ data: undefined });

      await workoutsApi.setHevySyncedRoutine('workout-123', 1, 2, 'routine-789');

      expect(apiClient.post).toHaveBeenCalledWith(
        '/workouts/workout-123/hevy-synced-routine',
        {
          weekNumber: 1,
          dayNumber: 2,
          routineId: 'routine-789',
        }
      );
    });
  });

  describe('updateExercises', () => {
    it('should update exercises', async () => {
      const request = {
        updates: [
          {
            exerciseId: 'ex-1',
            trainingMaxValue: 110,
            trainingMaxUnit: 1 as const,
          },
        ],
      };
      const response = {
        workoutId: 'workout-123',
        updatedCount: 1,
        results: [
          {
            exerciseId: 'ex-1',
            exerciseName: 'Squat',
            success: true,
            previousValue: '100kg',
            newValue: '110kg',
          },
        ],
      };
      vi.mocked(apiClient.put).mockResolvedValue({ data: response });

      const result = await workoutsApi.updateExercises('workout-123', request);

      expect(apiClient.put).toHaveBeenCalledWith(
        '/workouts/workout-123/exercises',
        request
      );
      expect(result).toEqual(response);
    });

  });

  describe('substituteExercise', () => {
    it('should substitute an exercise', async () => {
      const request = {
        exerciseId: 'ex-1',
        newExerciseName: 'Front Squat',
        newHevyExerciseTemplateId: 'HEVY123',
        reason: 'Injury modification',
      };
      const response = {
        exerciseId: 'ex-1',
        originalName: 'Back Squat',
        newName: 'Front Squat',
        success: true,
        message: 'Exercise substituted successfully',
      };
      vi.mocked(apiClient.put).mockResolvedValue({ data: response });

      const result = await workoutsApi.substituteExercise('workout-123', request);

      expect(apiClient.put).toHaveBeenCalledWith(
        '/workouts/workout-123/exercises/ex-1/substitute',
        request
      );
      expect(result).toEqual(response);
    });
  });

  describe('updateWorkingWeight', () => {
    it('should update working weight', async () => {
      vi.mocked(apiClient.put).mockResolvedValue({ data: undefined });

      await workoutsApi.updateWorkingWeight('workout-123', 'ex-1', 25, 1, 'Progress');

      expect(apiClient.put).toHaveBeenCalledWith(
        '/workouts/workout-123/exercises/ex-1/working-weight',
        {
          newWeight: 25,
          unit: 1,
          reason: 'Progress',
        }
      );
    });

    it('should work without reason', async () => {
      vi.mocked(apiClient.put).mockResolvedValue({ data: undefined });

      await workoutsApi.updateWorkingWeight('workout-123', 'ex-1', 25, 2);

      expect(apiClient.put).toHaveBeenCalledWith(
        '/workouts/workout-123/exercises/ex-1/working-weight',
        {
          newWeight: 25,
          unit: 2,
          reason: undefined,
        }
      );
    });
  });

  describe('confirmStartingWeight', () => {
    it('should confirm starting weight for an exercise', async () => {
      vi.mocked(apiClient.post).mockResolvedValue({ data: undefined });

      await workoutsApi.confirmStartingWeight('workout-123', 'ex-1', 40, 1);

      expect(apiClient.post).toHaveBeenCalledWith(
        '/workouts/workout-123/exercises/ex-1/confirm-weight',
        { weight: 40, unit: 1 }
      );
    });

    it('should confirm weight in pounds', async () => {
      vi.mocked(apiClient.post).mockResolvedValue({ data: undefined });

      await workoutsApi.confirmStartingWeight('workout-123', 'ex-2', 25, 2);

      expect(apiClient.post).toHaveBeenCalledWith(
        '/workouts/workout-123/exercises/ex-2/confirm-weight',
        { weight: 25, unit: 2 }
      );
    });
  });

  describe('confirmWorkingWeight', () => {
    it('should confirm new working weight after progression', async () => {
      vi.mocked(apiClient.post).mockResolvedValue({ data: undefined });

      await workoutsApi.confirmWorkingWeight('workout-123', 'ex-1', 32.5, 1);

      expect(apiClient.post).toHaveBeenCalledWith(
        '/workouts/workout-123/exercises/ex-1/confirm-working-weight',
        { weight: 32.5, unit: 1 }
      );
    });
  });

  describe('undoLastCompletion', () => {
    it('should undo last completion', async () => {
      const response = {
        day: 1,
        weekNumber: 1,
        weekRolledBack: false,
        newCurrentWeek: 1,
        newCurrentDay: 1,
        message: 'Day 1 completion undone',
      };
      vi.mocked(apiClient.post).mockResolvedValue({ data: response });

      const result = await workoutsApi.undoLastCompletion('workout-123');

      expect(apiClient.post).toHaveBeenCalledWith(
        '/workouts/workout-123/undo-last-completion'
      );
      expect(result).toEqual(response);
    });

    it('should handle week rollback', async () => {
      const response = {
        day: 4,
        weekNumber: 1,
        weekRolledBack: true,
        newCurrentWeek: 1,
        newCurrentDay: 4,
        message: 'Week rolled back and Day 4 completion undone',
      };
      vi.mocked(apiClient.post).mockResolvedValue({ data: response });

      const result = await workoutsApi.undoLastCompletion('workout-123');

      expect(result.weekRolledBack).toBe(true);
    });
  });
});

describe('workoutsApi Error Handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should propagate network errors', async () => {
    const networkError = new Error('Network Error');
    vi.mocked(apiClient.get).mockRejectedValue(networkError);

    await expect(workoutsApi.getAllWorkouts()).rejects.toThrow('Network Error');
  });

  it('should propagate validation errors', async () => {
    const validationError = {
      response: {
        status: 400,
        data: { message: 'Invalid workout data' },
      },
    };
    vi.mocked(apiClient.post).mockRejectedValue(validationError);

    await expect(
      workoutsApi.createWorkout({ name: '', variant: 4, totalWeeks: 21 })
    ).rejects.toEqual(validationError);
  });

  it('should propagate authorization errors', async () => {
    const authError = {
      response: {
        status: 401,
        data: { message: 'Unauthorized' },
      },
    };
    vi.mocked(apiClient.get).mockRejectedValue(authError);

    await expect(workoutsApi.getAllWorkouts()).rejects.toEqual(authError);
  });
});

describe('workoutsApi Request Formatting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should format day completion request correctly', async () => {
    const request: CompleteDayRequest = {
      performances: [
        {
          exerciseId: 'ex-1',
          completedSets: [
            { setNumber: 1, weight: 100, weightUnit: 1, actualReps: 10, wasAmrap: false },
            { setNumber: 2, weight: 100, weightUnit: 1, actualReps: 10, wasAmrap: false },
            { setNumber: 3, weight: 100, weightUnit: 1, actualReps: 12, wasAmrap: true },
          ],
        },
        {
          exerciseId: 'ex-2',
          completedSets: [
            { setNumber: 1, weight: 50, weightUnit: 1, actualReps: 8, wasAmrap: false },
          ],
        },
      ],
    };

    vi.mocked(apiClient.post).mockResolvedValue({ data: {} });

    await workoutsApi.completeDay('workout-123', 1, request);

    const [url, body] = vi.mocked(apiClient.post).mock.calls[0];
    expect(url).toBe('/workouts/workout-123/days/1/complete');
    expect(body.performances).toHaveLength(2);
    expect(body.performances[0].completedSets).toHaveLength(3);
    expect(body.performances[0].completedSets[2].wasAmrap).toBe(true);
  });
});
