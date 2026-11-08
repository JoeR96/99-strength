/**
 * Builds the session's exercise entries. The planned sets come from the API's
 * week plan (`GET /workouts/{id}/weeks/{w}/days/{d}/plan`), which already knows
 * the block, deload and every progression rule. The local calculators remain
 * only as a fallback for when that request fails, so the page still opens.
 */

import type { WeekPlanDto } from '@/api/workouts';
import { getWeekParameters, roundToGymIncrement } from '@/utils/weekParameters';
import type {
  ExerciseDto,
  ExerciseEntry,
  LinearProgressionDto,
  MinimalSetsProgressionDto,
  RepsPerSetProgressionDto,
  SetEntry,
} from './workoutSessionTypes';

function isPounds(unit: string | undefined | null): boolean {
  const u = (unit ?? '').toLowerCase();
  return u === 'pounds' || u === 'lbs' || u === 'lb';
}

function fromWeekPlan(exercise: ExerciseDto, weekPlan: WeekPlanDto): ExerciseEntry | null {
  const planned = weekPlan.exercises.find((e) => e.exerciseId === exercise.id);
  if (!planned || planned.plannedSets.length === 0) return null;
  const lbs = isPounds(planned.plannedSets[0].originalUnit);
  const sets: SetEntry[] = planned.plannedSets.map((s) => ({
    setNumber: s.setNumber,
    weight: Math.round((lbs ? s.weightLbs : s.weightKg) * 10) / 10,
    reps: s.targetReps,
    isAmrap: s.isAmrap,
    completed: false,
  }));
  return {
    exercise,
    sets,
    targetSets: sets.length,
    targetReps: sets[0].reps,
    targetWeight: sets[0].weight,
    weightUnit: lbs ? 'lbs' : 'kg',
    isAmrapExercise: sets.some((s) => s.isAmrap),
  };
}

/** @deprecated Fallback only; the week plan is the source of truth. */
function fromLocalCalculators(exercise: ExerciseDto, currentWeek: number): ExerciseEntry {
  const weekParams = getWeekParameters(currentWeek);
  let targetSets = 3;
  let targetReps = 10;
  let targetWeight = 50;
  let weightUnit = 'kg';
  let isAmrapExercise = false;

  if (exercise.progression.type === 'Linear') {
    const prog = exercise.progression as LinearProgressionDto;
    targetSets = weekParams.sets;
    targetReps = weekParams.targetReps;
    targetWeight = roundToGymIncrement(prog.trainingMax.value * weekParams.intensity);
    weightUnit = prog.trainingMax.unit === 1 ? 'kg' : 'lbs';
    isAmrapExercise = prog.useAmrap;
  } else if (exercise.progression.type === 'RepsPerSet') {
    const prog = exercise.progression as RepsPerSetProgressionDto;
    targetSets = prog.currentSetCount;
    targetReps = prog.repRange.maximum;
    targetWeight = prog.isWeightPending ? 0 : prog.currentWeight;
    weightUnit = isPounds(prog.weightUnit) ? 'lbs' : 'kg';
  } else if (exercise.progression.type === 'MinimalSets') {
    const prog = exercise.progression as MinimalSetsProgressionDto;
    targetSets = prog.currentSetCount;
    targetReps = Math.ceil(prog.targetTotalReps / prog.currentSetCount);
    targetWeight = prog.currentWeight;
    weightUnit = isPounds(prog.weightUnit) ? 'lbs' : 'kg';
  }

  const sets: SetEntry[] = [];
  for (let i = 1; i <= targetSets; i++) {
    sets.push({
      setNumber: i,
      weight: Math.round(targetWeight * 10) / 10,
      reps: targetReps,
      isAmrap: isAmrapExercise && i === targetSets,
      completed: false,
    });
  }
  return { exercise, sets, targetSets, targetReps, targetWeight, weightUnit, isAmrapExercise };
}

export function buildExerciseEntries(
  dayExercises: ExerciseDto[],
  currentWeek: number,
  weekPlan: WeekPlanDto | null | undefined
): ExerciseEntry[] {
  return dayExercises.map((exercise) => (weekPlan && fromWeekPlan(exercise, weekPlan)) || fromLocalCalculators(exercise, currentWeek));
}
