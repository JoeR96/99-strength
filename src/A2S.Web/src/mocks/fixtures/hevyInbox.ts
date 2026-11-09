/** Hevy sync fixtures: pulled workouts the plan could not place, a pull summary and a week push. */

import type { HevyInboxItemDto, PullSummary, PushWeekResult } from '@/types/hevySync';
import { addDays, weekStartLocal, type LocalDate } from '@/lib/localDate';

export const INBOX_SUNDAY_LOWER_ID = 'hw_0001_sunday_lower';
export const INBOX_WEDNESDAY_UPPER_ID = 'hw_0002_wednesday_upper';

/** Trained on the rest day (Sunday) and did Upper on the cardio day (Wednesday) of the week containing `date`. */
export function inboxTwoItems(date: LocalDate): HevyInboxItemDto[] {
  const monday = weekStartLocal(date);
  const wednesday = addDays(monday, 2);
  const sunday = addDays(monday, 6);
  return [
    {
      hevyWorkoutId: INBOX_SUNDAY_LOWER_ID,
      title: 'Lower A',
      startUtc: sunday + 'T09:02:00Z',
      endUtc: sunday + 'T10:11:00Z',
      localDate: sunday,
      routineId: null,
      reason: 'rest day',
      exercises: [
        { title: 'Squat (Barbell)', templateId: 'D04AC939', sets: 6 },
        { title: 'Romanian Deadlift (Barbell)', templateId: '2B4B7310', sets: 4 },
        { title: 'Leg Press', templateId: 'C7973E0E', sets: 3 },
        { title: 'Hanging Leg Raise', templateId: '0ABF1B5B', sets: 3 },
      ],
    },
    {
      hevyWorkoutId: INBOX_WEDNESDAY_UPPER_ID,
      title: 'Push day',
      startUtc: wednesday + 'T18:30:00Z',
      endUtc: wednesday + 'T19:25:00Z',
      localDate: wednesday,
      routineId: null,
      reason: 'no lifting session that day',
      exercises: [
        { title: 'Bench Press (Barbell)', templateId: '79D0BB3A', sets: 5 },
        { title: 'Overhead Press (Barbell)', templateId: '7B8D84E8', sets: 4 },
      ],
    },
  ];
}

export function pullSummaryFixture(overrides: Partial<PullSummary> = {}): PullSummary {
  return { pulled: 0, matched: 0, unmatched: 0, skipped: 0, skippedReason: null, wasSkipped: false, ...overrides };
}

export function pushWeekFixture(date: LocalDate): PushWeekResult {
  const monday = weekStartLocal(date);
  return {
    weekStart: monday,
    pushed: [
      { date: monday, label: 'Upper A', routineId: 'rt_upper_a_w1', title: 'Cut to 85 — Nov 2026 · W1 Upper A', outcome: 'created' },
      { date: addDays(monday, 1), label: 'Lower A', routineId: 'rt_lower_a_w1', title: 'Cut to 85 — Nov 2026 · W1 Lower A', outcome: 'created' },
      { date: addDays(monday, 2), label: 'Norwegian 4×4', routineId: 'rt_4x4', title: 'Cut to 85 — Nov 2026 · Norwegian 4×4', outcome: 'updated' },
      { date: addDays(monday, 2), label: 'Mobility B', routineId: 'rt_mob_b', title: 'Cut to 85 — Nov 2026 · Mobility B', outcome: 'updated' },
      { date: addDays(monday, 3), label: 'Upper B', routineId: 'rt_upper_b_w1', title: 'Cut to 85 — Nov 2026 · W1 Upper B', outcome: 'created' },
      { date: addDays(monday, 4), label: 'Lower B', routineId: 'rt_lower_b_w1', title: 'Cut to 85 — Nov 2026 · W1 Lower B', outcome: 'created' },
    ],
    failed: [
      { date: addDays(monday, 5), label: 'C25K run', error: 'This cardio slot has no protocol; choose one before pushing it to Hevy.' },
    ],
  };
}
