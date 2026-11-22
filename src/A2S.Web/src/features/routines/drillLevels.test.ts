import { describe, expect, it } from 'vitest';
import { DoseUnit, DrillChangeKind, DrillOutcome, SideBias, type MobilityDrillDto, type MobilityRoutineDto } from '@/types/mobility';
import { defaultFeedback, drillsToRate, levelNeighbours, nextTimeLines, prescription } from './drillLevels';

const range = { min: 15, max: 30, step: 5 };
const ladder = (overrides: Partial<MobilityDrillDto> = {}): MobilityDrillDto => ({
  name: 'Reverse plank ladder',
  sets: 3,
  dose: { unit: DoseUnit.Seconds, amount: 15 },
  extraSetSide: SideBias.None,
  cues: null,
  standingAlternative: null,
  perSide: false,
  levels: [
    { name: 'Level 1', description: 'Reverse tabletop, knees bent', sets: 3, dose: { unit: DoseUnit.Seconds, amount: 15 }, range, cues: null },
    { name: 'Level 2', description: 'Hands on a bench, straight legs', sets: 3, dose: { unit: DoseUnit.Seconds, amount: 15 }, range, cues: null },
  ],
  currentLevel: 0,
  ...overrides,
});
const plain: MobilityDrillDto = { ...ladder(), name: "Child's pose", levels: [] };
const routine = (drills: MobilityDrillDto[]): MobilityRoutineDto => ({ id: 'r', name: 'Home yoga', targetMinutes: 20, drills });
const change = (kind: DrillChangeKind, reason = 'why') => ({ changeId: 'c', at: '2026-11-22T18:00:00Z', drillName: 'Reverse plank ladder', kind, reason, undone: false });

describe('drillLevels', () => {
  it('finds the easier and harder neighbours', () => {
    expect(levelNeighbours(ladder()).easier).toBeNull();
    expect(levelNeighbours(ladder()).harder?.name).toBe('Level 2');
    expect(levelNeighbours(ladder({ currentLevel: 1 })).easier?.name).toBe('Level 1');
  });

  it('defaults ticked drills to clean and the rest to skipped', () => {
    expect(defaultFeedback(routine([ladder(), plain]), [true, false]).map((f) => f.outcome)).toEqual([DrillOutcome.Clean, DrillOutcome.Skipped]);
  });

  it('only asks about ticked drills with levels', () => {
    expect(drillsToRate(routine([ladder(), plain]), [true, true]).map((d) => d.name)).toEqual(['Reverse plank ladder']);
  });

  it('phrases a step-up and a level suggestion for next time', () => {
    const stepped = routine([ladder({ dose: { unit: DoseUnit.Seconds, amount: 20 } })]);
    expect(nextTimeLines(stepped, [change(DrillChangeKind.DoseIncreased)])).toEqual(['Next time: Reverse plank ladder — 3 × 20 s']);

    const suggested = routine([ladder({ dose: { unit: DoseUnit.Seconds, amount: 30 }, suggestedLevel: 1 })]);
    expect(nextTimeLines(suggested, [change(DrillChangeKind.LevelUpSuggested)])).toEqual(['Next time: Level 2: Hands on a bench, straight legs']);
  });

  it('shows the level with the dose', () => {
    expect(prescription(ladder())).toBe('Level 1 · 3 × 15 s');
    expect(prescription(plain)).toBe('3 × 15 s');
  });
});
