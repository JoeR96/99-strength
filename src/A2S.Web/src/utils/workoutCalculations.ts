/**
 * DEPRECATED: This file contains frontend calculations that duplicate backend logic.
 * These functions are kept for backward compatibility during migration.
 * New code should use the workoutsApi.getWeekPlan() endpoint instead.
 *
 * TODO: Remove this file once all components are migrated to use the API.
 */

import { getWeekParameters, roundToGymIncrement } from './weekParameters';
import { lbsToKg } from './constants';
import type {
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
} from '@/types/workout';

/**
 * @deprecated Use workoutsApi.getWeekPlan() to get PlannedSetDto.weightKg instead.
 * Calculate the prescribed weight for an exercise based on progression type and week.
 */
export function calculatePrescribedWeight(exercise: ExerciseDto, weekNumber: number): number {
  const prog = exercise.progression;

  if (prog.type === 'Linear') {
    const linear = prog as LinearProgressionDto;
    const weekParams = getWeekParameters(weekNumber);

    // Training Max × Intensity, rounded to gym increment
    const trainingMaxKg = linear.trainingMax.unit === 1
      ? linear.trainingMax.value
      : lbsToKg(linear.trainingMax.value); // Convert lbs to kg

    return roundToGymIncrement(trainingMaxKg * weekParams.intensity, 'kg');
  } else if (prog.type === 'RepsPerSet') {
    const reps = prog as RepsPerSetProgressionDto;
    // Return weight in kg
    if (reps.weightUnit?.toLowerCase() === 'pounds') {
      return lbsToKg(reps.currentWeight);
    }
    return reps.currentWeight;
  } else if (prog.type === 'MinimalSets') {
    const minimal = prog as MinimalSetsProgressionDto;
    // Return weight in kg
    if (minimal.weightUnit?.toLowerCase() === 'pounds') {
      return lbsToKg(minimal.currentWeight);
    }
    return minimal.currentWeight;
  }

  return 0;
}

/**
 * @deprecated Use workoutsApi.getWeekPlan() to get PlannedSetDto instead.
 * Get the prescribed sets and reps for an exercise based on progression type and week.
 */
export function getPrescribedSetsAndReps(
  exercise: ExerciseDto,
  weekNumber: number
): { sets: number; reps: number } {
  const prog = exercise.progression;

  if (prog.type === 'Linear') {
    const weekParams = getWeekParameters(weekNumber);
    return {
      sets: weekParams.sets,
      reps: weekParams.targetReps,
    };
  } else if (prog.type === 'RepsPerSet') {
    const reps = prog as RepsPerSetProgressionDto;
    return {
      sets: reps.currentSetCount,
      reps: reps.repRange.maximum,
    };
  } else if (prog.type === 'MinimalSets') {
    const minimal = prog as MinimalSetsProgressionDto;
    const repsPerSet = Math.floor(minimal.targetTotalReps / minimal.currentSetCount);
    return {
      sets: minimal.currentSetCount,
      reps: repsPerSet,
    };
  }

  // Default fallback
  return { sets: 3, reps: 10 };
}
