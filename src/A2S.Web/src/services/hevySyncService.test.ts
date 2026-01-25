/**
 * Hevy Sync Service Tests
 * Validates that only one week's routines are synced at a time
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { WorkoutDto, ExerciseDto, LinearProgressionDto } from '@/types/workout';
import type { HevyRoutine, HevyCreateRoutineRequest } from '@/types/hevy';

// Mock the hevyApi module
vi.mock('./hevyApi', () => ({
  hevyApi: {
    isConfigured: vi.fn(() => true),
    validateApiKey: vi.fn(() => Promise.resolve(true)),
    getRoutineFolders: vi.fn(() => Promise.resolve({
      routine_folders: [],
      page: 1,
      page_count: 1
    })),
    createRoutineFolder: vi.fn(() => Promise.resolve({
      id: 123,
      title: 'Test Program',
      created_at: new Date().toISOString(),
      updated_at: new Date().toISOString(),
    })),
    createRoutine: vi.fn((request: HevyCreateRoutineRequest) => Promise.resolve({
      id: `routine-${Date.now()}`,
      title: request.routine.title,
      folder_id: request.routine.folder_id,
      notes: request.routine.notes,
      exercises: request.routine.exercises.map((e, i) => ({
        index: i,
        exercise_template_id: e.exercise_template_id,
        superset_id: e.superset_id,
        rest_seconds: e.rest_seconds,
        notes: e.notes,
        sets: e.sets.map((s, j) => ({ index: j, ...s })),
      })),
      created_at: new Date().toISOString(),
      updated_at: new Date().toISOString(),
    } as HevyRoutine)),
    getAllRoutines: vi.fn(() => Promise.resolve([])),
    deleteRoutine: vi.fn(() => Promise.resolve()),
  },
}));

// Mock workoutsApi
vi.mock('@/api/workouts', () => ({
  workoutsApi: {
    setHevyFolderId: vi.fn(() => Promise.resolve()),
    setHevySyncedRoutine: vi.fn(() => Promise.resolve()),
  },
}));

// Import after mocks are set up
import { hevyApi } from './hevyApi';
import { syncWorkoutToHevy, syncDayAsRoutine } from './hevySyncService';

// Helper to create a mock workout
function createMockWorkout(options: {
  currentWeek?: number;
  daysPerWeek?: number;
  hevyRoutineFolderId?: string;
}): WorkoutDto {
  const { currentWeek = 1, daysPerWeek = 4, hevyRoutineFolderId } = options;

  const exercises: ExerciseDto[] = [];

  // Create exercises for each day
  for (let day = 1; day <= daysPerWeek; day++) {
    exercises.push({
      id: `exercise-${day}-1`,
      name: `Squat Day ${day}`,
      category: 1,
      equipment: 0,
      assignedDay: day as 1 | 2 | 3 | 4 | 5 | 6,
      orderInDay: 1,
      hevyExerciseTemplateId: `hevy-squat-${day}`,
      progression: {
        type: 'Linear',
        trainingMax: { value: 100, unit: 1 },
        useAmrap: true,
        baseSetsPerExercise: 5,
      } as LinearProgressionDto,
    });
  }

  return {
    id: 'workout-123',
    name: 'Test A2S Program',
    variant: daysPerWeek as 4 | 5 | 6,
    status: 1,
    currentWeek,
    currentBlock: Math.ceil(currentWeek / 7),
    currentDay: 1,
    daysPerWeek,
    completedDaysInCurrentWeek: [],
    isWeekComplete: false,
    totalWeeks: 21,
    startDate: new Date().toISOString(),
    createdAt: new Date().toISOString(),
    exerciseCount: exercises.length,
    exercises,
    hevyRoutineFolderId,
  };
}

describe('Hevy Sync Service', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('syncWorkoutToHevy', () => {
    it('should only create routines for the current week (not multiple weeks)', async () => {
      const workout = createMockWorkout({ currentWeek: 3, daysPerWeek: 4 });

      const result = await syncWorkoutToHevy(workout);

      expect(result.success).toBe(true);

      // Should have created exactly 4 routines (one per day)
      const createRoutineCalls = vi.mocked(hevyApi.createRoutine).mock.calls;
      expect(createRoutineCalls).toHaveLength(4);

      // All routines should be for Week 3
      createRoutineCalls.forEach((call, index) => {
        const request = call[0] as HevyCreateRoutineRequest;
        expect(request.routine.title).toContain('Week 3');
        expect(request.routine.title).toContain(`Day ${index + 1}`);
      });
    });

    it('should create routines with correct week-specific parameters', async () => {
      const workout = createMockWorkout({ currentWeek: 1, daysPerWeek: 4 });

      await syncWorkoutToHevy(workout);

      const createRoutineCalls = vi.mocked(hevyApi.createRoutine).mock.calls;

      // Week 1 should have intensity 75%, 5 sets, 10 reps
      createRoutineCalls.forEach((call) => {
        const request = call[0] as HevyCreateRoutineRequest;
        const exercise = request.routine.exercises[0];

        // Should have 5 sets for week 1
        expect(exercise.sets).toHaveLength(5);

        // Each set should have 10 reps target
        exercise.sets.forEach((set) => {
          expect(set.reps).toBe(10);
        });

        // Notes should mention intensity
        expect(request.routine.notes).toContain('75%');
      });
    });

    it('should not create routines for past or future weeks', async () => {
      const workout = createMockWorkout({ currentWeek: 5, daysPerWeek: 4 });

      await syncWorkoutToHevy(workout);

      const createRoutineCalls = vi.mocked(hevyApi.createRoutine).mock.calls;

      // Verify no routines created for other weeks
      createRoutineCalls.forEach((call) => {
        const request = call[0] as HevyCreateRoutineRequest;
        // Should only have Week 5
        expect(request.routine.title).toContain('Week 5');
        expect(request.routine.title).not.toContain('Week 4');
        expect(request.routine.title).not.toContain('Week 6');
      });
    });

    it('should create folder if not exists and reuse if already set', async () => {
      // First call - no folder ID
      const workout1 = createMockWorkout({ currentWeek: 1, daysPerWeek: 4 });
      await syncWorkoutToHevy(workout1);

      expect(hevyApi.createRoutineFolder).toHaveBeenCalledTimes(1);

      vi.clearAllMocks();

      // Second call - with existing folder ID
      const workout2 = createMockWorkout({
        currentWeek: 2,
        daysPerWeek: 4,
        hevyRoutineFolderId: '123',
      });
      await syncWorkoutToHevy(workout2);

      // Should not create a new folder
      expect(hevyApi.createRoutineFolder).not.toHaveBeenCalled();
    });
  });

  describe('syncDayAsRoutine', () => {
    it('should create a single routine for the specified day only', async () => {
      const workout = createMockWorkout({ currentWeek: 2, daysPerWeek: 4 });

      const result = await syncDayAsRoutine(workout, 2);

      expect(result.success).toBe(true);

      // Should have created exactly 1 routine
      const createRoutineCalls = vi.mocked(hevyApi.createRoutine).mock.calls;
      expect(createRoutineCalls).toHaveLength(1);

      // Should be for Week 2 Day 2
      const request = createRoutineCalls[0][0] as HevyCreateRoutineRequest;
      expect(request.routine.title).toBe('Test A2S Program - Week 2 Day 2');
    });

    it('should include correct working weight based on week intensity', async () => {
      const workout = createMockWorkout({ currentWeek: 1, daysPerWeek: 4 });

      await syncDayAsRoutine(workout, 1);

      const createRoutineCalls = vi.mocked(hevyApi.createRoutine).mock.calls;
      const request = createRoutineCalls[0][0] as HevyCreateRoutineRequest;
      const exercise = request.routine.exercises[0];

      // Week 1 = 75% intensity, TM = 100kg
      // Working weight should be 75kg (100 * 0.75), rounded to nearest 2.5kg
      expect(exercise.sets[0].weight_kg).toBe(75);
    });

    it('should not include rep_range in sets (API rejects null values)', async () => {
      const workout = createMockWorkout({ currentWeek: 1, daysPerWeek: 4 });

      await syncDayAsRoutine(workout, 1);

      const createRoutineCalls = vi.mocked(hevyApi.createRoutine).mock.calls;
      const request = createRoutineCalls[0][0] as HevyCreateRoutineRequest;
      const exercise = request.routine.exercises[0];

      // Sets should NOT have rep_range property at all
      exercise.sets.forEach((set) => {
        expect(set).not.toHaveProperty('rep_range');
      });
    });
  });

  describe('Week Isolation Validation', () => {
    it('should only ever sync routines for exactly ONE week at a time', async () => {
      // Test across multiple weeks to ensure isolation
      const weeks = [1, 7, 14, 21]; // Sample different weeks including deloads

      for (const week of weeks) {
        vi.clearAllMocks();

        const workout = createMockWorkout({ currentWeek: week, daysPerWeek: 4 });
        await syncWorkoutToHevy(workout);

        const createRoutineCalls = vi.mocked(hevyApi.createRoutine).mock.calls;

        // Should have exactly daysPerWeek routines
        expect(createRoutineCalls).toHaveLength(4);

        // All should be for the same week
        const weekNumbers = createRoutineCalls.map((call) => {
          const request = call[0] as HevyCreateRoutineRequest;
          const match = request.routine.title.match(/Week (\d+)/);
          return match ? parseInt(match[1], 10) : null;
        });

        // All should be the same week
        expect(new Set(weekNumbers).size).toBe(1);
        expect(weekNumbers[0]).toBe(week);
      }
    });

    it('should handle 4, 5, and 6 day variants correctly', async () => {
      for (const daysPerWeek of [4, 5, 6] as const) {
        vi.clearAllMocks();

        const workout = createMockWorkout({ currentWeek: 1, daysPerWeek });
        await syncWorkoutToHevy(workout);

        const createRoutineCalls = vi.mocked(hevyApi.createRoutine).mock.calls;

        // Should create exactly daysPerWeek routines
        expect(createRoutineCalls).toHaveLength(daysPerWeek);
      }
    });
  });
});
