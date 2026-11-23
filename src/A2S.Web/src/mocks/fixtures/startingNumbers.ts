/** Hevy-based starting-number suggestions for stories and tests (the cycle fixture's pending Bench Press). */

import type { StartingNumberSuggestion, StartingNumbersSuggestions } from '@/api/workouts';

export const BENCH_ID = 'ex-bench-press-barbell-';

export const freshSuggestion: StartingNumberSuggestion = {
  valueKg: 77.5,
  estimatedOneRepMaxKg: 87.5,
  setDate: '2026-11-14',
  setWeightKg: 75,
  setReps: 5,
  windowStart: '2026-08-25',
  windowEnd: '2026-11-23',
  stale: false,
  stalePenaltyPercent: 0,
  reason: 'From 75 kg × 5 on 14 Nov (est. 1RM 87.5 kg)',
};

export const staleSuggestion: StartingNumberSuggestion = {
  valueKg: 80,
  estimatedOneRepMaxKg: 100,
  setDate: '2026-05-07',
  setWeightKg: 100,
  setReps: 1,
  windowStart: '2026-08-25',
  windowEnd: '2026-11-23',
  stale: true,
  stalePenaltyPercent: 10,
  reason: 'Older history — estimated from 100 kg × 1 on 7 May (est. 1RM 100 kg, less 10 %)',
};

function one(suggestion: StartingNumberSuggestion | null, hevyStatus: StartingNumbersSuggestions['hevyStatus'] = 'ok'): StartingNumbersSuggestions {
  return {
    hevyStatus,
    hevyMessage: hevyStatus === 'unavailable' ? 'Hevy unavailable' : hevyStatus === 'noKey' ? 'Connect Hevy to get suggestions' : null,
    items: [{ exerciseId: BENCH_ID, exerciseName: 'Bench Press (Barbell)', kind: 'TrainingMax', matchedBy: suggestion ? 'templateId' : null, suggestion }],
  };
}

export const startingNumbers = {
  fresh: () => one(freshSuggestion),
  stale: () => one(staleSuggestion),
  none: () => one(null),
  unavailable: () => one(null, 'unavailable'),
  noKey: () => one(null, 'noKey'),
};
