/** Story/test handlers for `suggestedPlansApi` (the first-run "Start this plan" card). */

import { suggestedPlansApi, type StartSuggestedPlanResult, type SuggestedPlanPreview } from '@/api/suggestedPlans';
import { installHandlers, never, rejectWith } from './install';

type SuggestedPlansHandlers = Partial<typeof suggestedPlansApi>;

export const cutTo85Preview: SuggestedPlanPreview = {
  key: 'cut-to-85',
  name: 'Cut to 85 — Nov 2026',
  description: 'A2S2 on the four main lifts, cardio twice a week, Mobility A as every warm-up, five daily habits.',
  startDate: '2026-11-16',
  inputs: [
    { key: 'benchTm', label: 'Bench press TM', unit: 'kg', default: null, minimum: 20, maximum: 400 },
    { key: 'squatTm', label: 'Squat TM', unit: 'kg', default: null, minimum: 20, maximum: 400 },
    { key: 'ohpTm', label: 'Overhead press TM', unit: 'kg', default: null, minimum: 20, maximum: 400 },
    { key: 'deadliftTm', label: 'Deadlift TM', unit: 'kg', default: null, minimum: 20, maximum: 400 },
    { key: 'startWeightKg', label: 'Start weight', unit: 'kg', default: 95, minimum: 30, maximum: 300 },
  ],
  schedule: [
    { day: 1, kind: 'Lift', label: 'Upper A' },
    { day: 2, kind: 'Lift', label: 'Lower A' },
    { day: 3, kind: 'Cardio', label: 'Norwegian 4×4' },
    { day: 3, kind: 'Mobility', label: 'Mobility B' },
    { day: 4, kind: 'Lift', label: 'Upper B' },
    { day: 5, kind: 'Lift', label: 'Lower B' },
    { day: 6, kind: 'Cardio', label: 'C25K run' },
    { day: 0, kind: 'Rest', label: 'Rest — walk only' },
  ],
  liftingDays: [],
  habits: ['Water', 'Walks', 'Morning weigh-in', 'Mobility A', 'Creatine'],
  checkpoints: [
    { date: '2026-12-07', targetKg: 92, note: '4 cutting weeks' },
    { date: '2026-12-21', targetKg: 91, note: 'Cut pauses after this' },
    { date: '2027-02-01', targetKg: 88, note: null },
    { date: '2027-03-08', targetKg: 85, note: 'Goal' },
  ],
  alreadyStarted: false,
};

export const startedResult: StartSuggestedPlanResult = {
  planId: '0f1a4c2e-7b3d-4e5f-9a1b-2c3d4e5f6a7b',
  workoutId: 'w-1',
  startDate: '2026-11-16',
  alreadyStarted: false,
  warnings: [],
};

export const suggestedPlansHandlers = {
  default: (): SuggestedPlansHandlers => ({
    list: async () => [cutTo85Preview],
    startCutTo85: async () => startedResult,
  }),
  loading: (): SuggestedPlansHandlers => ({ list: never, startCutTo85: never }),
  error: (): SuggestedPlansHandlers => ({ list: rejectWith(500), startCutTo85: rejectWith(500) }),
};

export function installSuggestedPlansHandlers(handlers: SuggestedPlansHandlers = suggestedPlansHandlers.default()): void {
  installHandlers(suggestedPlansApi, handlers);
}
