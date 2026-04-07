/**
 * Hevy Sync Helpers
 * Types, utility functions, and template conversion for Hevy sync
 */

import { hevyApi } from './hevyApi';
import { getWeekParameters } from '@/utils/weekParameters';
import { lbsToKg } from '@/utils/constants';
import type {
  HevyRoutineSet,
  HevyRoutineExercise,
  HevyWorkoutExercise,
  HevyWorkoutSet,
} from '@/types/hevy';
import type {
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
} from '@/types/workout';

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
  routine?: import('@/types/hevy').HevyRoutine;
  routines?: import('@/types/hevy').HevyRoutine[];
  errors?: string[];
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
 * Round weight to 2 decimal places
 */
export function roundWeight(weight: number): number {
  return Math.round(weight * 100) / 100;
}

/**
 * Round weight to nearest gym increment (2.5kg or 5lbs)
 */
export function roundToGymIncrement(weight: number, unit: 'kg' | 'lbs' = 'kg'): number {
  const increment = unit === 'kg' ? 2.5 : 5;
  return Math.round(weight / increment) * increment;
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
 * Resolve a Hevy template ID — validate or look up by name
 */
export async function resolveHevyTemplateId(exerciseName: string, storedTemplateId: string): Promise<string> {
  if (isValidHevyTemplateId(storedTemplateId)) {
    return storedTemplateId;
  }

  const templateMap = await getHevyTemplateMap();

  const exactMatch = templateMap.get(exerciseName.toLowerCase());
  if (exactMatch) {
    return exactMatch;
  }

  for (const [name, id] of templateMap) {
    if (name.includes(exerciseName.toLowerCase()) || exerciseName.toLowerCase().includes(name)) {
      return id;
    }
  }

  return storedTemplateId;
}

/**
 * Convert completed exercise data to Hevy workout exercise format
 */
export function convertCompletedExerciseToHevy(
  exerciseData: CompletedExerciseData,
  amrapTarget?: number,
  resolvedTemplateId?: string
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

  const notes = amrapTarget ? `AMRAP target: ${amrapTarget} reps` : null;

  return {
    exercise_template_id: templateId,
    superset_id: null,
    notes,
    sets,
  };
}

/**
 * Convert exercise to Hevy routine exercise format (for routines/templates)
 */
export function convertExerciseToHevyRoutine(exercise: ExerciseDto, weekNumber: number, resolvedTemplateId?: string): HevyRoutineExercise {
  const templateId = resolvedTemplateId || exercise.hevyExerciseTemplateId;
  const sets: HevyRoutineSet[] = [];
  let notes: string | null = null;

  if (exercise.progression.type === 'Linear') {
    const prog = exercise.progression as LinearProgressionDto;
    const weekParams = getWeekParameters(weekNumber);

    const trainingMaxKg = prog.trainingMax.unit === 1
      ? prog.trainingMax.value
      : lbsToKg(prog.trainingMax.value);
    const workingWeight = roundToGymIncrement(trainingMaxKg * weekParams.intensity, 'kg');

    for (let i = 0; i < weekParams.sets; i++) {
      const isLastSet = i === weekParams.sets - 1;
      const isAmrap = isLastSet && prog.useAmrap && !weekParams.isDeload;
      sets.push({
        type: isAmrap ? 'failure' : 'normal',
        weight_kg: roundWeight(workingWeight),
        reps: isAmrap && weekParams.repOutTarget != null
          ? weekParams.repOutTarget
          : weekParams.targetReps,
      });
    }

    const noteParts: string[] = [];
    if (weekParams.isDeload) {
      noteParts.push('DELOAD WEEK');
    }
    noteParts.push(`TM: ${prog.trainingMax.value}${prog.trainingMax.unit === 1 ? 'kg' : 'lbs'}`);
    noteParts.push(`Intensity: ${Math.round(weekParams.intensity * 100)}%`);
    if (prog.useAmrap && weekParams.repOutTarget != null) {
      noteParts.push(`AMRAP target: ${weekParams.repOutTarget}+ reps on last set`);
    }
    notes = noteParts.join(' | ');

  } else if (exercise.progression.type === 'RepsPerSet') {
    const prog = exercise.progression as RepsPerSetProgressionDto;

    const weightKg = prog.weightUnit?.toLowerCase() === 'pounds'
      ? lbsToKg(prog.currentWeight)
      : prog.currentWeight;

    const totalSets = prog.isUnilateral ? prog.currentSetCount * 2 : prog.currentSetCount;

    for (let i = 0; i < totalSets; i++) {
      sets.push({
        type: 'normal',
        weight_kg: roundWeight(weightKg),
        reps: prog.repRange.maximum,
      });
    }

    const unilateralNote = prog.isUnilateral ? ` | Unilateral: ${prog.currentSetCount} sets per side` : '';
    notes = `Rep range: ${prog.repRange.minimum}-${prog.repRange.maximum}${unilateralNote}`;

  } else if (exercise.progression.type === 'MinimalSets') {
    const prog = exercise.progression as MinimalSetsProgressionDto;

    const weightKg = prog.weightUnit?.toLowerCase() === 'pounds'
      ? lbsToKg(prog.currentWeight)
      : prog.currentWeight;

    const repsPerSet = Math.ceil(prog.targetTotalReps / prog.currentSetCount);

    for (let i = 0; i < prog.currentSetCount; i++) {
      sets.push({
        type: 'normal',
        weight_kg: roundWeight(weightKg),
        reps: repsPerSet,
      });
    }

    notes = `Target: ${prog.targetTotalReps} total reps across ${prog.minimumSets}-${prog.maximumSets} sets`;
  }

  return {
    exercise_template_id: templateId,
    superset_id: null,
    rest_seconds: 120,
    notes,
    sets,
  };
}
