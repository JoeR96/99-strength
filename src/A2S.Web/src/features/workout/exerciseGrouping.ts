/** Pure helpers for the superset / extra set editor. */

import type { ExerciseDto, ExerciseUpdateRequest, ExtraSetSide, RepsPerSetProgressionDto } from '@/types/workout';

export interface GroupingChoice {
  supersetGroup: number | null;
  extraSetSide: ExtraSetSide;
}

/** The extra set is for unilateral reps-per-set exercises only. */
export function canHaveExtraSet(exercise: ExerciseDto): boolean {
  return exercise.progression.type === 'RepsPerSet' && (exercise.progression as RepsPerSetProgressionDto).isUnilateral === true;
}

/** The update items for what changed: `clearSupersetGroup` when a group is removed. */
export function groupingUpdates(exercises: ExerciseDto[], choices: Record<string, GroupingChoice>): ExerciseUpdateRequest[] {
  return exercises.flatMap((e) => {
    const choice = choices[e.id];
    if (!choice) return [];
    const update: ExerciseUpdateRequest = { exerciseId: e.id };
    if (choice.supersetGroup !== (e.supersetGroup ?? null)) {
      if (choice.supersetGroup === null) update.clearSupersetGroup = true;
      else update.supersetGroup = choice.supersetGroup;
    }
    if (choice.extraSetSide !== (e.extraSetSide ?? 'None')) update.extraSetSide = choice.extraSetSide;
    return Object.keys(update).length > 1 ? [update] : [];
  });
}
