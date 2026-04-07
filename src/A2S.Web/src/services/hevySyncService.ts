/**
 * Hevy Sync Service
 * Core sync functions for pushing workouts and routines to Hevy
 *
 * Re-exports all types and functions from sub-modules for backwards compatibility.
 */

import { hevyApi } from './hevyApi';
import { workoutsApi } from '@/api/workouts';
import { getWeekParameters } from '@/utils/weekParameters';
import type {
  HevyCreateWorkoutRequest,
  HevyCreateRoutineRequest,
  HevyRoutineExercise,
  HevyWorkoutExercise,
  HevyRoutine,
} from '@/types/hevy';
import type {
  WorkoutDto,
  LinearProgressionDto,
} from '@/types/workout';

import {
  type CompletedSetData,
  type CompletedExerciseData,
  type SyncResult,
  convertCompletedExerciseToHevy,
  convertExerciseToHevyRoutine,
  resolveHevyTemplateId,
  clearHevyTemplateCache,
} from './hevySyncHelpers';

import {
  type PulledSetData,
  type PulledWorkoutData,
  type DetectedSubstitution,
  type WeightDiscrepancy,
  type MissingExercise,
  type PullWorkoutResult,
  pullWorkoutFromHevy,
} from './hevySyncPull';

import {
  checkHevySyncAvailable,
  getOrCreateRoutineFolder,
  deleteRoutineFromHevy,
  resyncDayToHevy,
  getRoutinesFromHevy,
  cleanupDuplicateRoutines,
  handleRoutineLifecycle,
} from './hevySyncManagement';

// Re-export all types and functions for backwards compatibility
export type {
  CompletedSetData,
  CompletedExerciseData,
  SyncResult,
  PulledSetData,
  PulledWorkoutData,
  DetectedSubstitution,
  WeightDiscrepancy,
  MissingExercise,
  PullWorkoutResult,
};

export {
  clearHevyTemplateCache,
  pullWorkoutFromHevy,
  checkHevySyncAvailable,
  getOrCreateRoutineFolder,
  deleteRoutineFromHevy,
  resyncDayToHevy,
  getRoutinesFromHevy,
  handleRoutineLifecycle,
};

/**
 * Create a completed workout in Hevy
 * Used after finishing a workout session
 */
export async function createCompletedWorkoutInHevy(
  workout: WorkoutDto,
  dayNumber: number,
  completedExercises: CompletedExerciseData[],
  startTime: Date,
  endTime: Date
): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured. Please set your API key in settings.',
    };
  }

  const dayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const dayName = dayNames[dayNumber - 1] || `Day ${dayNumber}`;
  const workoutTitle = `${workout.name} - Week ${workout.currentWeek} / Day ${dayNumber} (${dayName})`;

  try {
    const weekParams = getWeekParameters(workout.currentWeek);
    const hevyExercises: HevyWorkoutExercise[] = [];

    for (const exerciseData of completedExercises) {
      const completedSets = exerciseData.sets.filter(s => s.reps > 0);
      if (completedSets.length > 0) {
        const resolvedTemplateId = await resolveHevyTemplateId(
          exerciseData.exercise.name,
          exerciseData.exercise.hevyExerciseTemplateId
        );
        let amrapTarget: number | undefined;
        if (exerciseData.exercise.progression.type === 'Linear') {
          const prog = exerciseData.exercise.progression as LinearProgressionDto;
          if (prog.useAmrap) {
            amrapTarget = weekParams.repOutTarget ?? weekParams.targetReps;
          }
        }
        const hevyExercise = convertCompletedExerciseToHevy(exerciseData, amrapTarget, resolvedTemplateId);
        hevyExercises.push(hevyExercise);
      }
    }

    if (hevyExercises.length === 0) {
      return { success: false, message: 'No completed exercises to sync' };
    }

    const workoutRequest: HevyCreateWorkoutRequest = {
      workout: {
        title: workoutTitle,
        description: `Block ${workout.currentBlock} - Auto-synced from 99 Strength`,
        start_time: startTime.toISOString(),
        end_time: endTime.toISOString(),
        is_private: false,
        exercises: hevyExercises,
      },
    };

    const createdWorkout = await hevyApi.createWorkout(workoutRequest);

    return {
      success: true,
      message: `Workout "${workoutTitle}" synced to Hevy!`,
      workout: createdWorkout,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    return { success: false, message: `Failed to sync workout: ${message}` };
  }
}

/**
 * Sync a day's exercises as a routine template to Hevy for a specific week
 */
export async function syncDayAsRoutineForWeek(
  workout: WorkoutDto,
  dayNumber: number,
  weekNumber: number,
  folderId?: string | null,
  forceRecreate?: boolean
): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return { success: false, message: 'Hevy API key not configured. Please set your API key in settings.' };
  }

  const routineTitle = `${workout.name} - Week ${weekNumber} Day ${dayNumber}`;

  try {
    const allRoutines = await hevyApi.getAllRoutines();
    const existingRoutine = allRoutines.find(
      (r) => r.title.toLowerCase() === routineTitle.toLowerCase()
    );

    if (existingRoutine) {
      if (forceRecreate) {
        try {
          await hevyApi.deleteRoutine(existingRoutine.id);
        } catch {
          // Continue with creation
        }
      } else {
        return {
          success: true,
          message: `Routine "${routineTitle}" already exists in Hevy`,
          routine: existingRoutine,
        };
      }
    }

    const dayExercises = workout.exercises
      .filter((e) => e.assignedDay === dayNumber)
      .sort((a, b) => a.orderInDay - b.orderInDay);

    if (dayExercises.length === 0) {
      return { success: false, message: `No exercises found for Day ${dayNumber}` };
    }

    const weekParams = getWeekParameters(weekNumber);

    const hevyExercises: HevyRoutineExercise[] = [];
    for (const exercise of dayExercises) {
      const resolvedTemplateId = await resolveHevyTemplateId(
        exercise.name,
        exercise.hevyExerciseTemplateId
      );
      const hevyExercise = convertExerciseToHevyRoutine(exercise, weekNumber, resolvedTemplateId);
      hevyExercises.push(hevyExercise);
    }

    const folderIdNum = folderId ? parseInt(folderId, 10) :
                        workout.hevyRoutineFolderId ? parseInt(workout.hevyRoutineFolderId, 10) :
                        null;

    const currentBlock = Math.ceil(weekNumber / 7);
    const routineNotes = weekParams.isDeload
      ? `Block ${currentBlock} | DELOAD WEEK | Intensity: ${Math.round(weekParams.intensity * 100)}%`
      : `Block ${currentBlock} | Intensity: ${Math.round(weekParams.intensity * 100)}% | ${weekParams.sets} sets \u00d7 ${weekParams.targetReps} reps`;

    const routineRequest: HevyCreateRoutineRequest = {
      routine: {
        title: routineTitle,
        folder_id: folderIdNum && !isNaN(folderIdNum) ? folderIdNum : null,
        notes: routineNotes,
        exercises: hevyExercises,
      },
    };

    const routine = await hevyApi.createRoutine(routineRequest);
    await cleanupDuplicateRoutines(routineTitle);

    try {
      await workoutsApi.setHevySyncedRoutine(workout.id, weekNumber, dayNumber, routine.id);
    } catch {
      // Don't fail \u2014 routine was created
    }

    return {
      success: true,
      message: `Routine "${routineTitle}" created in Hevy!`,
      routine,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    return { success: false, message: `Failed to create routine: ${message}` };
  }
}

/**
 * Sync a day's exercises as a routine template to Hevy for the current week
 */
export async function syncDayAsRoutine(
  workout: WorkoutDto,
  dayNumber: number,
  folderId?: string | null,
  forceRecreate?: boolean
): Promise<SyncResult> {
  return syncDayAsRoutineForWeek(workout, dayNumber, workout.currentWeek, folderId, forceRecreate);
}

/**
 * Sync an entire week's workouts to Hevy as routines
 */
export async function syncWeekToHevy(workout: WorkoutDto, weekNumber: number): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return { success: false, message: 'Hevy API key not configured. Please set your API key in settings.' };
  }

  const daysPerWeek = workout.daysPerWeek || 4;
  const createdRoutines: HevyRoutine[] = [];
  const errors: string[] = [];

  let folderId = workout.hevyRoutineFolderId;
  if (!folderId) {
    const folderResult = await getOrCreateRoutineFolder(workout.name);
    if (folderResult) {
      folderId = folderResult.folderId;
      try {
        await workoutsApi.setHevyFolderId(workout.id, folderId);
      } catch {
        // Continue
      }
    } else {
      errors.push('Failed to create routine folder');
    }
  }

  for (let day = 1; day <= daysPerWeek; day++) {
    const result = await syncDayAsRoutineForWeek(workout, day, weekNumber, folderId);
    if (result.success && result.routine) {
      createdRoutines.push(result.routine);
    } else if (!result.success) {
      errors.push(result.message);
    }
  }

  if (errors.length > 0 && createdRoutines.length === 0) {
    return { success: false, message: 'Failed to sync week to Hevy', errors };
  }

  const folderMsg = folderId ? ` in folder "${workout.name}"` : '';
  return {
    success: true,
    message: `Successfully synced ${createdRoutines.length} routines for Week ${weekNumber}${folderMsg} to Hevy`,
    routines: createdRoutines,
    errors: errors.length > 0 ? errors : undefined,
  };
}

/**
 * Sync an entire workout program to Hevy as routines
 */
export async function syncWorkoutToHevy(workout: WorkoutDto): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return { success: false, message: 'Hevy API key not configured. Please set your API key in settings.' };
  }

  const daysPerWeek = workout.daysPerWeek || 4;
  const createdRoutines: HevyRoutine[] = [];
  const errors: string[] = [];

  let folderId = workout.hevyRoutineFolderId;
  if (!folderId) {
    const folderResult = await getOrCreateRoutineFolder(workout.name);
    if (folderResult) {
      folderId = folderResult.folderId;
      try {
        await workoutsApi.setHevyFolderId(workout.id, folderId);
      } catch {
        // Continue
      }
    } else {
      errors.push('Failed to create routine folder');
    }
  }

  for (let day = 1; day <= daysPerWeek; day++) {
    const result = await syncDayAsRoutine(workout, day, folderId);
    if (result.success && result.routine) {
      createdRoutines.push(result.routine);
    } else if (!result.success) {
      errors.push(result.message);
    }
  }

  if (errors.length > 0 && createdRoutines.length === 0) {
    return { success: false, message: 'Failed to sync workout to Hevy', errors };
  }

  const folderMsg = folderId ? ` in folder "${workout.name}"` : '';
  return {
    success: true,
    message: `Successfully synced ${createdRoutines.length} routines${folderMsg} to Hevy`,
    routines: createdRoutines,
    errors: errors.length > 0 ? errors : undefined,
  };
}

/**
 * Sync a single day's workout to Hevy
 */
export async function syncDayToHevy(
  workout: WorkoutDto,
  dayNumber: number
): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return { success: false, message: 'Hevy API key not configured. Please set your API key in settings.' };
  }

  let folderId = workout.hevyRoutineFolderId;
  if (!folderId) {
    const folderResult = await getOrCreateRoutineFolder(workout.name);
    if (folderResult) {
      folderId = folderResult.folderId;
      try {
        await workoutsApi.setHevyFolderId(workout.id, folderId);
      } catch {
        // Continue
      }
    }
  }

  return syncDayAsRoutine(workout, dayNumber, folderId);
}
