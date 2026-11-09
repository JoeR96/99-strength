import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import toast from 'react-hot-toast';
import { hevySyncApi } from '@/api/hevySync';
import { plansApi } from '@/api/plans';
import { consumeHevyReturn, openHevyRoutine } from '@/features/today/hevyDeepLink';
import { pullSummaryFixture } from '@/mocks/fixtures/hevyInbox';
import { PLAN_ID, planFixture, todayDone, todayUpperA } from '@/mocks/fixtures/plan';
import { CompletionSource, SessionKind, type TrainingPlanDto } from '@/types/plan';
import { planKeys, useActivePlan, useToday } from './usePlan';
import {
  AUTO_PULL_MIN_GAP_MS,
  hevyDoneLabels,
  hevyErrorMessage,
  hevySyncKeys,
  matchedMessage,
  newlyDoneLabels,
  quietPullMessage,
  resetAutoPullThrottle,
  useAutoPullHevy,
  useAssignHevyWorkout,
  useHevyInbox,
  useIgnoreHevyWorkout,
  usePullHevy,
  usePushSession,
  usePushWeek,
} from './useHevySync';

vi.mock('@/api/hevySync', () => ({
  hevySyncApi: {
    pull: vi.fn(),
    getInbox: vi.fn(),
    assign: vi.fn(),
    ignore: vi.fn(),
    pushSession: vi.fn(),
    pushWeek: vi.fn(),
  },
}));

vi.mock('@/api/plans', () => ({
  plansApi: {
    getActive: vi.fn(),
    getToday: vi.fn(),
    getWeeklyGoals: vi.fn(),
  },
}));

vi.mock('react-hot-toast', () => ({
  default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }),
}));

vi.mock('@/lib/localDate', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/localDate')>();
  return { ...actual, todayLocal: vi.fn(() => '2026-11-09') };
});

const DATE = '2026-11-09';

function createClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 5 * 60 * 1000 }, mutations: { retry: false } },
  });
}

function wrapperFor(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

/** The plan after Hevy completed Monday's Upper A (completion `comp-0001`, as in `todayDone`). */
const planWithHevyCompletion: TrainingPlanDto = {
  ...planFixture,
  completions: [
    {
      id: 'comp-0001',
      date: DATE,
      kind: SessionKind.Lift,
      liftDay: 1,
      cardioProtocolId: null,
      mobilityRoutineId: null,
      source: CompletionSource.Hevy,
      hevyWorkoutId: 'hw_1',
      recordedAtUtc: '2026-11-09T18:40:00Z',
      cardio: null,
      drillTicks: null,
    },
  ],
};

function setVisibility(state: 'visible' | 'hidden') {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => state });
  act(() => {
    document.dispatchEvent(new Event('visibilitychange'));
  });
}

describe('useHevySync', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    resetAutoPullThrottle();
    consumeHevyReturn();
    vi.mocked(hevySyncApi.pull).mockResolvedValue(pullSummaryFixture());
    vi.mocked(hevySyncApi.getInbox).mockResolvedValue([]);
    vi.mocked(plansApi.getToday).mockImplementation(async (date) => todayUpperA(date ?? DATE));
    vi.mocked(plansApi.getActive).mockResolvedValue(planFixture);
  });

  afterEach(() => {
    delete (document as { visibilityState?: string }).visibilityState;
    vi.useRealTimers();
  });

  describe('messages', () => {
    it('names the sessions that became done between two reads of today', () => {
      expect(newlyDoneLabels(todayUpperA(DATE), todayDone(DATE))).toEqual(['Upper A']);
      expect(newlyDoneLabels(todayDone(DATE), todayDone(DATE))).toEqual([]);
      expect(newlyDoneLabels(undefined, todayDone(DATE))).toEqual([]);
      expect(newlyDoneLabels(todayUpperA(DATE), null)).toEqual([]);
    });

    it('names the sessions Hevy completed, and not the ones marked done by hand', () => {
      expect(hevyDoneLabels(todayDone(DATE), planWithHevyCompletion)).toEqual(['Upper A']);
      expect(hevyDoneLabels(todayDone(DATE), planFixture)).toEqual([]);
      expect(hevyDoneLabels(todayUpperA(DATE), planWithHevyCompletion)).toEqual([]);
      expect(hevyDoneLabels(null, planWithHevyCompletion)).toEqual([]);
    });

    it('says what matched, by name when it was today and by count otherwise', () => {
      expect(matchedMessage({ summary: pullSummaryFixture({ matched: 1 }), matchedToday: ['Upper A'] })).toBe('Matched Upper A from Hevy');
      expect(
        matchedMessage({ summary: pullSummaryFixture({ matched: 2 }), matchedToday: ['Norwegian 4×4', 'Mobility B'] })
      ).toBe('Matched Norwegian 4×4 and Mobility B from Hevy');
      expect(matchedMessage({ summary: pullSummaryFixture({ matched: 1 }), matchedToday: [] })).toBe('Matched 1 workout from Hevy');
      expect(matchedMessage({ summary: pullSummaryFixture({ matched: 3 }), matchedToday: [] })).toBe('Matched 3 workouts from Hevy');
    });

    it('has one line for a pull that matched nothing', () => {
      expect(quietPullMessage(pullSummaryFixture())).toBe('Nothing new from Hevy');
      expect(quietPullMessage(pullSummaryFixture({ pulled: 1, unmatched: 1 }))).toBe('1 new workout in the Hevy inbox');
      expect(quietPullMessage(pullSummaryFixture({ pulled: 2, unmatched: 2 }))).toBe('2 new workouts in the Hevy inbox');
      expect(quietPullMessage(pullSummaryFixture({ wasSkipped: true, skippedReason: 'no active plan' }))).toBe(
        'Nothing pulled: no active plan'
      );
    });

    it("prefers the server's sentence, says so when there is no connection, and otherwise falls back", () => {
      const problem = { response: { status: 502, data: { title: 'External Service Error', detail: 'Hevy Pro is required for the API' } } };
      expect(hevyErrorMessage(problem, 'fallback')).toBe('Hevy Pro is required for the API');
      expect(hevyErrorMessage({ response: { status: 500, data: { title: 'An error occurred' } } }, 'fallback')).toBe('fallback');
      expect(hevyErrorMessage(new Error('boom'), 'fallback')).toBe('fallback');
      expect(hevyErrorMessage(Object.assign(new Error('Network Error'), { isAxiosError: true, code: 'ERR_NETWORK' }), 'fallback')).toMatch(
        /no connection/i
      );
    });
  });

  describe('usePullHevy', () => {
    it('toasts the session a pull completed and re-reads today', async () => {
      const client = createClient();
      const wrapper = wrapperFor(client);
      const today = renderHook(() => useToday(DATE), { wrapper });
      await waitFor(() => expect(today.result.current.data).toBeDefined());

      vi.mocked(hevySyncApi.pull).mockImplementation(async () => {
        // The server completed Upper A from the pulled workout.
        vi.mocked(plansApi.getToday).mockImplementation(async (date) => todayDone(date ?? DATE));
        return pullSummaryFixture({ pulled: 1, matched: 1 });
      });
      const { result } = renderHook(() => usePullHevy(), { wrapper });

      act(() => result.current.mutate({}));

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(hevySyncApi.pull).toHaveBeenCalledWith(false);
      expect(result.current.data).toEqual({ summary: pullSummaryFixture({ pulled: 1, matched: 1 }), matchedToday: ['Upper A'] });
      expect(toast.success).toHaveBeenCalledWith('Matched Upper A from Hevy');
      expect(plansApi.getToday).toHaveBeenCalledTimes(2);
      await waitFor(() => expect(today.result.current.data?.sessions[0].completionId).toBe('comp-0001'));
    });

    it('when the pull started before today had loaded, names the sessions Hevy completed', async () => {
      const client = createClient();
      const wrapper = wrapperFor(client);
      vi.mocked(plansApi.getToday).mockImplementation(async (date) => todayDone(date ?? DATE));
      vi.mocked(plansApi.getActive).mockResolvedValue(planWithHevyCompletion);
      let release: (() => void) | undefined;
      vi.mocked(hevySyncApi.pull).mockImplementation(
        () => new Promise((resolve) => (release = () => resolve(pullSummaryFixture({ pulled: 1, matched: 1 }))))
      );

      // The pull goes out first; only then do the page's queries mount and load.
      const { result } = renderHook(() => usePullHevy(), { wrapper });
      act(() => result.current.mutate({}));
      const page = renderHook(() => ({ today: useToday(DATE), plan: useActivePlan() }), { wrapper });
      await waitFor(() => expect(page.result.current.today.data).toBeDefined());
      await waitFor(() => expect(page.result.current.plan.data).toBeDefined());
      await waitFor(() => expect(release).toBeDefined());
      act(() => release!());

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data?.matchedToday).toEqual(['Upper A']);
      expect(toast.success).toHaveBeenCalledWith('Matched Upper A from Hevy');
    });

    it('a quiet pull neither toasts nor re-reads the plan', async () => {
      const client = createClient();
      const wrapper = wrapperFor(client);
      const today = renderHook(() => useToday(DATE), { wrapper });
      await waitFor(() => expect(today.result.current.data).toBeDefined());
      const { result } = renderHook(() => usePullHevy(), { wrapper });

      act(() => result.current.mutate({}));

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(toast.success).not.toHaveBeenCalled();
      expect(plansApi.getToday).toHaveBeenCalledTimes(1);
    });

    it('a manual pull always re-reads the plan and can say that nothing was new', async () => {
      const client = createClient();
      const wrapper = wrapperFor(client);
      const today = renderHook(() => useToday(DATE), { wrapper });
      await waitFor(() => expect(today.result.current.data).toBeDefined());
      const { result } = renderHook(() => usePullHevy(), { wrapper });

      act(() => result.current.mutate({ manual: true, announce: 'always' }));

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(toast.success).toHaveBeenCalledWith('Nothing new from Hevy');
      expect(plansApi.getToday).toHaveBeenCalledTimes(2);
    });

    it('announce "never" leaves the telling to the caller, and force is passed through', async () => {
      const client = createClient();
      vi.mocked(hevySyncApi.pull).mockResolvedValue(pullSummaryFixture({ pulled: 2, matched: 2 }));
      const { result } = renderHook(() => usePullHevy(), { wrapper: wrapperFor(client) });

      act(() => result.current.mutate({ force: true, announce: 'never' }));

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(hevySyncApi.pull).toHaveBeenCalledWith(true);
      expect(toast.success).not.toHaveBeenCalled();
      expect(result.current.data?.summary.matched).toBe(2);
    });

    it('a failed pull surfaces as an error and leaves today alone', async () => {
      const client = createClient();
      const wrapper = wrapperFor(client);
      const today = renderHook(() => useToday(DATE), { wrapper });
      await waitFor(() => expect(today.result.current.data).toBeDefined());
      vi.mocked(hevySyncApi.pull).mockRejectedValue({ response: { status: 502, data: { detail: 'Hevy is unavailable' } } });
      const { result } = renderHook(() => usePullHevy(), { wrapper });

      act(() => result.current.mutate({}));

      await waitFor(() => expect(result.current.isError).toBe(true));
      expect(hevyErrorMessage(result.current.error, 'x')).toBe('Hevy is unavailable');
      expect(toast.success).not.toHaveBeenCalled();
      expect(today.result.current.data?.sessions[0].label).toBe('Upper A');
    });
  });

  describe('useAutoPullHevy', () => {
    it('pulls once on mount when enabled', async () => {
      const client = createClient();
      const { rerender } = renderHook(() => useAutoPullHevy(true), { wrapper: wrapperFor(client) });

      await waitFor(() => expect(hevySyncApi.pull).toHaveBeenCalledTimes(1));
      rerender();
      rerender();
      expect(hevySyncApi.pull).toHaveBeenCalledTimes(1);
    });

    it('does not pull without a stored key, and pulls once the key is known', async () => {
      const client = createClient();
      const { rerender } = renderHook(({ enabled }) => useAutoPullHevy(enabled), {
        wrapper: wrapperFor(client),
        initialProps: { enabled: false },
      });
      await act(async () => {
        await Promise.resolve();
      });
      expect(hevySyncApi.pull).not.toHaveBeenCalled();

      rerender({ enabled: true });

      await waitFor(() => expect(hevySyncApi.pull).toHaveBeenCalledTimes(1));
    });

    it('a second mount within a minute does not pull again; after a minute it does', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.setSystemTime(new Date('2026-11-09T09:00:00Z'));
      const client = createClient();
      const wrapper = wrapperFor(client);

      const first = renderHook(() => useAutoPullHevy(true), { wrapper });
      await waitFor(() => expect(hevySyncApi.pull).toHaveBeenCalledTimes(1));
      first.unmount();

      vi.setSystemTime(new Date(Date.parse('2026-11-09T09:00:00Z') + AUTO_PULL_MIN_GAP_MS - 1000));
      const second = renderHook(() => useAutoPullHevy(true), { wrapper });
      await act(async () => {
        await Promise.resolve();
      });
      expect(hevySyncApi.pull).toHaveBeenCalledTimes(1);
      second.unmount();

      vi.setSystemTime(new Date(Date.parse('2026-11-09T09:00:00Z') + AUTO_PULL_MIN_GAP_MS + 1000));
      renderHook(() => useAutoPullHevy(true), { wrapper });
      await waitFor(() => expect(hevySyncApi.pull).toHaveBeenCalledTimes(2));
    });

    it('pulls again when the tab comes back after Open in Hevy, even inside the minute', async () => {
      const client = createClient();
      renderHook(() => useAutoPullHevy(true), { wrapper: wrapperFor(client) });
      await waitFor(() => expect(hevySyncApi.pull).toHaveBeenCalledTimes(1));

      // Coming back without having left for Hevy, inside the minute: nothing.
      setVisibility('hidden');
      setVisibility('visible');
      expect(hevySyncApi.pull).toHaveBeenCalledTimes(1);

      const cancel = openHevyRoutine('rt_1', { navigate: vi.fn(), openWeb: vi.fn(), isVisible: () => false });
      cancel();
      setVisibility('hidden');
      expect(hevySyncApi.pull).toHaveBeenCalledTimes(1);
      setVisibility('visible');

      await waitFor(() => expect(hevySyncApi.pull).toHaveBeenCalledTimes(2));
      // The return was used up: the next visibility change does not pull a third time.
      setVisibility('hidden');
      setVisibility('visible');
      expect(hevySyncApi.pull).toHaveBeenCalledTimes(2);
    });

    it('resuming the app after more than a minute pulls again', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.setSystemTime(new Date('2026-11-09T09:00:00Z'));
      const client = createClient();
      renderHook(() => useAutoPullHevy(true), { wrapper: wrapperFor(client) });
      await waitFor(() => expect(hevySyncApi.pull).toHaveBeenCalledTimes(1));

      setVisibility('hidden');
      vi.setSystemTime(new Date('2026-11-09T10:15:00Z'));
      setVisibility('visible');

      await waitFor(() => expect(hevySyncApi.pull).toHaveBeenCalledTimes(2));
    });

    it('stops listening when disabled or unmounted', async () => {
      const client = createClient();
      const { rerender, unmount } = renderHook(({ enabled }) => useAutoPullHevy(enabled), {
        wrapper: wrapperFor(client),
        initialProps: { enabled: true },
      });
      await waitFor(() => expect(hevySyncApi.pull).toHaveBeenCalledTimes(1));

      rerender({ enabled: false });
      openHevyRoutine('rt_1', { navigate: vi.fn(), openWeb: vi.fn(), isVisible: () => false })();
      setVisibility('hidden');
      setVisibility('visible');
      unmount();
      setVisibility('hidden');
      setVisibility('visible');

      expect(hevySyncApi.pull).toHaveBeenCalledTimes(1);
    });
  });

  describe('push, inbox, assign, ignore', () => {
    it('usePushSession posts the session and re-reads today so the card can show Open in Hevy', async () => {
      const client = createClient();
      const wrapper = wrapperFor(client);
      const today = renderHook(() => useToday(DATE), { wrapper });
      await waitFor(() => expect(today.result.current.data).toBeDefined());
      vi.mocked(hevySyncApi.pushSession).mockResolvedValue({ routineId: 'rt_1', title: 'W1 Upper A', outcome: 'created' });
      const { result } = renderHook(() => usePushSession(), { wrapper });

      const request = { date: DATE, kind: SessionKind.Lift, liftDay: 1 as const };
      act(() => result.current.mutate({ planId: PLAN_ID, request }));

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(hevySyncApi.pushSession).toHaveBeenCalledWith(PLAN_ID, request);
      expect(plansApi.getToday).toHaveBeenCalledTimes(2);
    });

    it('usePushWeek posts the week and marks the plan views stale', async () => {
      const client = createClient();
      const invalidate = vi.spyOn(client, 'invalidateQueries');
      vi.mocked(hevySyncApi.pushWeek).mockResolvedValue({ weekStart: DATE, pushed: [], failed: [] });
      const { result } = renderHook(() => usePushWeek(), { wrapper: wrapperFor(client) });

      act(() => result.current.mutate({ planId: PLAN_ID }));

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(hevySyncApi.pushWeek).toHaveBeenCalledWith(PLAN_ID, undefined);
      expect(invalidate).toHaveBeenCalledWith({ queryKey: [...planKeys.all, 'today'] });
      expect(invalidate).toHaveBeenCalledWith({ queryKey: [...planKeys.all, 'calendar'] });
    });

    it('useHevyInbox is only asked for when enabled', async () => {
      const client = createClient();
      const wrapper = wrapperFor(client);
      const off = renderHook(() => useHevyInbox(false), { wrapper });
      expect(off.result.current.fetchStatus).toBe('idle');
      expect(hevySyncApi.getInbox).not.toHaveBeenCalled();

      const on = renderHook(() => useHevyInbox(true), { wrapper });
      await waitFor(() => expect(on.result.current.isSuccess).toBe(true));
      expect(hevySyncApi.getInbox).toHaveBeenCalledTimes(1);
    });

    it('assigning re-reads the inbox and the plan; ignoring re-reads the inbox', async () => {
      const client = createClient();
      const wrapper = wrapperFor(client);
      const inbox = renderHook(() => useHevyInbox(true), { wrapper });
      await waitFor(() => expect(inbox.result.current.isSuccess).toBe(true));
      const invalidate = vi.spyOn(client, 'invalidateQueries');
      vi.mocked(hevySyncApi.assign).mockResolvedValue({
        hevyWorkoutId: 'hw_1',
        completionId: 'c1',
        date: DATE,
        kind: SessionKind.Lift,
        liftDay: 1,
      });
      vi.mocked(hevySyncApi.ignore).mockResolvedValue(undefined);

      const assign = renderHook(() => useAssignHevyWorkout(), { wrapper });
      const request = { date: DATE, kind: SessionKind.Lift, liftDay: 1 as const, extra: false };
      act(() => assign.result.current.mutate({ hevyWorkoutId: 'hw_1', request }));
      await waitFor(() => expect(assign.result.current.isSuccess).toBe(true));
      expect(hevySyncApi.assign).toHaveBeenCalledWith('hw_1', request);
      expect(invalidate).toHaveBeenCalledWith({ queryKey: hevySyncKeys.inbox() });
      expect(invalidate).toHaveBeenCalledWith({ queryKey: [...planKeys.all, 'today'] });
      expect(hevySyncApi.getInbox).toHaveBeenCalledTimes(2);

      const ignore = renderHook(() => useIgnoreHevyWorkout(), { wrapper });
      act(() => ignore.result.current.mutate({ hevyWorkoutId: 'hw_2' }));
      await waitFor(() => expect(ignore.result.current.isSuccess).toBe(true));
      expect(hevySyncApi.ignore).toHaveBeenCalledWith('hw_2');
      expect(hevySyncApi.getInbox).toHaveBeenCalledTimes(3);
    });
  });
});
