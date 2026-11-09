/**
 * Hevy push/pull from the browser's side: pull completed workouts (on open, on refresh, after
 * coming back from Hevy), push a session or a week as routines, and work the unmatched inbox.
 * Every call is one request to our API; the Hevy key never leaves the server. Nothing here blocks
 * render: pulls are mutations fired from effects and buttons, and Today re-reads afterwards.
 *
 * The mutations run with `networkMode: 'always'` so that a tap with no signal fails straight away
 * (and can say so) instead of sitting paused until the connection returns.
 */

import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { hevySyncApi } from '@/api/hevySync';
import { useLocalToday } from '@/hooks/useLocalToday';
import { planKeys } from '@/hooks/usePlan';
import type { LocalDate } from '@/lib/localDate';
import { isNetworkError } from '@/offline/errors';
import { consumeHevyReturn } from '@/features/today/hevyDeepLink';
import { CompletionSource, SessionStatus, type TodayDto, type TrainingPlanDto } from '@/types/plan';
import type { AssignHevyWorkoutRequest, PullSummary, PushSessionRequest } from '@/types/hevySync';

export const hevySyncKeys = {
  all: ['hevy-sync'] as const,
  inbox: () => [...hevySyncKeys.all, 'inbox'] as const,
};

/**
 * What to tell the person about a failed Hevy call: "no connection" when the request never got an
 * answer, else the server's own sentence (the ProblemDetails `detail`, such as "Hevy Pro is
 * required for the API"), else `fallback`.
 */
export function hevyErrorMessage(error: unknown, fallback: string): string {
  if (isNetworkError(error)) return "No connection. Try again when you're back online.";
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const detail = (error as { response?: { data?: { detail?: unknown } } }).response?.data?.detail;
    if (typeof detail === 'string' && detail.trim() !== '') return detail;
  }
  return fallback;
}

/** Pulled workouts no session fitted. Only asked for when a key is stored. */
export function useHevyInbox(enabled = true) {
  return useQuery({
    queryKey: hevySyncKeys.inbox(),
    queryFn: () => hevySyncApi.getInbox(),
    enabled,
    staleTime: 60 * 1000,
  });
}

/** After a pull, push or assign the plan views and the inbox are stale. Resolves once active ones refetch. */
export function invalidateAfterHevySync(queryClient: QueryClient) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: [...planKeys.all, 'today'] }),
    queryClient.invalidateQueries({ queryKey: [...planKeys.all, 'weekly-goals'] }),
    queryClient.invalidateQueries({ queryKey: [...planKeys.all, 'calendar'] }),
    queryClient.invalidateQueries({ queryKey: planKeys.active() }),
    queryClient.invalidateQueries({ queryKey: hevySyncKeys.inbox() }),
  ]);
}

function doneKeys(today: TodayDto): Set<string> {
  return new Set(today.sessions.filter((s) => s.status === SessionStatus.Done).map((s) => s.kind + ':' + s.label));
}

/** Labels of sessions that went from not-done to Done between two reads of the same day. */
export function newlyDoneLabels(before: TodayDto | null | undefined, after: TodayDto | null | undefined): string[] {
  if (!before || !after) return [];
  const wasDone = doneKeys(before);
  return after.sessions
    .filter((s) => s.status === SessionStatus.Done && !wasDone.has(s.kind + ':' + s.label))
    .map((s) => s.label);
}

/** Labels of the day's sessions whose completion came from Hevy. */
export function hevyDoneLabels(today: TodayDto | null | undefined, plan: TrainingPlanDto | null | undefined): string[] {
  if (!today || !plan) return [];
  const fromHevy = new Set(plan.completions.filter((c) => c.source === CompletionSource.Hevy).map((c) => c.id));
  return today.sessions
    .filter((s) => s.status === SessionStatus.Done && s.completionId !== null && fromHevy.has(s.completionId))
    .map((s) => s.label);
}

export interface PullHevyVariables {
  /** Re-walk from the plan start instead of the last pull. */
  force?: boolean;
  /** The person asked for it: always re-read the plan afterwards, even when nothing came back. */
  manual?: boolean;
  /**
   * `matches` (default) toasts only when a workout matched; `always` also says when nothing new
   * came back (a Refresh tap); `never` leaves the telling to the caller.
   */
  announce?: 'matches' | 'always' | 'never';
}

export interface PullHevyOutcome {
  summary: PullSummary;
  /** Today's sessions the pull completed, by label. */
  matchedToday: string[];
}

/** "Matched Upper A from Hevy", or a count when the matches were not today's sessions. */
export function matchedMessage(outcome: PullHevyOutcome): string {
  if (outcome.matchedToday.length > 0) {
    return 'Matched ' + outcome.matchedToday.join(' and ') + ' from Hevy';
  }
  const n = outcome.summary.matched;
  return 'Matched ' + n + (n === 1 ? ' workout' : ' workouts') + ' from Hevy';
}

/** One line for a pull that matched nothing. */
export function quietPullMessage(summary: PullSummary): string {
  if (summary.wasSkipped) return 'Nothing pulled: ' + (summary.skippedReason ?? 'Hevy sync is not set up');
  if (summary.unmatched > 0) {
    return summary.unmatched + (summary.unmatched === 1 ? ' new workout' : ' new workouts') + ' in the Hevy inbox';
  }
  return 'Nothing new from Hevy';
}

/**
 * Pulls workout events and completes matched sessions, then re-reads the plan views. Toasts which
 * of today's sessions it completed ("Matched Upper A from Hevy"). Errors are the caller's to show.
 */
export function usePullHevy() {
  const queryClient = useQueryClient();
  const date = useLocalToday();
  return useMutation({
    networkMode: 'always',
    mutationFn: async ({ force = false, manual = false }: PullHevyVariables = {}): Promise<PullHevyOutcome> => {
      const before = queryClient.getQueryData<TodayDto | null>(planKeys.today(date));
      const summary = await hevySyncApi.pull(force);
      const changed = summary.matched > 0 || summary.unmatched > 0 || summary.pulled > 0;
      if (changed || manual) {
        await invalidateAfterHevySync(queryClient);
      }
      if (summary.matched === 0) return { summary, matchedToday: [] };

      const after = queryClient.getQueryData<TodayDto | null>(planKeys.today(date));
      // On app open the pull can start before today has loaded, leaving nothing to compare with.
      // Then the sessions Hevy completed are named instead, no more of them than the pull matched.
      const matchedToday =
        before !== undefined
          ? newlyDoneLabels(before, after)
          : hevyDoneLabels(after, queryClient.getQueryData<TrainingPlanDto | null>(planKeys.active())).slice(0, summary.matched);
      return { summary, matchedToday };
    },
    onSuccess: (outcome, variables) => {
      const announce = variables?.announce ?? 'matches';
      if (announce === 'never') return;
      if (outcome.summary.matched > 0) {
        toast.success(matchedMessage(outcome));
      } else if (announce === 'always') {
        toast.success(quietPullMessage(outcome.summary));
      }
    },
  });
}

// Automatic pulls are spaced out so React StrictMode's double effects, a quick lock/unlock and
// hopping between tabs do not hammer the server; the 15-minute job covers the rest.
export const AUTO_PULL_MIN_GAP_MS = 60 * 1000;
let lastAutoPullAt = 0;

/** Test hook: forget the last automatic pull so the next mount pulls again. */
export function resetAutoPullThrottle(): void {
  lastAutoPullAt = 0;
}

/**
 * Pulls when the screen mounts and whenever the app comes back into view (a phone PWA is resumed
 * far more often than it is opened), at most once a minute; coming back from "Open in Hevy" always
 * pulls. Only runs while `enabled` (a key is stored and there is a connection). Returns the pull
 * mutation so the screen can also pull on demand and show a failure.
 */
export function useAutoPullHevy(enabled: boolean) {
  const pull = usePullHevy();
  const { mutate } = pull;

  useEffect(() => {
    if (!enabled) return;

    const pullIfDue = (always: boolean) => {
      const now = Date.now();
      if (!always && now - lastAutoPullAt < AUTO_PULL_MIN_GAP_MS) return;
      lastAutoPullAt = now;
      mutate({});
    };

    pullIfDue(false);

    const onVisible = () => {
      if (document.visibilityState !== 'visible') return;
      pullIfDue(consumeHevyReturn());
    };
    document.addEventListener('visibilitychange', onVisible);
    return () => document.removeEventListener('visibilitychange', onVisible);
  }, [enabled, mutate]);

  return pull;
}

export interface PushSessionVariables {
  planId: string;
  request: PushSessionRequest;
}

/** Pushes one session as a Hevy routine and re-reads Today so the card shows Open in Hevy. */
export function usePushSession() {
  const queryClient = useQueryClient();
  return useMutation({
    networkMode: 'always',
    mutationFn: ({ planId, request }: PushSessionVariables) => hevySyncApi.pushSession(planId, request),
    onSuccess: () => invalidateAfterHevySync(queryClient),
  });
}

/** Pushes the Mon–Sun week containing `date` (today when omitted). */
export function usePushWeek() {
  const queryClient = useQueryClient();
  return useMutation({
    networkMode: 'always',
    mutationFn: ({ planId, date }: { planId: string; date?: LocalDate }) => hevySyncApi.pushWeek(planId, date),
    onSuccess: () => invalidateAfterHevySync(queryClient),
  });
}

export interface AssignHevyWorkoutVariables {
  hevyWorkoutId: string;
  request: AssignHevyWorkoutRequest;
}

export function useAssignHevyWorkout() {
  const queryClient = useQueryClient();
  return useMutation({
    networkMode: 'always',
    mutationFn: ({ hevyWorkoutId, request }: AssignHevyWorkoutVariables) => hevySyncApi.assign(hevyWorkoutId, request),
    onSuccess: () => invalidateAfterHevySync(queryClient),
  });
}

export function useIgnoreHevyWorkout() {
  const queryClient = useQueryClient();
  return useMutation({
    networkMode: 'always',
    mutationFn: ({ hevyWorkoutId }: { hevyWorkoutId: string }) => hevySyncApi.ignore(hevyWorkoutId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: hevySyncKeys.inbox() }),
  });
}
