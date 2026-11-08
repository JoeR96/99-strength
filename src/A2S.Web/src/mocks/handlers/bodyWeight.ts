/** Story/test handlers for `bodyWeightApi`. */

import { bodyWeightApi } from '@/api/bodyWeight';
import { todayLocal, type LocalDate } from '@/lib/localDate';
import type { Pace } from '@/types/bodyWeight';
import { weightTrendFixture } from '../fixtures/bodyWeight';
import { installHandlers, never, rejectWith } from './install';

type BodyWeightHandlers = Partial<typeof bodyWeightApi>;

export function bodyWeightWithPace(pace: Pace, days = 30): BodyWeightHandlers {
  return {
    getTrend: async (_from?: LocalDate, to?: LocalDate) => weightTrendFixture(to ?? todayLocal(), pace, days),
    record: async (date, kg) => ({ date, kg, recordedAtUtc: new Date().toISOString() }),
  };
}

export const bodyWeightHandlers = {
  default: (): BodyWeightHandlers => bodyWeightWithPace('OnPace'),
  onPace: (): BodyWeightHandlers => bodyWeightWithPace('OnPace'),
  behind: (): BodyWeightHandlers => bodyWeightWithPace('Behind'),
  ahead: (): BodyWeightHandlers => bodyWeightWithPace('Ahead'),
  noTarget: (): BodyWeightHandlers => bodyWeightWithPace('NoTarget'),
  /** Fewer than seven days: the 7-day moving average is absent on every point. */
  fewPoints: (): BodyWeightHandlers => bodyWeightWithPace('OnPace', 4),
  empty: (): BodyWeightHandlers => ({
    getTrend: async () => ({ points: [], pace: null }),
    record: async (date, kg) => ({ date, kg, recordedAtUtc: new Date().toISOString() }),
  }),
  loading: (): BodyWeightHandlers => ({ getTrend: never }),
  error: (): BodyWeightHandlers => ({ getTrend: rejectWith(500), record: rejectWith(500) }),
};

export function installBodyWeightHandlers(handlers: BodyWeightHandlers = bodyWeightHandlers.default()): void {
  installHandlers(bodyWeightApi, handlers);
}
