/**
 * useWorkouts Hook Tests
 * Tests for workout data fetching and mutations
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';
import {
  useCurrentWorkout,
  useAllWorkouts,
  useExerciseLibrary,
  useCreateWorkout,
  useSetActiveWorkout,
  useDeleteWorkout,
  useUpdateExercises,
  useSubstituteExercise,
  useUndoCompletion,
  workoutKeys,
} from './useWorkouts';
import { workoutsApi } from '../api/workouts';
import type { WorkoutDto, CreateWorkoutRequest } from '../types/workout';

// Mock the workouts API
vi.mock('../api/workouts', () => ({
  workoutsApi: {
    getCurrentWorkout: vi.fn(),
    getAllWorkouts: vi.fn(),
    getExerciseLibrary: vi.fn(),
    createWorkout: vi.fn(),
    setActiveWorkout: vi.fn(),
    deleteWorkout: vi.fn(),
    updateExercises: vi.fn(),
    substituteExercise: vi.fn(),
    undoLastCompletion: vi.fn(),
  },
}));

// Helper to create test query client
function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

// Wrapper component for hooks
function createWrapper(queryClient: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        {children}
      </QueryClientProvider>
    );
  };
}

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

describe('useWorkouts Hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = createTestQueryClient();
  });

  describe('workoutKeys', () => {
    it('should have correct key structure', () => {
      expect(workoutKeys.all).toEqual(['workouts']);
      expect(workoutKeys.current()).toEqual(['workouts', 'current']);
      expect(workoutKeys.list()).toEqual(['workouts', 'list']);
      expect(workoutKeys.exerciseLibrary()).toEqual(['exerciseLibrary']);
    });
  });

  describe('useCurrentWorkout', () => {
    it('should fetch current workout', async () => {
      vi.mocked(workoutsApi.getCurrentWorkout).mockResolvedValue(mockWorkout);

      const { result } = renderHook(() => useCurrentWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toEqual(mockWorkout);
      expect(workoutsApi.getCurrentWorkout).toHaveBeenCalledTimes(1);
    });

    it('should handle null (no active workout)', async () => {
      vi.mocked(workoutsApi.getCurrentWorkout).mockResolvedValue(null);

      const { result } = renderHook(() => useCurrentWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toBeNull();
    });

    it('should handle fetch error', async () => {
      vi.mocked(workoutsApi.getCurrentWorkout).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useCurrentWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error?.message).toBe('Network error');
    });
  });

  describe('useAllWorkouts', () => {
    it('should fetch all workouts', async () => {
      const mockWorkouts = [mockWorkout, { ...mockWorkout, id: 'workout-456', name: 'Second Program' }];
      vi.mocked(workoutsApi.getAllWorkouts).mockResolvedValue(mockWorkouts);

      const { result } = renderHook(() => useAllWorkouts(), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toHaveLength(2);
    });
  });

  describe('useExerciseLibrary', () => {
    it('should fetch exercise library', async () => {
      const mockLibrary = {
        templates: [
          { name: 'Squat', equipment: 0, description: 'Barbell squat' },
          { name: 'Bench Press', equipment: 0, description: 'Barbell bench press' },
        ],
      };
      vi.mocked(workoutsApi.getExerciseLibrary).mockResolvedValue(mockLibrary);

      const { result } = renderHook(() => useExerciseLibrary(), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data?.templates).toHaveLength(2);
    });
  });

  describe('useCreateWorkout', () => {
    it('should create a new workout', async () => {
      vi.mocked(workoutsApi.createWorkout).mockResolvedValue(mockWorkout);

      const { result } = renderHook(() => useCreateWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      const request: CreateWorkoutRequest = {
        name: 'Test Program',
        variant: 4,
        totalWeeks: 21,
      };

      await act(async () => {
        result.current.mutate(request);
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(workoutsApi.createWorkout).toHaveBeenCalledWith(request);
      expect(result.current.data).toEqual(mockWorkout);
    });

    it('should invalidate queries on success', async () => {
      vi.mocked(workoutsApi.createWorkout).mockResolvedValue(mockWorkout);

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCreateWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      await act(async () => {
        result.current.mutate({ name: 'Test', variant: 4, totalWeeks: 21 });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: workoutKeys.current() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: workoutKeys.list() });
    });
  });

  describe('useSetActiveWorkout', () => {
    it('should set workout as active', async () => {
      vi.mocked(workoutsApi.setActiveWorkout).mockResolvedValue(undefined);

      const { result } = renderHook(() => useSetActiveWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      await act(async () => {
        result.current.mutate('workout-123');
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(workoutsApi.setActiveWorkout).toHaveBeenCalledWith('workout-123');
    });

    it('should invalidate queries on success', async () => {
      vi.mocked(workoutsApi.setActiveWorkout).mockResolvedValue(undefined);

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useSetActiveWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      await act(async () => {
        result.current.mutate('workout-123');
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: workoutKeys.current() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: workoutKeys.list() });
    });
  });

  describe('useDeleteWorkout', () => {
    it('should delete a workout', async () => {
      vi.mocked(workoutsApi.deleteWorkout).mockResolvedValue(undefined);

      const { result } = renderHook(() => useDeleteWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      await act(async () => {
        result.current.mutate('workout-123');
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(workoutsApi.deleteWorkout).toHaveBeenCalledWith('workout-123');
    });

    it('should invalidate queries on success', async () => {
      vi.mocked(workoutsApi.deleteWorkout).mockResolvedValue(undefined);

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useDeleteWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      await act(async () => {
        result.current.mutate('workout-123');
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: workoutKeys.current() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: workoutKeys.list() });
    });
  });

  describe('useUpdateExercises', () => {
    it('should update exercises', async () => {
      const mockResult = {
        workoutId: 'workout-123',
        updatedCount: 1,
        results: [{ exerciseId: 'ex-1', exerciseName: 'Squat', success: true }],
      };
      vi.mocked(workoutsApi.updateExercises).mockResolvedValue(mockResult);

      const { result } = renderHook(() => useUpdateExercises(), {
        wrapper: createWrapper(queryClient),
      });

      const params = {
        workoutId: 'workout-123',
        request: {
          updates: [{ exerciseId: 'ex-1', trainingMaxValue: 100, trainingMaxUnit: 1 as const }],
        },
      };

      await act(async () => {
        result.current.mutate(params);
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(workoutsApi.updateExercises).toHaveBeenCalledWith('workout-123', params.request);
    });

    it('should invalidate current workout on success', async () => {
      vi.mocked(workoutsApi.updateExercises).mockResolvedValue({
        workoutId: 'workout-123',
        updatedCount: 1,
        results: [],
      });

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useUpdateExercises(), {
        wrapper: createWrapper(queryClient),
      });

      await act(async () => {
        result.current.mutate({
          workoutId: 'workout-123',
          request: { updates: [] },
        });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: workoutKeys.current() });
    });
  });

  describe('useSubstituteExercise', () => {
    it('should substitute an exercise', async () => {
      const mockResult = {
        exerciseId: 'ex-1',
        originalName: 'Squat',
        newName: 'Front Squat',
        success: true,
      };
      vi.mocked(workoutsApi.substituteExercise).mockResolvedValue(mockResult);

      const { result } = renderHook(() => useSubstituteExercise(), {
        wrapper: createWrapper(queryClient),
      });

      const params = {
        workoutId: 'workout-123',
        request: {
          exerciseId: 'ex-1',
          newExerciseName: 'Front Squat',
          reason: 'Testing substitution',
        },
      };

      await act(async () => {
        result.current.mutate(params);
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(workoutsApi.substituteExercise).toHaveBeenCalledWith('workout-123', params.request);
      expect(result.current.data).toEqual(mockResult);
    });
  });

  describe('useUndoCompletion', () => {
    it('should undo last completion', async () => {
      vi.mocked(workoutsApi.undoLastCompletion).mockResolvedValue(undefined);

      const { result } = renderHook(() => useUndoCompletion(), {
        wrapper: createWrapper(queryClient),
      });

      await act(async () => {
        result.current.mutate('workout-123');
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(workoutsApi.undoLastCompletion).toHaveBeenCalledWith('workout-123');
    });

    it('should invalidate workout queries on success', async () => {
      vi.mocked(workoutsApi.undoLastCompletion).mockResolvedValue(undefined);

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useUndoCompletion(), {
        wrapper: createWrapper(queryClient),
      });

      await act(async () => {
        result.current.mutate('workout-123');
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      // Note: useUndoCompletion invalidates ['workout'] and ['workouts'] keys
      expect(invalidateSpy).toHaveBeenCalled();
    });
  });

  describe('Error Handling', () => {
    it('should handle mutation errors', async () => {
      vi.mocked(workoutsApi.createWorkout).mockRejectedValue(new Error('Server error'));

      const { result } = renderHook(() => useCreateWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      await act(async () => {
        result.current.mutate({ name: 'Test', variant: 4, totalWeeks: 21 });
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error?.message).toBe('Server error');
    });
  });

  describe('Loading States', () => {
    it('should track loading state during fetch', async () => {
      vi.mocked(workoutsApi.getCurrentWorkout).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(mockWorkout), 100))
      );

      const { result } = renderHook(() => useCurrentWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      expect(result.current.isLoading).toBe(true);

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });
    });

    it('should track pending state during mutation', async () => {
      let resolvePromise: (value: typeof mockWorkout) => void;
      vi.mocked(workoutsApi.createWorkout).mockImplementation(
        () => new Promise((resolve) => {
          resolvePromise = resolve;
        })
      );

      const { result } = renderHook(() => useCreateWorkout(), {
        wrapper: createWrapper(queryClient),
      });

      expect(result.current.isPending).toBe(false);

      await act(async () => {
        result.current.mutate({ name: 'Test', variant: 4, totalWeeks: 21 });
      });

      // After triggering mutation, check pending state
      await waitFor(() => {
        expect(result.current.isPending).toBe(true);
      });

      // Resolve the promise
      await act(async () => {
        resolvePromise!(mockWorkout);
      });

      await waitFor(() => {
        expect(result.current.isPending).toBe(false);
      });
    });
  });
});
