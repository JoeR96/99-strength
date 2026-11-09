/** Story/test handlers for `budgetApi`. */

import { budgetApi } from '@/api/budget';
import { dayBudgetFixture } from '../fixtures/budget';
import { installHandlers, never, rejectWith } from './install';

type BudgetHandlers = Partial<typeof budgetApi>;

export const budgetHandlers = {
  default: (): BudgetHandlers => ({ getDayBudget: async () => dayBudgetFixture() }),
  noWarnings: (): BudgetHandlers => ({
    getDayBudget: async () =>
      dayBudgetFixture({
        hardSetsThisWeek: { Chest: 6, Back: 8, Shoulders: 4, Quads: 5, Hamstrings: 4, Glutes: 6, Core: 6 },
        volumeWarnings: [],
      }),
  }),
  loading: (): BudgetHandlers => ({ getDayBudget: never }),
  error: (): BudgetHandlers => ({ getDayBudget: rejectWith(500) }),
};

export function installBudgetHandlers(handlers: BudgetHandlers = budgetHandlers.default()): void {
  installHandlers(budgetApi, handlers);
}
