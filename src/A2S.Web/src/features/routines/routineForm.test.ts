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

describe('levels in the form', () => {
  const ladderRoutine = () => ({
    id: 'r2',
    name: 'Home',
    targetMinutes: 20,
    drills: [
      {
        name: 'Ladder',
        sets: 3,
        dose: { unit: DoseUnit.Seconds, amount: 25 },
        extraSetSide: SideBias.None,
        cues: null,
        standingAlternative: null,
        perSide: false,
        levels: [
          { name: 'Level 1', description: null, sets: 3, dose: { unit: DoseUnit.Seconds, amount: 15 }, range: { min: 15, max: 30, step: 5 }, cues: null },
          { name: 'Level 2', description: null, sets: 2, dose: { unit: DoseUnit.Seconds, amount: 20 }, range: { min: 20, max: 30, step: 5 }, cues: null },
        ],
        currentLevel: 0,
        cleanStreak: 1,
        suggestedLevel: null,
      },
    ],
  });

  it('keeps the progress when the starting level is unchanged', () => {
    const dto = toDrillDtos(draftFromRoutine(ladderRoutine()))[0];
    expect(dto).toMatchObject({ currentLevel: 0, cleanStreak: 1, dose: { amount: 25 }, sets: 3 });
  });

  it('starts a new level at the bottom of its range with its sets', () => {
    const draft = draftFromRoutine(ladderRoutine());
    draft.drills[0].startingLevel = 1;
    expect(toDrillDtos(draft)[0]).toMatchObject({ currentLevel: 1, cleanStreak: 0, dose: { amount: 20 }, sets: 2 });
  });

  it('flags a level range that runs backwards', () => {
    const draft = draftFromRoutine(ladderRoutine());
    draft.drills[0].levels[0].rangeMax = '10';
    expect(validateRoutine(draft).level[draft.drills[0].levels[0].key]?.range).toBeDefined();
  });
});
