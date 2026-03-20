/**
 * DEPRECATED: This file contains frontend calculations that duplicate backend logic.
 * These functions are kept for backward compatibility during migration.
 * New code should use the workoutsApi.getWeekPlan() endpoint instead.
 *
 * TODO: Remove this file once all components are migrated to use the API.
 */

export interface WeekParameters {
  intensity: number;
  sets: number;
  targetReps: number;
  repOutTarget: number | null;
  isDeload: boolean;
}

/**
 * @deprecated Use workoutsApi.getWeekPlan() instead.
 * This function duplicates backend logic from LinearProgressionStrategy.
 */
export function getWeekParameters(weekNumber: number): WeekParameters {
  // Week data matching the A2S2 Hypertrophy spreadsheet exactly
  // Format: [intensity, sets, repsPerSet, repOutTarget]
  // repOutTarget = null for deload weeks (no AMRAP)
  const weeklyProgram: [number, number, number, number | null][] = [
    [0, 0, 0, null],       // Week 0 placeholder (1-indexed)
    // Block 1
    [0.65, 4, 12, 15],    // Week 1
    [0.68, 4, 11, 13],    // Week 2
    [0.70, 4, 10, 12],    // Week 3
    [0.68, 4, 11, 13],    // Week 4
    [0.70, 4, 10, 12],    // Week 5
    [0.73, 4,  9, 11],    // Week 6
    [0.60, 4,  5, null],  // Week 7 - DELOAD
    // Block 2
    [0.68, 4, 11, 13],    // Week 8
    [0.70, 4, 10, 12],    // Week 9
    [0.73, 4,  9, 11],    // Week 10
    [0.70, 4, 10, 12],    // Week 11
    [0.73, 4,  9, 11],    // Week 12
    [0.76, 4,  8, 10],    // Week 13
    [0.60, 4,  5, null],  // Week 14 - DELOAD
    // Block 3
    [0.70, 4, 10, 12],    // Week 15
    [0.73, 4,  9, 11],    // Week 16
    [0.76, 4,  8, 10],    // Week 17
    [0.73, 4,  9, 11],    // Week 18
    [0.76, 4,  8, 10],    // Week 19
    [0.79, 4,  7,  9],    // Week 20
    [0.60, 4,  5, null],  // Week 21 - DELOAD
  ];

  // Handle out-of-range weeks
  if (weekNumber < 1 || weekNumber > 21) {
    console.warn(`Week ${weekNumber} is out of range (1-21), using week 1 defaults`);
    return {
      intensity: 0.65,
      sets: 4,
      targetReps: 12,
      repOutTarget: 15,
      isDeload: false,
    };
  }

  const [intensity, sets, targetReps, repOutTarget] = weeklyProgram[weekNumber];
  const isDeload = weekNumber === 7 || weekNumber === 14 || weekNumber === 21;

  return { intensity, sets, targetReps, repOutTarget, isDeload };
}

/**
 * Translate a program week to a template week (1-21) using the block sequence.
 * E.g., with sequence [1,1,2,3], program week 10 → template week 3
 */
export function getTemplateWeek(programWeek: number, blockSequence: number[]): number {
  const blockIndex = Math.floor((programWeek - 1) / 7);
  const blockType = blockSequence[blockIndex] ?? 1;
  const weekInBlock = ((programWeek - 1) % 7) + 1;
  return ((blockType - 1) * 7) + weekInBlock;
}

/**
 * Get the block type for a given program week from the block sequence.
 */
export function getBlockType(programWeek: number, blockSequence: number[]): number {
  const blockIndex = Math.floor((programWeek - 1) / 7);
  return blockSequence[blockIndex] ?? 1;
}

/**
 * @deprecated Use backend PlannedSetDto.weightKg or weightLbs which is already rounded.
 * Round weight to nearest gym increment (2.5kg or 5lbs).
 */
export function roundToGymIncrement(weight: number, unit: 'kg' | 'lbs' = 'kg'): number {
  const increment = unit === 'kg' ? 2.5 : 5;
  return Math.round(weight / increment) * increment;
}
