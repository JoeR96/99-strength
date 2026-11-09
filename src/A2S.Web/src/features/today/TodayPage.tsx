/**
 * Today: what do I do now? Today's session with one big Start, the habit
 * counters as 64 px `+` buttons, the weigh-in, the week's rings and the streak.
 * Logging a bottle of water is one tap from app open.
 *
 * With a Hevy key stored, opening the page (or pulling down, pressing Refresh, or coming back
 * from Hevy) pulls completed workouts in the background; the page never waits for it.
 */

import { useMemo, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { formatDayLabel } from '@/lib/localDate';
import { useLocalToday } from '@/hooks/useLocalToday';
import { useActivePlan, useCompleteSession, useToday, useUndoSessionCompletion } from '@/hooks/usePlan';
import { hevyErrorMessage, useAutoPullHevy, useHevyInbox, usePushSession } from '@/hooks/useHevySync';
import { usePullToRefresh } from '@/hooks/usePullToRefresh';
import { useHevyStatus } from '@/contexts/hevyStatusContext';
import { useOnlineStatus } from '@/offline/useOnlineStatus';
import { useHabitsToday, useIncrementHabit, useSetHabitValue } from '@/hooks/useHabits';
import { useRecordWeight, useWeightTrend } from '@/hooks/useBodyWeight';
import { PhaseType, SessionKind, type CardioLogDto, type TodaySessionDto } from '@/types/plan';
import type { HabitTodayDto } from '@/types/habits';
import { SessionCard } from './SessionCard';
import { HabitCounter } from './HabitCounter';
import { HabitTick } from './HabitTick';
import { WeighInField } from './WeighInField';
import { WeeklyRings } from './WeeklyRings';
import { StreakChip } from './StreakChip';
import { PhaseBanner } from './PhaseBanner';
import { currentPhase } from './phase';
import { CardioLogSheet } from './CardioLogSheet';
import { MobilityChecklist } from './MobilityChecklist';
import { PendingSyncBadge } from './PendingSyncBadge';
import { usePendingSyncCount } from '@/offline/context';
import { StartSuggestedPlanCard } from './StartSuggestedPlanCard';
import { HevySyncNotice } from './HevySyncNotice';
import { openHevyRoutine } from './hevyDeepLink';

export interface TodayPageProps {
  /** Overrides the offline queue length shown in the header (stories); otherwise read from the queue. */
  pendingSyncCount?: number;
}

function isWeighInHabit(habit: HabitTodayDto): boolean {
  return habit.type === 'Numeric' && (habit.unit ?? '').toLowerCase() === 'kg';
}

function Skeleton({ className }: { className: string }) {
  return <div className={`animate-pulse rounded-lg bg-muted ${className}`} aria-hidden="true" />;
}

export function TodayPage({ pendingSyncCount }: TodayPageProps) {
  const date = useLocalToday();
  const queuedCount = usePendingSyncCount();
  const pendingCount = pendingSyncCount ?? queuedCount;
  const todayQuery = useToday(date);
  const planQuery = useActivePlan();
  const habitsQuery = useHabitsToday(date);
  const trendQuery = useWeightTrend(date, date);
  const increment = useIncrementHabit();
  const setValue = useSetHabitValue();
  const recordWeight = useRecordWeight();
  const completeSession = useCompleteSession();
  const undoCompletion = useUndoSessionCompletion();

  const { isConfigured: hevyConfigured } = useHevyStatus();
  const online = useOnlineStatus();
  const pull = useAutoPullHevy(hevyConfigured && online);
  const inboxQuery = useHevyInbox(hevyConfigured);
  const pushSession = usePushSession();

  const [cardioSession, setCardioSession] = useState<TodaySessionDto | null>(null);
  const [mobilitySession, setMobilitySession] = useState<TodaySessionDto | null>(null);

  const today = todayQuery.data;
  const plan = planQuery.data;
  const habits = habitsQuery.data;
  const paused = today?.phase === PhaseType.Pause;
  const phaseDetail = useMemo(() => currentPhase(plan?.phases, date), [plan?.phases, date]);

  const weighInHabit = habits?.habits.find(isWeighInHabit) ?? null;
  const counters = habits?.habits.filter((h) => h.type === 'Counter') ?? [];
  const ticks = habits?.habits.filter((h) => h.type === 'Checkbox') ?? [];
  const todayPoint = trendQuery.data?.points.find((p) => p.date === date);
  const weighInKg = todayPoint?.kg ?? (weighInHabit && weighInHabit.value > 0 ? weighInHabit.value : null);

  const saveWeight = (kg: number) => {
    recordWeight.mutate(
      { date, kg },
      {
        onSuccess: () => {
          if (weighInHabit) setValue.set(weighInHabit.id, kg, date);
          toast.success(`Logged ${kg.toFixed(1)} kg`);
        },
        onError: () => toast.error("Couldn't save your weigh-in. Try again."),
      }
    );
  };

  const openMarkDone = (session: TodaySessionDto) => {
    if (session.kind === SessionKind.Cardio) setCardioSession(session);
    if (session.kind === SessionKind.Mobility) setMobilitySession(session);
  };

  const complete = (session: TodaySessionDto, extra: { cardio?: CardioLogDto; drillTicks?: boolean[] | null }) => {
    if (!plan) return;
    completeSession.mutate(
      {
        planId: plan.id,
        request: {
          date,
          kind: session.kind,
          liftDay: session.liftDay,
          cardioProtocolId: session.cardioProtocolId,
          mobilityRoutineId: session.mobilityRoutineId,
          cardio: extra.cardio ?? null,
          drillTicks: extra.drillTicks ?? null,
        },
        label: session.label,
      },
      {
        onSuccess: (outcome) => {
          setCardioSession(null);
          setMobilitySession(null);
          toast.success(outcome.queued ? `${session.label} done — will sync when you're back online` : `${session.label} done`);
        },
        onError: () => toast.error("Couldn't mark that done. Try again."),
      }
    );
  };

  const undo = (session: TodaySessionDto) => {
    if (!plan || !session.completionId) return;
    undoCompletion.mutate(
      { planId: plan.id, completionId: session.completionId },
      {
        onSuccess: () => toast.success(`${session.label} undone`),
        onError: () => toast.error("Couldn't undo that. Try again."),
      }
    );
  };

  /** Refresh button and pull-to-refresh: pull from Hevy (which re-reads the plan), or just re-read the plan. */
  const refresh = () => {
    if (pull.isPending) return;
    if (!online) {
      toast("You're offline. Showing what's saved on this device.");
      return;
    }
    if (hevyConfigured) {
      pull.mutate({ manual: true, announce: 'always' });
    } else {
      void todayQuery.refetch();
      void planQuery.refetch();
    }
  };
  const pullGesture = usePullToRefresh(refresh);
  const refreshing = pull.isPending || (todayQuery.isRefetching && !todayQuery.isLoading);

  const pushToHevy = (session: TodaySessionDto) => {
    if (!plan) return;
    if (!online) {
      toast.error("You're offline. Push to Hevy when you're back online.");
      return;
    }
    pushSession.mutate(
      { planId: plan.id, request: { date, kind: session.kind, liftDay: session.liftDay } },
      {
        onSuccess: () => toast.success(`${session.label} is in Hevy`),
        onError: (error) => toast.error(hevyErrorMessage(error, `Couldn't push ${session.label} to Hevy. Try again.`)),
      }
    );
  };
  const isPushing = (session: TodaySessionDto) =>
    pushSession.isPending &&
    pushSession.variables?.request.kind === session.kind &&
    (pushSession.variables.request.liftDay ?? null) === session.liftDay;

  const openInHevy = (session: TodaySessionDto) => {
    if (session.hevyRoutineId) openHevyRoutine(session.hevyRoutineId);
  };

  const planLoading = todayQuery.isLoading || planQuery.isLoading;
  const planError = todayQuery.isError || planQuery.isError;

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
          <div>
            <p className="text-sm text-muted-foreground">Today</p>
            <h1 className="text-2xl font-bold text-foreground">{formatDayLabel(date)}</h1>
          </div>
          <div className="flex flex-wrap items-center justify-end gap-2">
            <PendingSyncBadge count={pendingCount} />
            {habits && <StreakChip streak={habits.streak} />}
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

        {today?.phase !== undefined && today?.phase !== PhaseType.Cut && (
          <PhaseBanner phase={today?.phase ?? null} detail={phaseDetail} />
        )}

        {planLoading && <Skeleton className="h-36" />}

        {planError && (
          <section className="rounded-lg border border-destructive/50 bg-card p-4" role="alert">
            <p className="text-sm text-foreground">Couldn't load today's plan.</p>
            <Button
              variant="outline"
              className="mt-3 h-11"
              onClick={() => {
                void todayQuery.refetch();
                void planQuery.refetch();
              }}
            >
              <RefreshCw aria-hidden="true" />
              Try again
            </Button>
          </section>
        )}

        {!planLoading && !planError && today === null && (
          <StartSuggestedPlanCard
            onStarted={(result) => {
              toast.success(result.alreadyStarted ? 'Your plan is already running' : 'Plan started');
              void todayQuery.refetch();
              void planQuery.refetch();
            }}
          />
        )}

        {today && (
          <section className="space-y-3" aria-label="Today's sessions">
            {today.sessions.map((session, index) => (
              <SessionCard
                key={`${session.kind}-${session.label}-${index}`}
                session={session}
                paused={paused}
                onMarkDone={openMarkDone}
                onUndo={undo}
                hevyConfigured={hevyConfigured}
                onOpenInHevy={openInHevy}
                onPushToHevy={pushToHevy}
                pushing={isPushing(session)}
                busy={completeSession.isPending || undoCompletion.isPending}
              />
            ))}
          </section>
        )}

        <section aria-labelledby="habits-heading" className="space-y-2">
          <h2 id="habits-heading" className="text-sm font-medium text-muted-foreground">
            Habits
          </h2>
          {habitsQuery.isLoading && (
            <>
              <Skeleton className="h-24" />
              <Skeleton className="h-24" />
            </>
          )}
          {habitsQuery.isError && (
            <div className="rounded-lg border border-destructive/50 bg-card p-4" role="alert">
              <p className="text-sm text-foreground">Couldn't load habits.</p>
              <Button variant="outline" className="mt-3 h-11" onClick={() => void habitsQuery.refetch()}>
                <RefreshCw aria-hidden="true" />
                Try again
              </Button>
            </div>
          )}
          {habits && habits.habits.length === 0 && (
            <p className="rounded-lg border border-dashed border-border p-4 text-sm text-muted-foreground">
              No habits yet. Add water, walks and the rest on the Habits tab.
            </p>
          )}
          {counters.map((habit) => (
            <HabitCounter key={habit.id} habit={habit} onTap={increment.tap} />
          ))}
          {ticks.length > 0 && (
            <div className="grid grid-cols-2 gap-2">
              {ticks.map((habit) => (
                <HabitTick key={habit.id} habit={habit} onToggle={(id, done) => setValue.set(id, done ? 1 : 0, date)} />
              ))}
            </div>
          )}
          {!habitsQuery.isLoading && !habitsQuery.isError && (
            <WeighInField valueKg={weighInKg} onSave={saveWeight} saving={recordWeight.isPending} />
          )}
        </section>

        {today && <WeeklyRings week={today.week} />}
      </main>

      <CardioLogSheet
        session={cardioSession}
        onClose={() => setCardioSession(null)}
        onSubmit={(session, cardio) => complete(session, { cardio })}
        saving={completeSession.isPending}
      />
      <MobilityChecklist
        session={mobilitySession}
        onClose={() => setMobilitySession(null)}
        onSubmit={(session, drillTicks) => complete(session, { drillTicks })}
        saving={completeSession.isPending}
      />
    </div>
  );
}
