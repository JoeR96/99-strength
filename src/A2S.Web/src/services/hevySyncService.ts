/**
 * Hevy Sync Service
 * What is left of the browser-driven Hevy code: sending a session logged in the app to Hevy as a
 * completed workout, and the Plan tab's "Pull from Hevy". Both go through `/hevy/{resource}` on
 * our API, which attaches the stored key.
 *
 * Routines are not built here any more. The server pushes them (`api/hevySync.ts`,
 * `hooks/useHevySync.ts`) and stores their Hevy ids.
 */

import { hevyApi } from './hevyApi';
import { getWeekParameters } from '@/utils/weekParameters';
import type {
  HevyCreateWorkoutRequest,
  HevyWorkoutExercise,
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

// Re-exported so callers have one module to import from
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
  endTime: Date,
  progressionChanges?: { exerciseId: string; change: string }[],
  /** The week that was just completed. After a rollover `workout.currentWeek` is already the next one. */
  weekNumber: number = workout.currentWeek
): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured. Please set your API key in settings.',
    };
  }

  const dayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const dayName = dayNames[dayNumber - 1] || `Day ${dayNumber}`;
  const workoutTitle = `${workout.name} - Week ${weekNumber} / Day ${dayNumber} (${dayName})`;

  try {
    const weekParams = getWeekParameters(weekNumber);
    const hevyExercises: HevyWorkoutExercise[] = [];
    const unresolved: string[] = [];

    for (const exerciseData of completedExercises) {
      const completedSets = exerciseData.sets.filter(s => s.reps > 0);
      if (completedSets.length > 0) {
        const resolvedTemplateId = await resolveHevyTemplateId(
          exerciseData.exercise.name,
          exerciseData.exercise.hevyExerciseTemplateId
        );
        if (!resolvedTemplateId) {
          unresolved.push(exerciseData.exercise.name);
          continue;
        }
        let amrapTarget: number | undefined;
        if (exerciseData.exercise.progression.type === 'Linear') {
          const prog = exerciseData.exercise.progression as LinearProgressionDto;
          if (prog.useAmrap) {
            amrapTarget = weekParams.repOutTarget ?? weekParams.targetReps;
          }
        }
        const progressionChange = progressionChanges?.find(
          (c) => c.exerciseId === exerciseData.exercise.id
        )?.change;
        const hevyExercise = convertCompletedExerciseToHevy(
          exerciseData, amrapTarget, resolvedTemplateId, progressionChange);
        hevyExercises.push(hevyExercise);
      }
    }

    if (unresolved.length > 0) {
      return {
        success: false,
        message: `Could not resolve a Hevy exercise template for: ${unresolved.join(', ')}. ` +
          `Check the exercise name matches one in Hevy, or set a valid template ID.`,
      };
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
