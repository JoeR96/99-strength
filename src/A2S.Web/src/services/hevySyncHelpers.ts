/**
 * Hevy Sync Helpers
 * Types, unit conversion and exercise-template lookup for sending a completed workout to Hevy
 */

import { hevyApi } from './hevyApi';
import { lbsToKg } from '@/utils/constants';
import type {
  HevyWorkoutExercise,
  HevyWorkoutSet,
} from '@/types/hevy';
import type { ExerciseDto } from '@/types/workout';

// Completed set data from workout session
export interface CompletedSetData {
  setNumber: number;
  weight: number;
  reps: number;
  isAmrap: boolean;
}

// Completed exercise data from workout session
export interface CompletedExerciseData {
  exercise: ExerciseDto;
  sets: CompletedSetData[];
  weightUnit: string; // 'kg' or 'lbs'
}

export interface SyncResult {
  success: boolean;
  message: string;
  workout?: import('@/types/hevy').HevyWorkout;
}

/**
 * Convert weight to kg if needed and round to 2 decimal places
 */
export function convertToKg(weight: number, unit: string): number {
  let weightKg = weight;
  if (unit.toLowerCase() === 'lbs' || unit.toLowerCase() === 'pounds') {
    weightKg = lbsToKg(weight);
  }
  return Math.round(weightKg * 100) / 100;
}

/**
 * Check if a string looks like a valid Hevy template ID (8-character hex string)
 */
export function isValidHevyTemplateId(id: string): boolean {
  return /^[A-F0-9]{8}$/i.test(id);
}

/**
 * Cache for Hevy exercise templates to avoid repeated API calls
 */
let hevyTemplateCache: Map<string, string> | null = null;

/**
 * Get or build the Hevy exercise template lookup map (name -> id)
 */
export async function getHevyTemplateMap(): Promise<Map<string, string>> {
  if (hevyTemplateCache) {
    return hevyTemplateCache;
  }

  try {
    const templates = await hevyApi.getAllExerciseTemplates();
    hevyTemplateCache = new Map();
    for (const template of templates) {
      hevyTemplateCache.set(template.title.toLowerCase(), template.id);
    }
    return hevyTemplateCache;
  } catch {
    return new Map();
  }
}

/**
 * Clear the Hevy template cache (useful after API key changes)
 */
export function clearHevyTemplateCache(): void {
  hevyTemplateCache = null;
}

/**
 * Resolve a Hevy template ID — validate or look up by name.
 *
 * Matching is deliberately conservative: an exact (case-insensitive) title match wins;
 * otherwise a substring match is only used when it is UNAMBIGUOUS. A loose "first
 * substring hit wins" strategy previously bound exercises to the wrong template
 * (e.g. "Squat (Barbell)" ↔ "Pause Squat (Barbell)"), silently sending data against
 * the wrong Hevy exercise.
 */
export async function resolveHevyTemplateId(exerciseName: string, storedTemplateId: string): Promise<string> {
  if (isValidHevyTemplateId(storedTemplateId)) {
    return storedTemplateId;
  }

  const templateMap = await getHevyTemplateMap();
  const query = exerciseName.toLowerCase().trim().replace(/\s+/g, ' ');

  const exactMatch = templateMap.get(query);
  if (exactMatch) {
    return exactMatch;
  }

  const candidates: { name: string; id: string }[] = [];
  for (const [name, id] of templateMap) {
    if (name.includes(query) || query.includes(name)) {
      candidates.push({ name, id });
    }
  }

  if (candidates.length === 1) {
    return candidates[0].id;
  }

  if (candidates.length > 1) {
    // Prefer the candidate closest in length to the query (e.g. "lat pulldown (cable)"
    // over "single arm lat pulldown (cable)"). If two are equally close it's genuinely
    // ambiguous — fail loudly rather than guessing the wrong exercise.
    candidates.sort((a, b) =>
      Math.abs(a.name.length - query.length) - Math.abs(b.name.length - query.length));
    const [best, next] = candidates;
    if (Math.abs(best.name.length - query.length) < Math.abs(next.name.length - query.length)) {
      return best.id;
    }
    return '';
  }

  // Fall back to the stored ID. May be empty/undefined if neither the stored ID was a
  // valid Hevy template ID nor the name matched — callers must guard against that before
  // sending to Hevy (an empty/missing exercise_template_id is rejected by the API).
  return storedTemplateId ?? '';
}

/**
 * Convert completed exercise data to Hevy workout exercise format
 */
export function convertCompletedExerciseToHevy(
  exerciseData: CompletedExerciseData,
  amrapTarget?: number,
  resolvedTemplateId?: string,
  progressionChange?: string
): HevyWorkoutExercise {
  const templateId = resolvedTemplateId || exerciseData.exercise.hevyExerciseTemplateId;

  const sets: HevyWorkoutSet[] = exerciseData.sets
    .filter(set => set.reps > 0)
    .map((set) => ({
      type: set.isAmrap ? 'failure' : 'normal',
      weight_kg: convertToKg(set.weight, exerciseData.weightUnit),
      reps: set.reps,
      distance_meters: null,
      duration_seconds: null,
      custom_metric: null,
      rpe: null,
    }));

  const noteParts: string[] = [];
  if (amrapTarget) {
    noteParts.push(`AMRAP target: ${amrapTarget} reps`);
  }
  if (progressionChange) {
    noteParts.push(`Progression: ${progressionChange}`);
  }
  const notes = noteParts.length > 0 ? noteParts.join(' | ') : null;

  return {
    exercise_template_id: templateId,
    superset_id: null,
    notes,
    sets,
  };
}
