/**
 * Gym mode (`/gym/:day`, brief § 3a). Loads today's lift: the week plan's sets, the slot's warm-up
 * routine (or "Mobility A") and its finisher. Every tap is saved to localStorage first; Finish sends
 * one request that completes the day in the app and posts the workout to Hevy from the server, and
 * goes through the offline queue (keyed by the client session id) when there is no signal. Keeps the
 * screen awake and runs the rest timer after each confirmed set.
 *
 * `?preview=1&date=yyyy-MM-dd` renders `GymPreview` instead: read-only, and it never mounts the runner,
 * so no draft is saved, no wake lock is held and no rest timer or offline op exists.
 */

import { useCardioTracks } from '@/hooks/useCardioTracks';
import { useReadiness, useSetReadinessFinisher } from '@/hooks/useReadiness';
import type { CardioTrackDto } from '@/types/cardioTracks';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
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
import { statusOf } from '@/offline/errors';
import type { OfflineOutcome } from '@/offline/useOfflineMutation';
import { SessionKind, type FixedFinisherDto } from '@/types/plan';
import type { CardioProtocolDto, FinishGymSessionResult } from '@/types/gym';
import type { WeekPlanDto } from '@/api/workouts';
import type { MobilityRoutineDto } from '@/types/mobility';
import type { WorkoutDto } from '@/features/workout/workoutSessionTypes';
import type { DayNumber } from '@/features/workout/workoutSessionTypes';
import { CompletionSummary } from '@/features/workout/CompletionSummary';
import { beep, primeAudio, useWakeLock } from '@/features/routines/playerEffects';
import { GymChecklist } from './GymChecklist';
import { GymPreview } from './GymPreview';
import { dayHeading, parseDateParam } from '@/features/today/upNext';
import { GymHevyStatus } from './GymSections';
import { FinishedElsewhereNotice } from './FinishedElsewhereNotice';
import {
  allDrillsDone,
  clearGymSession,
  createGymSession,
  editSet,
  expireGymDrafts,
  loadGymSession,
  newClientSessionId,
  saveGymSession,
  startClockOnFirstTick,
  tickDrill,
  toFinishRequest,
  toggleSet,
  updateFinisher,
  restAfterSet,
  syncPendingLifts,
  toggleExtraSet,
  toggleFixedFinisher,
  toggleSide,
  type GymSessionState,
} from './gymSession';
import { SetTrainingMaxDialog } from './SetTrainingMaxDialog';
import { useRestTimer } from './useRestTimer';
import { FinisherWizard } from './finisher/FinisherWizard';
import { cardioLogFromSummary, finisherLogFromSummary, wizardTarget } from './finisher/gymFinisherPlan';
import { chooseCardioEffort, chooseCardioSettings, keepHardCardio, setHardCardioSkipped, syncCardioBlocks, tickCardio, updateCardio } from './gymCardio';
import { clearEngine, loadEngine, saveEngine, scheduleOf, startEngine, type FinisherSummary, type ScheduledNudge } from './finisher/finisherEngine';
import { finisherSessionsApi } from '@/api/finisherSessions';

const MOBILITY_A = 'mobility a';
const DAY_NAMES = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export function GymModePage() {
  const { day: dayParam } = useParams<{ day: string }>();
  const day = Number(dayParam ?? '1');
  const localToday = useLocalToday();
  const [searchParams] = useSearchParams();
  const preview = searchParams.get('preview') === '1';
  const date = (preview && parseDateParam(searchParams.get('date'))) || localToday;
  // `?preview=1&week=N`: a cycle position (Current workout page); `?date=` stays for back-compat.
  const weekParam = Number(searchParams.get('week'));
  const previewWeek = preview && Number.isInteger(weekParam) && weekParam >= 1 ? weekParam : null;

  const workoutQuery = useCurrentWorkout();
  const workout = workoutQuery.data ?? null;
  const weekPlan = useWeekPlan(workout?.id, previewWeek ?? workout?.currentWeek, day, Boolean(workout));
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
  const finisherFixed = slot?.finisherFixed ?? scheduled?.finisherFixed ?? null;
  // Known once today or the plan has loaded: a draft from an older app may still carry a finisher the slot lost.
  const slotHasFinisher = todayQuery.data || plan ? Boolean(finisherId || finisherTrackId || finisherFixed) : undefined;
  const tracks = useCardioTracks(Boolean(finisherTrackId));
  const warmUpId = slot?.warmUpRoutineId ?? scheduled?.warmUpRoutineId ?? null;
  const protocols = useCardioProtocols(Boolean(finisherId));
  // Red readiness skips the finisher and the stair climber (hard-lane cardio); "Do it anyway" keeps them.
  const hasHardCardio = weekPlan.data?.exercises.some((e) => e.cardioBlock?.lane === 'Hard') ?? false;
  const readiness = useReadiness(date, Boolean(finisherId || finisherTrackId || hasHardCardio));
  const skipFinisher = readiness.data?.finisherSkipped === true;
  const keepFinisher = useSetReadinessFinisher();

  const ready =
    weekPlan.data !== undefined &&
    !routines.isLoading &&
    !todayQuery.isLoading &&
    !(readiness.isLoading && readiness.fetchStatus !== 'idle') &&
    (!finisherId || protocols.isSuccess || protocols.isError) &&
    (!finisherTrackId || tracks.isSuccess || tracks.isError);

  if (workoutQuery.isLoading || (workout && !ready && !weekPlan.isError)) {
    return (
      <div className="min-h-dvh bg-background">
        <Navbar />
        <p className="p-4 text-muted-foreground">{preview ? 'Loading the preview…' : "Loading today's session…"}</p>
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
              <Link to="/today">Back to workout</Link>
            </Button>
          </Card>
        </main>
      </div>
    );
  }

  const routineList = routines.data ?? [];
  const warmUp = (warmUpId && routineList.find((r) => r.id === warmUpId)) || routineList.find((r) => r.name.toLowerCase() === MOBILITY_A) || null;
  const finisher = (!skipFinisher && finisherId && protocols.data?.find((p) => p.id === finisherId)) || null;
  const finisherTrack = (!skipFinisher && finisherTrackId && tracks.data?.find((t) => t.id === finisherTrackId)) || null;
  const label = slot?.label ?? scheduled?.label ?? weekPlan.data.sessionLabel ?? `Day ${day}`;
  const title = `${plan?.name ?? workout.name} · W${weekPlan.data.weekNumber} ${label}`;

  if (preview) {
    const state = createGymSession({ clientSessionId: 'preview', startedAt: '', title, plan: weekPlan.data, warmUp, finisher, finisherTrack, finisherFixed });
    return (
      <div className="min-h-dvh bg-background">
        <Navbar />
        <GymPreview
          state={state}
          dayLabel={previewWeek ? `Week ${previewWeek} · Session ${day} of ${weekPlanDays(workout)}` : dayHeading(date, localToday)}
          startHref={
            previewWeek
              ? weekPlan.data.isCurrent ? `/gym/${day}` : null
              : date === localToday ? `/gym/${day}` : null
          }
          cardio={weekPlan.data.cardio ?? null}
        />
      </div>
    );
  }

  return (
    <GymSessionRunner
      key={`${workout.id}-${weekPlan.data.weekNumber}-${day}`}
      workout={workout}
      plan={weekPlan.data}
      day={day}
      title={title}
      label={label}
      warmUp={warmUp}
      finisher={finisher}
      finisherTrack={finisherTrack}
      finisherFixed={finisherFixed}
      slotHasFinisher={slotHasFinisher}
      skipHardCardio={skipFinisher}
      onKeepHardCardio={() =>
        keepFinisher.mutate({ date, keep: true }, { onError: () => toast.error('Kept on this phone. Could not tell the server; your readiness still says skip.') })
      }
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
  finisherFixed: FixedFinisherDto | null;
  /** Whether the plan slot still has a finisher (undefined: not known yet). */
  slotHasFinisher?: boolean;
  /** Red readiness: the stair climber is skipped for recovery unless kept. */
  skipHardCardio: boolean;
  onKeepHardCardio: () => void;
}

/** Owns one session from its first render: resumes what this phone saved, or starts a new one. */
function GymSessionRunner({ workout, plan, day, title, label, warmUp, finisher, finisherTrack, finisherFixed, slotHasFinisher, skipHardCardio, onKeepHardCardio }: RunnerProps) {
  const navigate = useNavigate();
  const online = useOnlineStatus();
  const [state, setState] = useState<GymSessionState>(() => {
    // Drafts from earlier weeks of this workout, or more than a week old, are never resumed.
    expireGymDrafts(new Date(), { workoutId: workout.id, currentWeek: workout.currentWeek });
    const saved = loadGymSession(workout.id, plan.weekNumber, day);
    if (saved) return saved;
    const created = createGymSession({ clientSessionId: newClientSessionId(), startedAt: new Date().toISOString(), title, plan, warmUp, finisher, finisherTrack, finisherFixed });
    saveGymSession(created);
    return created;
  });
  const [outcome, setOutcome] = useState<OfflineOutcome<FinishGymSessionResult> | null>(null);
  const [finishedElsewhere, setFinishedElsewhere] = useState<string | null>(null);
  const [tmLift, setTmLift] = useState<number | null>(null);
  const finish = useFinishGymSession(label);
  const retry = useRetryHevyPost();
  const rest = useRestTimer(() => {
    beep('finish');
    navigator.vibrate?.(200);
  });
  useWakeLock(outcome === null);

  // The live wizard: the stair finisher, or the stair climber cardio exercise. `?finisher=1` (the phase
  // notification's deep link) or a wizard this phone left running reopens it.
  const [searchParams, setSearchParams] = useSearchParams();
  const wizard = useMemo(() => wizardTarget({ finisher: state.finisher, cardio: state.cardio }), [state.finisher, state.cardio]);
  const finisherPlan = useMemo(() => wizard?.phases ?? [], [wizard]);
  const [wizardOpen, setWizardOpen] = useState(
    () => wizardTarget(state) !== null && (searchParams.get('finisher') === '1' || loadEngine(state.clientSessionId) !== null)
  );
  // The target going away closes the wizard too, so it does not pop back open if the target returns ("Do it anyway").
  const [hadWizard, setHadWizard] = useState(wizard !== null);
  if ((wizard !== null) !== hadWizard) {
    setHadWizard(wizard !== null);
    if (wizard === null) setWizardOpen(false);
  }
  const wizardName = wizard?.name ?? null;
  const postSchedule = useCallback(
    (nudges: ScheduledNudge[]) => {
      void finisherSessionsApi.replace(state.clientSessionId, { day: state.day, title: wizardName, nudges }).catch(() => undefined);
    },
    [state.clientSessionId, state.day, wizardName]
  );
  const closeWizard = () => {
    clearEngine(state.clientSessionId);
    void finisherSessionsApi.remove(state.clientSessionId).catch(() => undefined);
    setWizardOpen(false);
    if (searchParams.has('finisher')) {
      const next = new URLSearchParams(searchParams);
      next.delete('finisher');
      setSearchParams(next, { replace: true });
    }
  };
  const startFinisher = () => {
    primeAudio();
    if (!loadEngine(state.clientSessionId)) {
      const now = Date.now();
      const engine = startEngine(state.clientSessionId, finisherPlan, now);
      saveEngine(engine);
      postSchedule(scheduleOf(engine, now));
    }
    setWizardOpen(true);
  };
  const onFinisherDone = (summary: FinisherSummary) => {
    const cardioIndex = wizard?.cardioIndex ?? null;
    update((s) =>
      cardioIndex !== null && s.cardio[cardioIndex]
        ? updateCardio(s, cardioIndex, cardioLogFromSummary(s.cardio[cardioIndex], summary))
        : s.finisher
          ? updateFinisher(s, finisherLogFromSummary(s.finisher, summary))
          : s
    );
    closeWizard();
  };

  const update = useCallback((next: (s: GymSessionState) => GymSessionState) => {
    setState((current) => {
      const updated = startClockOnFirstTick(current, next(current), new Date().toISOString());
      saveGymSession(updated);
      return updated;
    });
  }, []);

  // A training max or cardio settings saved mid-session bring the week plan back: fill them in. A draft started on an
  // older app gets the plan's cardio exercises and loses a finisher its slot no longer has.
  useEffect(() => {
    update((s) => syncCardioBlocks(syncPendingLifts(s, plan), plan, { slotHasFinisher }));
  }, [plan, slotHasFinisher, update]);

  // The wizard's target went away while its clock was running (the stair climber skipped on a red day, a finisher
  // dropped): stop its phase-change pushes and forget the clock, as closing the wizard does.
  useEffect(() => {
    if (wizard || !loadEngine(state.clientSessionId)) return;
    clearEngine(state.clientSessionId);
    void finisherSessionsApi.remove(state.clientSessionId).catch(() => undefined);
  }, [wizard, state.clientSessionId]);

  useEffect(() => {
    update((s) => setHardCardioSkipped(s, skipHardCardio));
  }, [skipHardCardio, update]);

  const onToggleSet = (lift: number, set: number, reps?: number) => {
    primeAudio();
    const wasDone = state.lifts[lift]?.sets[set]?.done ?? false;
    update((s) => toggleSet(s, lift, set, reps));
    // Superset: no rest between A and B; after B the superset rest.
    if (!wasDone || reps !== undefined) rest.start(restAfterSet(state, lift, set));
  };

  const onFinish = () => {
    rest.skip();
    finish.mutate(toFinishRequest(state, new Date().toISOString()), {
      onSuccess: (result) => {
        clearGymSession(state.workoutId, state.week, state.day);
        setOutcome(result);
      },
      onError: (error) => {
        if (statusOf(error) === 409) {
          setFinishedElsewhere(conflictMessage(error));
          return;
        }
        toast.error(error instanceof Error ? error.message : 'Could not finish the session');
      },
    });
  };

  const discardDraft = () => {
    clearGymSession(state.workoutId, state.week, state.day);
    navigate('/today');
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
              Back to workout
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
        finisherOutcome={result.finisherOutcome ?? null}
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
      {finishedElsewhere && (
        <FinishedElsewhereNotice message={finishedElsewhere} onDiscard={discardDraft} onKeep={() => setFinishedElsewhere(null)} />
      )}
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
        onToggleExtraSet={(l, change) => update((s) => toggleExtraSet(s, l, change))}
        onToggleFixedFinisher={() => update(toggleFixedFinisher)}
        onStartFinisher={wizard && wizard.cardioIndex === null ? startFinisher : undefined}
        finisherStartLabel={loadEngine(state.clientSessionId) ? 'Resume finisher' : 'Start finisher'}
        onCardioChange={(i, change) => update((s) => updateCardio(s, i, change))}
        onCardioEffort={(i, effort) => update((s) => chooseCardioEffort(s, i, effort))}
        onTickCardio={(i) => update((s) => tickCardio(s, i))}
        onCardioSettingsChosen={(i, values) => update((s) => chooseCardioSettings(s, i, values))}
        onStartCardio={wizard && wizard.cardioIndex !== null ? startFinisher : undefined}
        cardioStartLabel={loadEngine(state.clientSessionId) ? 'Resume' : 'Start'}
        onKeepHardCardio={() => {
          update(keepHardCardio);
          onKeepHardCardio();
        }}
        onSetTrainingMax={setTmLift}
        onToggleLeft={(l, set) => update((s) => toggleSide(s, l, set, 'Left'))}
      />
      {wizardOpen && wizard && (
        <FinisherWizard
          name={wizard.name}
          clientSessionId={state.clientSessionId}
          phases={finisherPlan}
          onSchedule={postSchedule}
          onDone={onFinisherDone}
          onAbandon={closeWizard}
        />
      )}
      {tmLift !== null && state.lifts[tmLift] && (
        <SetTrainingMaxDialog
          open
          workoutId={workout.id}
          exerciseId={state.lifts[tmLift].exerciseId}
          exerciseName={state.lifts[tmLift].name}
          onClose={() => setTmLift(null)}
          onSaved={(result) => toast.success(`Training max set: ${result.trainingMax} kg`)}
        />
      )}
    </div>
  );
}

function weekPlanDays(workout: WorkoutDto): number {
  return Number(workout.variant) || 4;
}

const FINISHED_ELSEWHERE = 'This day was already finished on another device';

/** The server's 409 detail ("This day was already finished on another device"), or that text. */
function conflictMessage(error: unknown): string {
  const data = (error as { response?: { data?: { detail?: unknown } } })?.response?.data;
  return typeof data?.detail === 'string' && data.detail.trim() !== '' ? data.detail : FINISHED_ELSEWHERE;
}
