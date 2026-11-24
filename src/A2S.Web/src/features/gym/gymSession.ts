/**
 * Gym mode's checklist state and the pure steps that change it (brief § 3a). One session, in order:
 * warm-up drills, then each lift with its planned sets pre-filled, then the cardio exercises (`gymCardio.ts`)
 * and the attached finisher. Every
 * step returns a new state, which the page saves to localStorage straight away, so nothing logged is
 * lost to a reload or a dead battery. `toFinishRequest` turns it into the server's finish body: only
 * sets that were confirmed are sent, and a lift with none is left out (its progression waits).
 */

import { VolumeUnit, type CardioTrackDto, type TrackSegmentDto } from '@/types/cardioTracks';
import type { FinisherIntervals } from './finisher/finisherPlan';
import { stepUpText } from '@/features/cardio/trackText';
import type { WeekPlanDto } from '@/api/workouts';
import type { MobilityRoutineDto } from '@/types/mobility';
import { DoseUnit } from '@/types/mobility';
import { WeightUnit } from '@/types/workout';
import type { CardioProtocolDto, Effort, FinishGymSessionRequest } from '@/types/gym';
import type { FixedFinisherDto } from '@/types/plan';
import { cardioFromPlan, cardioPerformances, isCardioDone, type GymCardio } from './gymCardio';

export interface GymSet {
  setNumber: number;
  plannedWeight: number;
  plannedReps: number;
  weight: number;
  reps: number;
  isAmrap: boolean;
  done: boolean;
  /** Unilateral lifts: the left side (always done first) is ticked; `done` is set by the right side. */
  leftDone?: boolean;
}

/** The bonus set on one side ("+1 left"): sent as `extraSet`, never part of completedSets. */
export interface GymExtraSet {
  side: 'Left' | 'Right';
  weight: number;
  reps: number;
  done: boolean;
}

export interface GymLift {
  exerciseId: string;
  name: string;
  restSeconds: number;
  unit: 'kg' | 'lbs';
  sets: GymSet[];
  /** Same number = one superset block; the lift with the lower order is A. */
  supersetGroup?: number | null;
  extraSet?: GymExtraSet | null;
  /** An A2S2 lift without a training max: sets show reps and intensity, no kg, until it is set. */
  trainingMaxPending?: boolean;
  /** Linear only, e.g. 0.7. */
  intensity?: number | null;
  /** One side at a time: each set is listed Left then Right, left first. */
  unilateral?: boolean;
}

export interface GymDrill {
  name: string;
  dose: string;
  done: boolean;
}

export interface GymFinisher {
  protocolId: string;
  /** A fixed cool-down (treadmill, easy incline, 10 min): one tick, logged as prescribed. */
  fixed?: FixedFinisherDto | null;
  /** The fixed cool-down was ticked. */
  done?: boolean;
  /** Set when the finisher is a cardio track: its prescription and "N more sessions to step up" are shown. */
  trackId?: string | null;
  stepUp?: string | null;
  name: string;
  prescription: string;
  minutes: number | null;
  level: number | null;
  floors: number | null;
  effort: Effort | null;
  pain?: 'None' | 'Niggle' | 'Stop';
  /** A track with a warm-up or cool-down: `minutes` is the whole session, this the work block (prefilled). */
  workMinutes?: number | null;
  warmUp?: TrackSegmentDto | null;
  coolDown?: TrackSegmentDto | null;
  /** A 1:1 interval stage: rounds of hard / easy (the live wizard expands them). */
  intervals?: FinisherIntervals | null;
}

/** Bump when GymSessionState changes shape, and teach `migrateGymSession` to upgrade the old one. */
export const GYM_SESSION_VERSION = 2;

export interface GymSessionState {
  /** Saved-shape version; a draft saved before versioning (no field) is v1. */
  version: typeof GYM_SESSION_VERSION;
  clientSessionId: string;
  workoutId: string;
  day: number;
  week: number;
  title: string;
  startedAt: string;
  drills: GymDrill[];
  lifts: GymLift[];
  /** v2: the day's cardio exercises, after the lifts. */
  cardio: GymCardio[];
  finisher: GymFinisher | null;
}

export interface CreateGymSessionInput {
  clientSessionId: string;
  startedAt: string;
  title: string;
  plan: WeekPlanDto;
  warmUp: MobilityRoutineDto | null;
  finisher: CardioProtocolDto | null;
  /** A track finisher (takes precedence over a protocol). */
  finisherTrack?: CardioTrackDto | null;
  /** A fixed cool-down (takes precedence over both). */
  finisherFixed?: FixedFinisherDto | null;
}

/** "Treadmill · easy incline · 10 min". */
/** A track prescription's interval block (work/rest seconds and rounds), or null when it is continuous. */
export function trackIntervals(track: CardioTrackDto): FinisherIntervals | null {
  const p = track.prescription;
  if (!p.workSeconds || p.workSeconds <= 0) return null;
  const rest = p.restSeconds ?? 0;
  const rounds = p.volumeUnit === VolumeUnit.Rounds && p.volume ? p.volume : Math.floor(((p.workMinutes || 0) * 60) / (p.workSeconds + rest));
  return rounds > 0 ? { rounds, workSeconds: p.workSeconds, restSeconds: rest } : null;
}

export function fixedFinisherText(fixed: FixedFinisherDto): string {
  const notes = fixed.notes?.trim();
  return [fixed.kind, notes ? notes.charAt(0).toLowerCase() + notes.slice(1) : null, `${fixed.minutes} min`].filter(Boolean).join(' · ');
}

function fixedFinisher(fixed: FixedFinisherDto): GymFinisher {
  return { protocolId: '', fixed, done: false, name: fixed.kind, prescription: fixedFinisherText(fixed), minutes: fixed.minutes, level: null, floors: null, effort: 'Easy' };
}

type PlannedExercise = WeekPlanDto['exercises'][number];

function liftFromPlan(e: PlannedExercise): GymLift {
  const unit = e.plannedSets[0]?.originalUnit === 'Pounds' ? 'lbs' : 'kg';
  const sets = e.plannedSets.map((s) => {
    const weight = unit === 'lbs' ? s.weightLbs : s.weightKg;
    return { setNumber: s.setNumber, plannedWeight: weight, plannedReps: s.targetReps, weight, reps: s.targetReps, isAmrap: s.isAmrap, done: false };
  });
  const last = sets[sets.length - 1];
  const side = e.extraSetSide === 'Left' || e.extraSetSide === 'Right' ? e.extraSetSide : null;
  return {
    exerciseId: e.exerciseId,
    name: e.name,
    restSeconds: restSecondsFor(e.category, e.restSeconds),
    unit,
    sets,
    supersetGroup: e.supersetGroup ?? null,
    extraSet: side && last ? { side, weight: last.weight, reps: last.reps, done: false } : null,
    trainingMaxPending: e.metadata?.isTrainingMaxPending ?? false,
    intensity: e.metadata?.intensityPercentage ?? null,
    unilateral: e.metadata?.isUnilateral ?? false,
  };
}

/** Main lifts rest 180 s, everything else 90 s, when the server did not say; cardio none. */
export function restSecondsFor(category: string, restSeconds: number | undefined): number {
  if (category === 'Cardio') return 0;
  if (restSeconds && restSeconds > 0) return restSeconds;
  return category === 'MainLift' ? 180 : 90;
}

function doseText(drill: MobilityRoutineDto['drills'][number]): string {
  const unit = drill.dose.unit === DoseUnit.Seconds ? 's' : drill.dose.unit === DoseUnit.Breaths ? ' breaths' : ' reps';
  const side = drill.perSide ? ' each side' : '';
  return `${drill.sets} × ${drill.dose.amount}${unit}${side}`;
}

export function createGymSession(input: CreateGymSessionInput): GymSessionState {
  const { plan } = input;
  const exercises = [...plan.exercises].sort((a, b) => a.orderInDay - b.orderInDay);
  return {
    version: GYM_SESSION_VERSION,
    clientSessionId: input.clientSessionId,
    workoutId: plan.workoutId,
    day: plan.dayNumber,
    week: plan.weekNumber,
    title: input.title,
    startedAt: input.startedAt,
    drills: (input.warmUp?.drills ?? []).map((d) => ({ name: d.name, dose: doseText(d), done: false })),
    lifts: exercises.filter((e) => !e.cardioBlock && e.plannedSets.length > 0).map(liftFromPlan),
    cardio: exercises.flatMap((e) => (e.cardioBlock ? [cardioFromPlan({ ...e, cardioBlock: e.cardioBlock })] : [])),
    finisher: input.finisherFixed
      ? fixedFinisher(input.finisherFixed)
      : input.finisherTrack
      ? {
          protocolId: '',
          trackId: input.finisherTrack.id,
          stepUp: stepUpText(input.finisherTrack),
          name: input.finisherTrack.name,
          prescription: input.finisherTrack.prescription.title,
          minutes: input.finisherTrack.prescription.minutes || null,
          ...(input.finisherTrack.prescription.warmUp || input.finisherTrack.prescription.coolDown
            ? {
                workMinutes: input.finisherTrack.prescription.workMinutes,
                warmUp: input.finisherTrack.prescription.warmUp ?? null,
                coolDown: input.finisherTrack.prescription.coolDown ?? null,
              }
            : {}),
          ...(trackIntervals(input.finisherTrack) ? { intervals: trackIntervals(input.finisherTrack) } : {}),
          level: input.finisherTrack.prescription.intensity ?? null,
          floors: null,
          effort: null,
          pain: 'None',
        }
      : input.finisher
      ? {
          protocolId: input.finisher.id,
          name: input.finisher.name,
          prescription: input.finisher.notes ? `${input.finisher.totalMinutes} min · ${input.finisher.notes}` : `${input.finisher.totalMinutes} min`,
          minutes: input.finisher.totalMinutes || null,
          level: null,
          floors: null,
          effort: null,
        }
      : null,
  };
}

export function tickDrill(state: GymSessionState, index: number): GymSessionState {
  return { ...state, drills: state.drills.map((d, i) => (i === index ? { ...d, done: !d.done } : d)) };
}

export function allDrillsDone(state: GymSessionState): GymSessionState {
  return { ...state, drills: state.drills.map((d) => ({ ...d, done: true })) };
}

function updateSet(state: GymSessionState, lift: number, set: number, change: (s: GymSet) => GymSet): GymSessionState {
  return {
    ...state,
    lifts: state.lifts.map((l, li) => (li === lift ? { ...l, sets: l.sets.map((s, si) => (si === set ? change(s) : s)) } : l)),
  };
}

/** Confirms a set as done, optionally with the reps actually done (the AMRAP prompt). Tapping a done set undoes it. */
export function toggleSet(state: GymSessionState, lift: number, set: number, reps?: number): GymSessionState {
  return updateSet(state, lift, set, (s) =>
    s.done && reps === undefined ? { ...s, done: false } : { ...s, done: true, reps: reps ?? s.reps }
  );
}

/** Edits a set's weight or reps (different from the plan). Values are clamped to sensible bounds. */
export function editSet(state: GymSessionState, lift: number, set: number, change: { weight?: number; reps?: number }): GymSessionState {
  return updateSet(state, lift, set, (s) => ({
    ...s,
    weight: change.weight === undefined ? s.weight : Math.min(Math.max(0, change.weight), 1000),
    reps: change.reps === undefined ? s.reps : Math.min(Math.max(0, Math.round(change.reps)), 200),
  }));
}

export function updateFinisher(state: GymSessionState, change: Partial<Omit<GymFinisher, 'protocolId' | 'trackId' | 'stepUp' | 'name' | 'prescription'>>): GymSessionState {
  return state.finisher ? { ...state, finisher: { ...state.finisher, ...change } } : state;
}

export type Side = 'Left' | 'Right';

/** The rows of one set: "Left" then "Right" for a unilateral lift (left side first), one row otherwise. */
export function sideRows(lift: GymLift): (Side | null)[] {
  return lift.unilateral ? ['Left', 'Right'] : [null];
}

/** Ticks one side of a unilateral set: Left is its own tick, Right confirms the set (as `toggleSet`). */
export function toggleSide(state: GymSessionState, lift: number, set: number, side: Side, reps?: number): GymSessionState {
  if (side === 'Right') return toggleSet(state, lift, set, reps);
  return updateSet(state, lift, set, (s) => ({ ...s, leftDone: !s.leftDone }));
}

/** Ticks (or unticks) the fixed cool-down. */
export function toggleFixedFinisher(state: GymSessionState): GymSessionState {
  return state.finisher?.fixed ? { ...state, finisher: { ...state.finisher, done: !state.finisher.done } } : state;
}

/** Ticks (or unticks) a lift's extra set; with a change, edits its weight or reps instead. */
export function toggleExtraSet(state: GymSessionState, lift: number, change?: { weight?: number; reps?: number }): GymSessionState {
  return {
    ...state,
    lifts: state.lifts.map((l, li) =>
      li === lift && l.extraSet ? { ...l, extraSet: { ...l.extraSet, ...change, done: change ? l.extraSet.done : !l.extraSet.done } } : l
    ),
  };
}

/** One step of a block: which lift (index into `state.lifts`) and which of its sets. */
export interface SetStep {
  lift: number;
  set: number;
}

/** A card in the checklist: one lift, or a superset (A then B) labelled 2a/2b. */
export interface LiftBlock {
  lifts: number[];
  /** "2a"/"2b" for a superset, null for a lift on its own. */
  labels: (string | null)[];
  /** The order sets are done in: A1 → B1 → A2 → B2 …, then whatever is left of the longer one. */
  order: SetStep[];
}

/** Groups the lifts into blocks: lifts sharing a superset number go together (in plan order). */
export function liftBlocks(state: GymSessionState): LiftBlock[] {
  const blocks: LiftBlock[] = [];
  const byGroup = new Map<number, LiftBlock>();
  state.lifts.forEach((lift, i) => {
    const group = lift.supersetGroup ?? null;
    const partners = group === null ? 0 : state.lifts.filter((l) => l.supersetGroup === group).length;
    if (group !== null && partners > 1) {
      const existing = byGroup.get(group);
      if (existing) {
        existing.lifts.push(i);
        return;
      }
      const block: LiftBlock = { lifts: [i], labels: [], order: [] };
      byGroup.set(group, block);
      blocks.push(block);
      return;
    }
    blocks.push({ lifts: [i], labels: [null], order: lift.sets.map((_, s) => ({ lift: i, set: s })) });
  });
  for (const [group, block] of byGroup) {
    block.labels = block.lifts.map((_, k) => `${group}${String.fromCharCode(97 + k)}`);
    const longest = Math.max(...block.lifts.map((l) => state.lifts[l].sets.length));
    for (let s = 0; s < longest; s++) {
      for (const l of block.lifts) if (s < state.lifts[l].sets.length) block.order.push({ lift: l, set: s });
    }
  }
  return blocks;
}

/**
 * Rest to start after confirming a set: none when the next set in its superset is the partner's and
 * still to do (go straight from A to B), otherwise the lift's rest (after B, the superset rest).
 */
export function restAfterSet(state: GymSessionState, lift: number, set: number): number | null {
  const rest = state.lifts[lift]?.restSeconds ?? 90;
  const block = liftBlocks(state).find((b) => b.lifts.includes(lift));
  if (!block || block.lifts.length < 2) return rest;
  const at = block.order.findIndex((s) => s.lift === lift && s.set === set);
  const next = block.order[at + 1];
  if (next && next.lift !== lift && block.lifts.indexOf(next.lift) > block.lifts.indexOf(lift)) {
    const nextSet = state.lifts[next.lift].sets[next.set];
    if (nextSet && !nextSet.done) return null;
  }
  return rest;
}

/**
 * After a training max is set the week plan comes back with weights: give a pending lift its planned
 * weights (sets not yet done keep nothing of the 0 kg) and clear the flag. Other lifts are untouched.
 */
export function syncPendingLifts(state: GymSessionState, plan: WeekPlanDto): GymSessionState {
  let changed = false;
  const lifts = state.lifts.map((lift) => {
    if (!lift.trainingMaxPending) return lift;
    const planned = plan.exercises.find((e) => e.exerciseId === lift.exerciseId);
    if (!planned || planned.metadata?.isTrainingMaxPending) return lift;
    changed = true;
    const fresh = liftFromPlan(planned);
    return {
      ...lift,
      trainingMaxPending: false,
      sets: lift.sets.map((s) => {
        const p = fresh.sets.find((f) => f.setNumber === s.setNumber);
        if (!p) return s;
        return s.done ? { ...s, plannedWeight: p.plannedWeight } : { ...s, plannedWeight: p.plannedWeight, weight: p.weight };
      }),
      extraSet: lift.extraSet && !lift.extraSet.done ? { ...lift.extraSet, weight: fresh.extraSet?.weight ?? lift.extraSet.weight } : lift.extraSet,
    };
  });
  return changed ? { ...state, lifts } : state;
}

export interface GymProgress {
  setsDone: number;
  setsTotal: number;
  drillsDone: number;
  finisherLogged: boolean;
  /** Every drill ticked, every set confirmed and the finisher (if any) logged. */
  complete: boolean;
  /** At least one set confirmed and no logged lift waiting for its training max; Finish needs this. */
  canFinish: boolean;
  /** Lifts with logged sets that still need a training max before the session can finish. */
  needsTrainingMax: string[];
}

export function isFinisherLogged(finisher: GymFinisher | null): boolean {
  if (finisher?.fixed) return finisher.done === true;
  return !!finisher && !!finisher.minutes && finisher.minutes > 0 && finisher.effort !== null;
}

export function progressOf(state: GymSessionState): GymProgress {
  const sets = state.lifts.flatMap((l) => l.sets);
  const setsDone = sets.filter((s) => s.done).length;
  const drillsDone = state.drills.filter((d) => d.done).length;
  const finisherLogged = isFinisherLogged(state.finisher);
  const needsTrainingMax = state.lifts.filter((l) => l.trainingMaxPending && l.sets.some((s) => s.done)).map((l) => l.name);
  const cardio = state.cardio.filter((c) => !c.skipped);
  const cardioDone = cardio.filter(isCardioDone).length;
  return {
    setsDone,
    setsTotal: sets.length,
    drillsDone,
    finisherLogged,
    complete:
      setsDone === sets.length && drillsDone === state.drills.length && cardioDone === cardio.length && (state.finisher === null || finisherLogged),
    canFinish: (setsDone > 0 || cardioDone > 0) && needsTrainingMax.length === 0,
    needsTrainingMax,
  };
}

/** The server's finish body. Only confirmed sets go; a lift with none is left out, so its progression is untouched. */
export function toFinishRequest(state: GymSessionState, endedAt: string): FinishGymSessionRequest {
  const unit = (lift: GymLift) => (lift.unit === 'lbs' ? WeightUnit.Pounds : WeightUnit.Kilograms);
  const finisher = state.finisher && isFinisherLogged(state.finisher) ? state.finisher : null;
  return {
    clientSessionId: state.clientSessionId,
    workoutId: state.workoutId,
    day: state.day,
    startedAt: state.startedAt,
    endedAt: endedAt < state.startedAt ? state.startedAt : endedAt,
    warmUpTicks: state.drills.length > 0 ? state.drills.map((d) => d.done) : null,
    performances: [
      ...state.lifts
        .map((lift) => {
          const extra = lift.extraSet?.done ? lift.extraSet : null;
          return {
            exerciseId: lift.exerciseId,
            completedSets: lift.sets
              .filter((s) => s.done)
              .map((s) => ({ setNumber: s.setNumber, weight: s.weight, weightUnit: unit(lift), actualReps: s.reps, wasAmrap: s.isAmrap })),
            ...(extra
              ? { extraSet: { setNumber: lift.sets.length + 1, weight: extra.weight, weightUnit: unit(lift), actualReps: extra.reps, wasAmrap: false } }
              : {}),
          };
        })
        .filter((p) => p.completedSets.length > 0),
      ...cardioPerformances(state),
    ],
    finisher: finisher?.fixed
      ? { durationSeconds: finisher.fixed.minutes * 60, effort: 'Easy', completedAsPrescribed: true }
      : finisher
      ? {
          durationSeconds: Math.round(finisher.minutes! * 60),
          effort: finisher.effort!,
          level: finisher.level,
          floors: finisher.floors,
          ...(finisher.workMinutes != null && finisher.workMinutes > 0 ? { workMinutes: finisher.workMinutes } : {}),
          ...(finisher.pain && finisher.pain !== 'None' ? { pain: finisher.pain } : {}),
        }
      : null,
  };
}

export const GYM_SESSION_PREFIX = 'a2s-gym-session:';
const storageKey = (workoutId: string, week: number, day: number) => `${GYM_SESSION_PREFIX}${workoutId}:${week}:${day}`;

/** Drafts older than this are dropped: nobody resumes a gym session a week later. */
export const GYM_DRAFT_MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000;

const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);

/**
 * Brings a saved draft up to the current shape. A draft without `version` is v1 (the shape before
 * versioning was added). Anything else that cannot be read as a session (wrong types, a version from a
 * newer app) returns null so the caller can discard it; a recognisable draft is never thrown away.
 */
export function migrateGymSession(raw: unknown): GymSessionState | null {
  if (!isRecord(raw)) return null;
  const version = raw.version ?? 1;
  if (version !== 1 && version !== 2) return null;
  const valid =
    typeof raw.clientSessionId === 'string' &&
    raw.clientSessionId !== '' &&
    typeof raw.workoutId === 'string' &&
    typeof raw.day === 'number' &&
    typeof raw.week === 'number' &&
    typeof raw.startedAt === 'string' &&
    Array.isArray(raw.lifts) &&
    (raw.drills === undefined || Array.isArray(raw.drills));
  if (!valid) return null;
  return {
    ...(raw as unknown as GymSessionState),
    drills: (raw.drills as GymDrill[] | undefined) ?? [],
    // v1 → v2: no cardio exercises; ticked sets are kept as they are.
    cardio: Array.isArray(raw.cardio) ? (raw.cardio as GymCardio[]) : [],
    finisher: (raw.finisher as GymFinisher | null | undefined) ?? null,
    version: GYM_SESSION_VERSION,
  };
}

export function saveGymSession(state: GymSessionState): void {
  try {
    localStorage.setItem(storageKey(state.workoutId, state.week, state.day), JSON.stringify(state));
  } catch {
    /* private mode or full storage: the session still works, it just won't survive a reload */
  }
}

export function loadGymSession(workoutId: string, week: number, day: number): GymSessionState | null {
  try {
    const raw = localStorage.getItem(storageKey(workoutId, week, day));
    if (!raw) return null;
    let parsed: unknown = null;
    try {
      parsed = JSON.parse(raw);
    } catch {
      /* unreadable: discarded below */
    }
    const migrated = migrateGymSession(parsed);
    if (!migrated) localStorage.removeItem(storageKey(workoutId, week, day));
    return migrated;
  } catch {
    return null;
  }
}

export interface ExpireGymDraftsOptions {
  /** With `currentWeek`, drafts for this workout's earlier weeks go too. */
  workoutId?: string;
  currentWeek?: number;
}

/**
 * Removes stale gym drafts: any started more than 7 days before `now`, and, when the workout and its
 * current week are known, that workout's drafts for earlier weeks. Unreadable drafts are left for
 * `loadGymSession` to deal with. Returns the keys it removed.
 */
export function expireGymDrafts(now: Date = new Date(), options: ExpireGymDraftsOptions = {}): string[] {
  const removed: string[] = [];
  try {
    const keys: string[] = [];
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (key?.startsWith(GYM_SESSION_PREFIX)) keys.push(key);
    }
    for (const key of keys) {
      let draft: GymSessionState | null = null;
      try {
        draft = migrateGymSession(JSON.parse(localStorage.getItem(key) ?? 'null'));
      } catch {
        continue;
      }
      if (!draft) continue;
      const started = Date.parse(draft.startedAt);
      const tooOld = Number.isFinite(started) && now.getTime() - started > GYM_DRAFT_MAX_AGE_MS;
      const pastWeek =
        options.workoutId !== undefined &&
        options.currentWeek !== undefined &&
        draft.workoutId === options.workoutId &&
        draft.week < options.currentWeek;
      if (tooOld || pastWeek) {
        localStorage.removeItem(key);
        removed.push(key);
      }
    }
  } catch {
    /* storage unavailable: nothing to expire */
  }
  return removed;
}

export function clearGymSession(workoutId: string, week: number, day: number): void {
  try {
    localStorage.removeItem(storageKey(workoutId, week, day));
  } catch {
    /* ignore */
  }
}

/** A v4 UUID for the client session id, generated on the phone when the session starts. */
export function newClientSessionId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  const bytes = Array.from({ length: 16 }, () => Math.floor(Math.random() * 256));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = bytes.map((b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
