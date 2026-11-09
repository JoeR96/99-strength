/** Day-budget fixture: the brief's default caps, with back over its cap. */

import type { DayBudgetDto } from '@/types/budget';

export const DEFAULT_CAPS: Record<string, number> = {
  Chest: 10,
  Back: 14,
  Shoulders: 10,
  Quads: 10,
  Hamstrings: 10,
  Glutes: 12,
  Core: 12,
};

export function dayBudgetFixture(overrides: Partial<DayBudgetDto> = {}): DayBudgetDto {
  return {
    hardSetsThisWeek: { Chest: 9, Back: 16, Shoulders: 6, Quads: 7, Hamstrings: 6, Glutes: 8, Core: 9 },
    volumeWarnings: [{ bucket: 'Back', sets: 16, cap: 14, message: 'Back is at 16 hard sets this week, over the cap of 14.' }],
    time: {
      totalMinutes: 52,
      warmUpMinutes: 10,
      perExercise: [
        { name: 'Bench press', minutes: 16 },
        { name: 'Chest-supported row', minutes: 10 },
        { name: 'Landmine press', minutes: 9 },
        { name: 'Cable row + face pulls', minutes: 7 },
      ],
      overBudget: false,
      budgetMinutes: 55,
    },
    ...overrides,
  };
}
