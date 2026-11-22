import { describe, it, expect } from 'vitest';
import type { ExerciseDto } from '@/types/workout';
import { canHaveExtraSet, groupingUpdates } from './exerciseGrouping';

const ex = (id: string, extra: Partial<ExerciseDto> = {}, unilateral = false): ExerciseDto =>
  ({ id, name: id, progression: { type: 'RepsPerSet', isUnilateral: unilateral }, supersetGroup: null, extraSetSide: 'None', ...extra }) as unknown as ExerciseDto;

describe('groupingUpdates', () => {
  it('sends a new superset group, clears a removed one and sends a changed extra set side', () => {
    const exercises = [ex('a'), ex('b', { supersetGroup: 2 }), ex('c', {}, true), ex('d')];
    const updates = groupingUpdates(exercises, {
      a: { supersetGroup: 2, extraSetSide: 'None' },
      b: { supersetGroup: null, extraSetSide: 'None' },
      c: { supersetGroup: null, extraSetSide: 'Left' },
      d: { supersetGroup: null, extraSetSide: 'None' },
    });

    expect(updates).toEqual([
      { exerciseId: 'a', supersetGroup: 2 },
      { exerciseId: 'b', clearSupersetGroup: true },
      { exerciseId: 'c', extraSetSide: 'Left' },
    ]);
  });

  it('offers the extra set only for unilateral reps-per-set exercises', () => {
    expect(canHaveExtraSet(ex('a', {}, true))).toBe(true);
    expect(canHaveExtraSet(ex('b'))).toBe(false);
    expect(canHaveExtraSet({ ...ex('c', {}, true), progression: { type: 'Linear' } } as ExerciseDto)).toBe(false);
  });
});
