/**
 * Gym mode (`/gym/:day`, brief § 3a). Loads today's lift: the week plan's sets, the slot's warm-up
 * routine (or "Mobility A") and its finisher. Every tap is saved to localStorage first; Finish sends
 * one request that completes the day in the app and posts the workout to Hevy from the server, and
 * goes through the offline queue (keyed by the client session id) when there is no signal. Keeps the
 * screen awake and runs the rest timer after each confirmed set.
 */

import { useCardioTracks } from '@/hooks/useCardioTracks';
import type { CardioTrackDto } from '@/types/cardioTracks';
import { useCallback, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { useCurrentWorkout, useWeekPlan } from '@/hooks/useWorkouts';
import { useActivePlan, useToday } from '@/hooks/usePlan';
import { useMobilityRoutines } from '@/hooks/useMobilityRoutines';
import { useCardioProtocols, useFinishGymSession, useRetryHevyPost } from '@/hooks/useGymSession';
import { useLocalToday } from '@/hooks/useLocalToday';
import { useOnlineStatus } from '@/offline/useOnlineStatus';
import type { OfflineOutcome } from '@/offline/useOfflineMutation';
import { SessionKind } from '@/types/plan';
import type { CardioProtocolDto, FinishGymSessionResult } from '@/types/gym';
import type { WeekPlanDto } from '@/api/workouts';
import type { MobilityRoutineDto } from '@/types/mobility';
import type { WorkoutDto } from '@/features/workout/workoutSessionTypes';
import type { DayNumber } from '@/features/workout/workoutSessionTypes';
import { CompletionSummary } from '@/features/workout/CompletionSummary';
import { beep, primeAudio, useWakeLock } from '@/features/routines/playerEffects';
import { GymChecklist } from './GymChecklist';
import { GymHevyStatus } from './GymSections';
import {
  allDrillsDone,
  clearGymSession,
  createGymSession,
  editSet,
  loadGymSession,
  newClientSessionId,
  saveGymSession,
  tickDrill,
  toFinishRequest,
  toggleSet,
  updateFinisher,
  type GymSessionState,
} from './gymSession';
import { useRestTimer } from './useRestTimer';

const MOBILITY_A = 'mobility a';
const DAY_NAMES = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export function GymModePage() {
  const { day: dayParam } = useParams<{ day: string }>();
  const day = Number(dayParam ?? '1');
  const date = useLocalToday();

  const workoutQuery = useCurrentWorkout();
  const workout = workoutQuery.data ?? null;
  const weekPlan = useWeekPlan(workout?.id, workout?.currentWeek, day, Boolean(workout));
  const { data: plan } = useActivePlan();
  const todayQuery = useToday(date);
  const routines = useMobilityRoutines();

  const slot = useMemo(
    () => todayQuery.data?.sessions.find((s) => s.kind === SessionKind.Lift && s.liftDay === day) ?? null,
    [todayQuery.data, day]
  );
  const scheduled = plan?.schedule.find((s) => s.kind === SessionKind.Lift && s.liftDay === day) ?? null;
  const finisherId = slot?.finisherCardioProtocolId ?? scheduled?.finisherCardioProtocolId ?? null;
  const finisherTrackId = slot?.finisherCardioTrackId ?? scheduled?.finisherCardioTrackId ?? null;
  const tracks = useCardioTracks(Boolean(finisherTrackId));
  const warmUpId = slot?.warmUpRoutineId ?? scheduled?.warmUpRoutineId ?? null;
  const protocols = useCardioProtocols(Boolean(finisherId));

  const ready =
    weekPlan.data !== undefined &&
    !routines.isLoading &&
    !todayQuery.isLoading &&
    (!finisherId || protocols.isSuccess || protocols.isError) &&
    (!finisherTrackId || tracks.isSuccess || tracks.isError);

  if (workoutQuery.isLoading || (workout && !ready && !weekPlan.isError)) {
    return (
      <div className="min-h-dvh bg-background">
        <Navbar />
        <p className="p-4 text-muted-foreground">Loading today's session…</p>
      </div>
    );
  }

  if (!workout || !weekPlan.data) {
    return (
      <div className="min-h-dvh bg-background">
        <Navbar />
        <main className="mx-auto max-w-2xl px-4 py-8">
          <Card className="p-6 text-center">
            <h1 className="mb-2 text-xl font-semibold">Nothing to lift here</h1>
            <p className="mb-4 text-muted-foreground">There is no active workout with this day, or it could not be loaded.</p>
            <Button asChild className="h-12">
              <Link to="/today">Back to Today</Link>
            </Button>
          </Card>
        </main>
      </div>
    );
  }

  const routineList = routines.data ?? [];
  const warmUp = (warmUpId && routineList.find((r) => r.id === warmUpId)) || routineList.find((r) => r.name.toLowerCase() === MOBILITY_A) || null;
  const finisher = (finisherId && protocols.data?.find((p) => p.id === finisherId)) || null;
  const finisherTrack = (finisherTrackId && tracks.data?.find((t) => t.id === finisherTrackId)) || null;
  const label = slot?.label ?? scheduled?.label ?? `Day ${day}`;

  return (
    <GymSessionRunner
      key={`${workout.id}-${weekPlan.data.weekNumber}-${day}`}
      workout={workout}
      plan={weekPlan.data}
      day={day}
      title={`${plan?.name ?? workout.name} · W${weekPlan.data.weekNumber} ${label}`}
      label={label}
      warmUp={warmUp}
      finisher={finisher}
      finisherTrack={finisherTrack}
    />
  );
}

interface RunnerProps {
  workout: WorkoutDto;
  plan: WeekPlanDto;
  day: number;
  title: string;
  label: string;
  warmUp: MobilityRoutineDto | null;
  finisher: CardioProtocolDto | null;
  finisherTrack: CardioTrackDto | null;
}

/** Owns one session from its first render: resumes what this phone saved, or starts a new one. */
function GymSessionRunner({ workout, plan, day, title, label, warmUp, finisher, finisherTrack }: RunnerProps) {
  const navigate = useNavigate();
  const online = useOnlineStatus();
  const [state, setState] = useState<GymSessionState>(() => {
    const saved = loadGymSession(workout.id, plan.weekNumber, day);
    if (saved) return saved;
    const created = createGymSession({ clientSessionId: newClientSessionId(), startedAt: new Date().toISOString(), title, plan, warmUp, finisher, finisherTrack });
    saveGymSession(created);
    return created;
  });
  const [outcome, setOutcome] = useState<OfflineOutcome<FinishGymSessionResult> | null>(null);
  const finish = useFinishGymSession(label);
  const retry = useRetryHevyPost();
  const rest = useRestTimer(() => {
    beep('finish');
    navigator.vibrate?.(200);
  });
  useWakeLock(outcome === null);

  const update = useCallback((next: (s: GymSessionState) => GymSessionState) => {
    setState((current) => {
      const updated = next(current);
      saveGymSession(updated);
      return updated;
    });
  }, []);

  const onToggleSet = (lift: number, set: number, reps?: number) => {
    primeAudio();
    const wasDone = state.lifts[lift]?.sets[set]?.done ?? false;
    update((s) => toggleSet(s, lift, set, reps));
    if (!wasDone || reps !== undefined) rest.start(state.lifts[lift]?.restSeconds ?? 90);
  };

  const onFinish = () => {
    rest.skip();
    finish.mutate(toFinishRequest(state, new Date().toISOString()), {
      onSuccess: (result) => {
        clearGymSession(state.workoutId, state.week, state.day);
        setOutcome(result);
      },
      onError: (error) => toast.error(error instanceof Error ? error.message : 'Could not finish the session'),
    });
  };

  if (outcome) {
    if (outcome.queued || !outcome.data.completeDay) {
      return (
        <div className="min-h-dvh bg-background">
          <Navbar />
          <main className="mx-auto max-w-2xl px-4 py-8">
            <h1 className="mb-4 text-2xl font-semibold text-foreground">Session saved</h1>
            <GymHevyStatus status={outcome.queued ? null : outcome.data.hevyStatus} queued={outcome.queued} />
            <Button className="h-12 w-full" onClick={() => navigate('/today')}>
              Back to Today
            </Button>
          </main>
        </div>
      );
    }
    const result = outcome.data;
    return (
      <CompletionSummary
        result={result.completeDay!}
        workout={workout}
        dayNumber={day as DayNumber}
        dayName={DAY_NAMES[day - 1] ?? `Day ${day}`}
        exerciseEntries={[]}
        onContinue={() => navigate('/today')}
        hevyNotice={
          <GymHevyStatus
            status={result.hevyStatus}
            onRetry={() => retry.mutate(result.clientSessionId, { onSuccess: (s) => setOutcome({ queued: false, data: { ...result, hevyStatus: s.status } }) })}
            retrying={retry.isPending}
          />
        }
      />
    );
  }

  return (
    <div className="min-h-dvh bg-background">
      <Navbar />
      <GymChecklist
        state={state}
        rest={{ remaining: rest.remaining, total: rest.total, running: rest.running, onAdjust: rest.adjust, onSkip: rest.skip }}
        online={online}
        finishing={finish.isPending}
        onTickDrill={(i) => update((s) => tickDrill(s, i))}
        onAllDrillsDone={() => update(allDrillsDone)}
        onToggleSet={onToggleSet}
        onEditSet={(l, s, change) => update((st) => editSet(st, l, s, change))}
        onFinisherChange={(change) => update((s) => updateFinisher(s, change))}
        onFinish={onFinish}
      />
    </div>
  );
}
