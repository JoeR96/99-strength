/**
 * Current workout (`/today`, also `/current`): the training cycle, not the calendar. It always shows
 * the next session to do in the A2S2 cycle (the workout's current week and day, whatever the date):
 * exercises, the cardio finisher, warm-up, estimated time and Start → gym mode. After a finish the
 * cycle advances and the next session shows straight away. Below: the home routine the plan pairs
 * with this session, the rest of the cycle (tap to preview read-only, "Preview next week"), and the
 * day's habits. Only "no active workout" shows the empty state with the plan templates.
 *
 * With a Hevy key stored it pulls in the background on mount, on resume and on Refresh / pull-to-refresh.
 */

import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Play, RefreshCw } from 'lucide-react';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { useLocalToday } from '@/hooks/useLocalToday';
import { useCurrentCycle } from '@/hooks/useWorkouts';
import { useToday } from '@/hooks/usePlan';
import { hevyErrorMessage, useAutoPullHevy, useHevyInbox } from '@/hooks/useHevySync';
import { usePullToRefresh } from '@/hooks/usePullToRefresh';
import { useHevyStatus } from '@/contexts/hevyStatusContext';
import { useOnlineStatus } from '@/offline/useOnlineStatus';
import { usePendingSyncCount } from '@/offline/context';
import { useMobilityRoutines } from '@/hooks/useMobilityRoutines';
import { useRetryHevyPost } from '@/hooks/useGymSession';
import { useHabitsToday } from '@/hooks/useHabits';
import { PendingSyncBadge } from '@/features/today/PendingSyncBadge';
import { StreakChip } from '@/features/today/StreakChip';
import { HevySyncNotice } from '@/features/today/HevySyncNotice';
import { StartSuggestedPlanCard } from '@/features/today/StartSuggestedPlanCard';
import { TodayPhaseHeadsUp } from '@/features/today/PhaseHeadsUpCard';
import { CurrentSessionCard } from './CurrentSessionCard';
import { CycleList } from './CycleList';
import { LastSessionStrip } from './LastSessionStrip';
import { TodayHabits } from './TodayHabits';
import { splitCycle } from './cycle';

export interface CurrentWorkoutPageProps {
  /** Overrides the offline queue length shown in the header (stories). */
  pendingSyncCount?: number;
}

export function CurrentWorkoutPage({ pendingSyncCount }: CurrentWorkoutPageProps) {
  const date = useLocalToday();
  const cycleQuery = useCurrentCycle();
  const todayQuery = useToday(date);
  const habitsQuery = useHabitsToday(date);
  const routines = useMobilityRoutines();
  const queuedCount = usePendingSyncCount();
  const pendingCount = pendingSyncCount ?? queuedCount;

  const { isConfigured: hevyConfigured } = useHevyStatus();
  const online = useOnlineStatus();
  const pull = useAutoPullHevy(hevyConfigured && online);
  const inboxQuery = useHevyInbox(hevyConfigured);
  const retryHevy = useRetryHevyPost();

  const refresh = () => {
    if (pull.isPending) return;
    if (!online) {
      toast("You're offline. Showing what's saved on this device.");
      return;
    }
    if (hevyConfigured) pull.mutate({ manual: true, announce: 'always' });
    void cycleQuery.refetch();
    void todayQuery.refetch();
  };
  const pullGesture = usePullToRefresh(refresh);
  const refreshing = pull.isPending || (cycleQuery.isRefetching && !cycleQuery.isLoading);

  const cycle = cycleQuery.data;
  const split = cycle ? splitCycle(cycle) : null;
  const current = split?.current ?? null;
  const routineList = routines.data ?? [];
  const warmUp = current?.plan.warmUpRoutineId ? routineList.find((r) => r.id === current.plan.warmUpRoutineId) ?? null : null;
  const paired = current?.plan.pairedRoutineId ? routineList.find((r) => r.id === current.plan.pairedRoutineId) ?? null : null;

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page space-y-4 py-4">
        {pullGesture.distance > 0 && (
          <p
            className="flex items-end justify-center overflow-hidden text-sm text-muted-foreground"
            style={{ height: pullGesture.distance / 2 }}
            aria-hidden="true"
            data-testid="pull-to-refresh-hint"
          >
            {pullGesture.ready ? 'Release to refresh' : 'Pull to refresh'}
          </p>
        )}

        <header className="flex items-center justify-between gap-3">
          <div className="min-w-0">
            <p className="truncate text-sm text-muted-foreground">{cycle?.workoutName ?? 'Workout'}</p>
            <h1 className="text-2xl font-bold text-foreground">Current workout</h1>
          </div>
          <div className="flex items-center gap-2">
            <PendingSyncBadge count={pendingCount} />
            {habitsQuery.data && <StreakChip streak={habitsQuery.data.streak} />}
            <Button
              variant="outline"
              size="icon"
              className="h-11 w-11"
              onClick={refresh}
              disabled={refreshing}
              aria-label={hevyConfigured ? 'Refresh and pull from Hevy' : 'Refresh'}
              data-testid="today-refresh"
            >
              <RefreshCw className={refreshing ? 'animate-spin' : undefined} aria-hidden="true" />
            </Button>
          </div>
        </header>

        {hevyConfigured && (
          <HevySyncNotice
            inboxCount={inboxQuery.data?.length ?? 0}
            pullError={pull.isError ? hevyErrorMessage(pull.error, "Couldn't pull from Hevy.") : null}
            onRetry={refresh}
            retrying={pull.isPending}
            offline={!online}
          />
        )}

        {todayQuery.data && (
          <LastSessionStrip
            sessions={todayQuery.data.sessions}
            hevyConfigured={hevyConfigured}
            onRetry={(id) => retryHevy.mutate(id)}
            retrying={retryHevy.isPending}
          />
        )}

        {cycleQuery.isLoading && <div className="h-64 animate-pulse rounded-lg bg-muted" aria-hidden="true" />}

        {cycleQuery.isError && (
          <section className="rounded-lg border border-destructive/50 bg-card p-4" role="alert">
            <p className="text-sm text-foreground">Couldn't load your workout.</p>
            <Button variant="outline" className="mt-3 h-11" onClick={() => void cycleQuery.refetch()}>
              <RefreshCw aria-hidden="true" />
              Try again
            </Button>
          </section>
        )}

        {cycle === null && (
          <>
            <StartSuggestedPlanCard
              onStarted={(result) => {
                toast.success(result.alreadyStarted ? 'Your plan is already running' : 'Plan started');
                void cycleQuery.refetch();
                void todayQuery.refetch();
              }}
            />
            <Link to="/plans/templates" className="inline-flex min-h-11 items-center text-sm font-medium text-primary underline-offset-4 hover:underline">
              Browse plan templates
            </Link>
          </>
        )}

        {cycle?.isCompleted && (
          <section className="rounded-lg border border-border bg-card p-4" data-testid="program-complete">
            <h2 className="text-xl font-semibold text-foreground">Program complete</h2>
            <p className="mt-1 text-sm text-muted-foreground">All {cycle.totalWeeks} weeks done. Restart or pick a new plan.</p>
            <Button asChild variant="outline" className="mt-3 h-11">
              <Link to="/workout">Open the plan</Link>
            </Button>
          </section>
        )}

        {current && cycle && <CurrentSessionCard session={current} daysPerWeek={cycle.daysPerWeek} warmUp={warmUp} />}

        {paired && (
          <section className="flex items-center gap-3 rounded-lg border border-border bg-card p-4" data-testid="paired-routine">
            <div className="min-w-0 flex-1">
              <p className="text-sm text-muted-foreground">After this workout</p>
              <p className="truncate font-semibold text-foreground">
                {paired.name} ({paired.targetMinutes} min)
              </p>
            </div>
            <Button asChild className="h-11" aria-label={`Play ${paired.name}`}>
              <Link to={`/routines/${paired.id}/play`}>
                <Play aria-hidden="true" />
                Play
              </Link>
            </Button>
          </section>
        )}

        <TodayPhaseHeadsUp date={date} />

        {split && <CycleList thisWeek={split.thisWeek} nextWeek={split.nextWeek} />}

        <TodayHabits date={date} />

        {cycle && (
          <Link to="/workout" className="inline-flex min-h-11 items-center text-sm font-medium text-primary underline-offset-4 hover:underline">
            Full plan and history
          </Link>
        )}
      </main>
    </div>
  );
}
