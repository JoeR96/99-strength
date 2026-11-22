import { describe, it, expect } from 'vitest';
import { DoseUnit, SideBias } from '@/types/mobility';
import { draftFromRoutine, emptyDrill, emptyRoutine, hasErrors, moveDrill, toDrillDtos, validateRoutine } from './routineForm';

describe('validateRoutine', () => {
  it('accepts a filled-in routine', () => {
    const draft = { ...emptyRoutine(), name: 'Evening reset' };
    draft.drills[0].name = 'Cat-cow';

    expect(hasErrors(validateRoutine(draft))).toBe(false);
  });

  it('flags a missing name, bad minutes, an unnamed drill and duplicate drill names', () => {
    const a = { ...emptyDrill(), name: 'Pigeon' };
    const b = { ...emptyDrill(), name: ' pigeon ' };
    const c = { ...emptyDrill(), name: '', sets: '0', amount: 'x' };
    const errors = validateRoutine({ name: ' ', targetMinutes: '0', drills: [a, b, c] });

    expect(errors.name).toBeDefined();
    expect(errors.targetMinutes).toBeDefined();
    expect(errors.drill[a.key]).toBeUndefined();
    expect(errors.drill[b.key]?.name).toMatch(/different name/);
    expect(errors.drill[c.key]).toEqual({ name: 'Name the drill', sets: '1 to 20', amount: 'At least 1' });
  });

  it('needs at least one drill', () => {
    expect(validateRoutine({ name: 'Flow', targetMinutes: '25', drills: [] }).drills).toBeDefined();
  });
});

describe('toDrillDtos / draftFromRoutine', () => {
  it('round-trips a routine, trimming text and turning blanks into null', () => {
    const routine = {
      id: 'r1',
      name: 'Flow',
      targetMinutes: 25,
      drills: [{ name: 'Lizard', sets: 1, dose: { unit: DoseUnit.Breaths, amount: 5 }, extraSetSide: SideBias.Left, cues: null, standingAlternative: 'Standing lunge', perSide: true }],
    };
    const draft = draftFromRoutine(routine);
    draft.drills[0].cues = '  ';

    expect(toDrillDtos(draft)).toEqual(routine.drills);
  });
});

describe('moveDrill', () => {
  it('swaps with the neighbour and ignores moves off either end', () => {
    const drills = [emptyDrill(), emptyDrill(), emptyDrill()];

    expect(moveDrill(drills, 1, -1).map((d) => d.key)).toEqual([drills[1].key, drills[0].key, drills[2].key]);
    expect(moveDrill(drills, 0, -1)).toBe(drills);
    expect(moveDrill(drills, 2, 1)).toBe(drills);
  });
});
