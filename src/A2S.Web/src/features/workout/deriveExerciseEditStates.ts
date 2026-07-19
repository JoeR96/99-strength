import type {
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  WorkoutDto,
} from "@/types/workout";
import { WeightUnit } from "@/types/workout";

export type ProgressionType = "Linear" | "RepsPerSet" | "MinimalSets";

export interface ExerciseEditState {
  exerciseId: string;
  name: string;
  progressionType: ProgressionType;
  // Weight / TM
  originalValue: number;
  newValue: number;
  unit: string;
  weightUnit: WeightUnit;
  hasChanged: boolean;
  // RepsPerSet rep range
  repRangeMin: number;
  repRangeMax: number;
  originalRepRangeMin: number;
  originalRepRangeMax: number;
  startingSets: number;
  originalStartingSets: number;
  currentSets: number;
  originalCurrentSets: number;
  targetSets: number;
  originalTargetSets: number;
  // Swap state
  wantSwap: boolean;
  // Swap fields: when Linear wants to become RPS
  swapWeight: number;
  swapRepMin: number;
  swapRepMax: number;
  swapTargetSets: number;
  swapIsUnilateral: boolean;
  // Swap fields: when RPS wants to become Linear
  swapTrainingMax: number;
  // Extra info for display
  linearSets?: number;
  linearAmrap?: boolean;
}

/**
 * Derives the per-exercise edit state used by EditExercisesModal from the
 * exercises assigned to the given day. Pure move from the modal's useEffect
 * body: identical output.
 */
export function deriveExerciseEditStates(
  exercisesForDay: WorkoutDto["exercises"]
): ExerciseEditState[] {
  return exercisesForDay.map((exercise): ExerciseEditState => {
    const isLinear = exercise.progression.type === "Linear";
    const isRepsPerSet = exercise.progression.type === "RepsPerSet";
    const isMinimalSets = exercise.progression.type === "MinimalSets";

    let value = 0;
    let unit = "kg";
    let weightUnit: WeightUnit = WeightUnit.Kilograms;

    const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
    const rpsProg = isRepsPerSet ? (exercise.progression as RepsPerSetProgressionDto) : null;
    const minProg = isMinimalSets ? (exercise.progression as MinimalSetsProgressionDto) : null;

    if (linearProg) {
      value = linearProg.trainingMax.value;
      weightUnit = linearProg.trainingMax.unit;
      unit = weightUnit === WeightUnit.Kilograms ? "kg" : "lbs";
    } else if (rpsProg) {
      value = rpsProg.currentWeight;
      unit = rpsProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg";
      weightUnit = unit === "lbs" ? WeightUnit.Pounds : WeightUnit.Kilograms;
    } else if (minProg) {
      value = minProg.currentWeight;
      unit = minProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg";
      weightUnit = unit === "lbs" ? WeightUnit.Pounds : WeightUnit.Kilograms;
    }

    return {
      exerciseId: exercise.id,
      name: exercise.name,
      progressionType: exercise.progression.type as ProgressionType,
      originalValue: value,
      newValue: value,
      unit,
      weightUnit,
      hasChanged: false,
      // Rep range
      repRangeMin: rpsProg?.repRange?.minimum ?? 8,
      repRangeMax: rpsProg?.repRange?.maximum ?? 12,
      originalRepRangeMin: rpsProg?.repRange?.minimum ?? 8,
      originalRepRangeMax: rpsProg?.repRange?.maximum ?? 12,
      startingSets: rpsProg?.startingSets ?? 2,
      originalStartingSets: rpsProg?.startingSets ?? 2,
      currentSets: rpsProg?.currentSetCount ?? 2,
      originalCurrentSets: rpsProg?.currentSetCount ?? 2,
      targetSets: rpsProg?.targetSets ?? 5,
      originalTargetSets: rpsProg?.targetSets ?? 5,
      // Swap state
      wantSwap: false,
      // Swap to RPS defaults (from Linear: ~60% TM)
      swapWeight: linearProg ? Math.round((linearProg.trainingMax.value * 0.6) / 2.5) * 2.5 : value,
      swapRepMin: 8,
      swapRepMax: 12,
      swapTargetSets: 5,
      swapIsUnilateral: false,
      // Swap to Linear defaults (from RPS: ~150% weight)
      swapTrainingMax: rpsProg ? Math.round((rpsProg.currentWeight * 1.5) / 2.5) * 2.5 : 100,
      // Display info
      linearSets: linearProg?.baseSetsPerExercise,
      linearAmrap: linearProg?.useAmrap,
    };
  });
}

/**
 * Recalculates hasChanged for an updated edit state. Pure move from the
 * modal's updateState body.
 */
export function withRecalculatedHasChanged(updated: ExerciseEditState): ExerciseEditState {
  return {
    ...updated,
    hasChanged:
      updated.newValue !== updated.originalValue ||
      updated.repRangeMin !== updated.originalRepRangeMin ||
      updated.repRangeMax !== updated.originalRepRangeMax ||
      updated.startingSets !== updated.originalStartingSets ||
      updated.currentSets !== updated.originalCurrentSets ||
      updated.targetSets !== updated.originalTargetSets ||
      updated.wantSwap,
  };
}
