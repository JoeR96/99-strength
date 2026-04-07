import type { ExerciseEntry, SavedWorkoutProgress } from "./workoutSessionTypes";

const WORKOUT_PROGRESS_KEY = "workout_progress";

export function saveWorkoutProgress(
  workoutId: string,
  dayNumber: number,
  weekNumber: number,
  exerciseEntries: ExerciseEntry[]
): void {
  const progress: SavedWorkoutProgress = {
    workoutId,
    dayNumber,
    weekNumber,
    savedAt: new Date().toISOString(),
    exercises: exerciseEntries.map((entry) => ({
      exerciseId: entry.exercise.id,
      sets: entry.sets.map((set) => ({
        setNumber: set.setNumber,
        weight: set.weight,
        reps: set.reps,
        isAmrap: set.isAmrap,
        completed: set.completed,
      })),
    })),
  };
  localStorage.setItem(WORKOUT_PROGRESS_KEY, JSON.stringify(progress));
}

export function loadWorkoutProgress(): SavedWorkoutProgress | null {
  try {
    const stored = localStorage.getItem(WORKOUT_PROGRESS_KEY);
    if (!stored) return null;
    return JSON.parse(stored) as SavedWorkoutProgress;
  } catch (error) {
    console.warn('Failed to parse saved workout progress:', error);
    return null;
  }
}

export function clearWorkoutProgress(): void {
  localStorage.removeItem(WORKOUT_PROGRESS_KEY);
}
