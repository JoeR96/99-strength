/** Story/test handlers for `hevySyncApi` (pull, inbox, assign, ignore, push). */

import { hevySyncApi } from '@/api/hevySync';
import { resetAutoPullThrottle } from '@/hooks/useHevySync';
import { plansApi } from '@/api/plans';
import { todayLocal } from '@/lib/localDate';
import type { HevyInboxItemDto } from '@/types/hevySync';
import { inboxTwoItems, pullSummaryFixture, pushWeekFixture } from '../fixtures/hevyInbox';
import { HEVY_ROUTINE_ID, todayUpperA, todayUpperAWithHevy } from '../fixtures/plan';
import { installHandlers, never, rejectWith } from './install';
import { plansHandlers } from './plans';

type HevySyncHandlers = Partial<typeof hevySyncApi>;

/** A rejection shaped like the API's ProblemDetails: a category `title` and the sentence in `detail`. */
function rejectWithDetail(status: number, detail: string) {
  return () =>
    Promise.reject(
      Object.assign(new Error(`Request failed with status code ${status}`), {
        response: { status, data: { status, title: 'External Service Error', detail } },
      })
    );
}

const HEVY_DOWN = 'Hevy is unavailable, your change is saved locally';

/** An inbox held in memory so Assign and Ignore remove the item like the server would. */
function statefulInbox(initial: HevyInboxItemDto[]): HevySyncHandlers {
  let items = [...initial];
  return {
    pull: async () => pullSummaryFixture(),
    getInbox: async () => [...items],
    assign: async (hevyWorkoutId, request) => {
      items = items.filter((i) => i.hevyWorkoutId !== hevyWorkoutId);
      return {
        hevyWorkoutId,
        completionId: 'comp-from-hevy',
        date: request.date,
        kind: request.kind,
        liftDay: request.liftDay ?? null,
      };
    },
    ignore: async (hevyWorkoutId) => {
      items = items.filter((i) => i.hevyWorkoutId !== hevyWorkoutId);
    },
    pushSession: async (_planId, request) => ({
      routineId: 'rt_' + request.kind + '_' + (request.liftDay ?? 'slot'),
      title: 'Pushed routine',
      outcome: 'created',
    }),
    pushWeek: async (_planId, date) => pushWeekFixture(date ?? todayLocal()),
  };
}

export const hevySyncHandlers = {
  /** Nothing waiting; pulls find nothing. */
  empty: (): HevySyncHandlers => statefulInbox([]),
  /** Two unmatched workouts this week. */
  twoItems: (): HevySyncHandlers => statefulInbox(inboxTwoItems(todayLocal())),
  /** A pull that matched one workout. */
  pullMatchesOne: (): HevySyncHandlers => ({
    ...statefulInbox([]),
    pull: async () => pullSummaryFixture({ pulled: 1, matched: 1 }),
  }),
  /** Hevy is unreachable: the inbox still lists, everything that talks to Hevy fails. */
  writesFail: (): HevySyncHandlers => ({
    ...statefulInbox(inboxTwoItems(todayLocal())),
    pull: rejectWithDetail(502, HEVY_DOWN),
    assign: rejectWithDetail(502, HEVY_DOWN),
    ignore: rejectWithDetail(502, HEVY_DOWN),
    pushSession: rejectWithDetail(502, HEVY_DOWN),
    pushWeek: rejectWithDetail(502, HEVY_DOWN),
  }),
  loading: (): HevySyncHandlers => ({
    pull: never,
    getInbox: never,
    assign: never,
    ignore: never,
    pushSession: never,
    pushWeek: never,
  }),
  error: (): HevySyncHandlers => ({
    pull: rejectWith(500),
    getInbox: rejectWith(500),
    assign: rejectWith(500),
    ignore: rejectWith(500),
    pushSession: rejectWith(500),
    pushWeek: rejectWith(500),
  }),
};

/** Installs the stubs and lets the next Today mount pull again (automatic pulls are spaced a minute apart). */
export function installHevySyncHandlers(handlers: HevySyncHandlers = hevySyncHandlers.empty()): void {
  installHandlers(hevySyncApi, handlers);
  resetAutoPullThrottle();
}

/**
 * Monday's Upper A with no routine yet, wired so that Push to Hevy works end to end: once
 * `pushSession` has answered, `/plans/today` carries the routine id and the card turns into
 * Open in Hevy. Installs both API stubs.
 */
export function installPushFlowHandlers(): void {
  let pushed = false;
  installHandlers(plansApi, {
    ...plansHandlers.default(),
    getToday: async (date) => (pushed ? todayUpperAWithHevy : todayUpperA)(date ?? todayLocal()),
  });
  installHevySyncHandlers({
    ...hevySyncHandlers.empty(),
    pushSession: async () => {
      pushed = true;
      return { routineId: HEVY_ROUTINE_ID, title: 'Cut to 85 — Nov 2026 · W1 Upper A', outcome: 'created' };
    },
  });
}
