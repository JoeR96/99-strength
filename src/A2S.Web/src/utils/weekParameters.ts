/**
 * A2S 2.0 Hypertrophy Program Week Parameters
 *
 * These values match exactly what the backend uses in LinearProgressionStrategy.cs
 * to calculate planned sets. This ensures frontend and backend are in sync.
 */
export interface WeekParameters {
  intensity: number;
  sets: number;
  targetReps: number;
  isDeload: boolean;
}

/**
 * Week-by-week programming data matching the A2S 2024-2025 spreadsheet exactly.
 * Source: LinearProgressionStrategy.cs WeeklyProgram array
 */
const weekParams: Record<number, WeekParameters> = {
  // Block 1 - Volume Phase (Weeks 1-7)
  1:  { intensity: 0.75, sets: 5, targetReps: 10, isDeload: false },
  2:  { intensity: 0.85, sets: 4, targetReps: 8,  isDeload: false },
  3:  { intensity: 0.90, sets: 3, targetReps: 6,  isDeload: false },
  4:  { intensity: 0.80, sets: 5, targetReps: 9,  isDeload: false },
  5:  { intensity: 0.85, sets: 4, targetReps: 7,  isDeload: false },
  6:  { intensity: 0.90, sets: 3, targetReps: 5,  isDeload: false },
  7:  { intensity: 0.65, sets: 5, targetReps: 10, isDeload: true },

  // Block 2 - Intensity Phase (Weeks 8-14)
  8:  { intensity: 0.85, sets: 4, targetReps: 8, isDeload: false },
  9:  { intensity: 0.90, sets: 3, targetReps: 6, isDeload: false },
  10: { intensity: 0.95, sets: 2, targetReps: 4, isDeload: false },
  11: { intensity: 0.85, sets: 4, targetReps: 7, isDeload: false },
  12: { intensity: 0.90, sets: 3, targetReps: 5, isDeload: false },
  13: { intensity: 0.95, sets: 2, targetReps: 3, isDeload: false },
  14: { intensity: 0.65, sets: 5, targetReps: 10, isDeload: true },

  // Block 3 - Peak Phase (Weeks 15-21)
  15: { intensity: 0.90, sets: 3, targetReps: 6, isDeload: false },
  16: { intensity: 0.95, sets: 2, targetReps: 4, isDeload: false },
  17: { intensity: 1.00, sets: 1, targetReps: 2, isDeload: false },
  18: { intensity: 0.95, sets: 2, targetReps: 4, isDeload: false },
  19: { intensity: 1.00, sets: 1, targetReps: 2, isDeload: false },
  20: { intensity: 1.05, sets: 1, targetReps: 2, isDeload: false },
  21: { intensity: 0.65, sets: 5, targetReps: 10, isDeload: true },
};

/**
 * Get week parameters for a given week number.
 * Returns default Week 1 parameters if week is out of range.
 */
export function getWeekParameters(weekNumber: number): WeekParameters {
  return weekParams[weekNumber] || weekParams[1];
}

/**
 * Round weight to the nearest gym increment (2.5 kg/lbs)
 */
export function roundToGymIncrement(weight: number): number {
  return Math.round(weight / 2.5) * 2.5;
}

/**
 * Calculate the working weight for a given training max and week
 */
export function calculateWorkingWeight(trainingMax: number, weekNumber: number): number {
  const params = getWeekParameters(weekNumber);
  return roundToGymIncrement(trainingMax * params.intensity);
}
