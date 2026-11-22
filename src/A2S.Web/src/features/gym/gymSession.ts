/**
 * Gym mode's checklist state and the pure steps that change it (brief § 3a). One session, in order:
 * warm-up drills, then each lift with its planned sets pre-filled, then the attached finisher. Every
 * step returns a new state, which the page saves to localStorage straight away, so nothing logged is
 * lost to a reload or a dead battery. `toFinishRequest` turns it into the server's finish body: only
 * sets that were confirmed are sent, and a lift with none is left out (its progression waits).
 */

import type { CardioTrackDto } from '@/types/cardioTracks';
import { stepUpText } from '@/features/cardio/trackText';
import type { WeekPlanDto } from '@/api/workouts';
import type { MobilityRoutineDto } from '@/types/mobility';
import { DoseUnit } from '@/types/mobility';
import { WeightUnit } from '@/types/workout';
import type { CardioProtocolDto, Effort, FinishGymSessionRequest } from '@/types/gym';

export interface GymSet {
  setNumber: number;
  plannedWeight: number;
  plannedReps: number;
  weight: number;
  reps: number;
  isAmrap: boolean;
  done: boolean;
}

export interface GymLift {
  exerciseId: string;
  name: string;
  restSeconds: number;
  unit: 'kg' | 'lbs';
  sets: GymSet[];
}

export interface GymDrill {
  name: string;
  dose: string;
  done: boolean;
}

export interface GymFinisher {
  protocolId: string;
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
}

/** Bump when GymSessionState changes shape, and teach `migrateGymSession` to upgrade the old one. */
export const GYM_SESSION_VERSION = 1;

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
}

/** Main lifts rest 180 s, everything else 90 s, when the server did not say. */
export function restSecondsFor(category: string, restSeconds: number | undefined): number {
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
  return {
    version: GYM_SESSION_VERSION,
    clientSessionId: input.clientSessionId,
    workoutId: plan.workoutId,
    day: plan.dayNumber,
    week: plan.weekNumber,
    title: input.title,
    startedAt: input.startedAt,
    drills: (input.warmUp?.drills ?? []).map((d) => ({ name: d.name, dose: doseText(d), done: false })),
    lifts: [...plan.exercises]
      .sort((a, b) => a.orderInDay - b.orderInDay)
      .filter((e) => e.plannedSets.length > 0)
      .map((e) => {
        const unit = e.plannedSets[0]?.originalUnit === 'Pounds' ? 'lbs' : 'kg';
        return {
          exerciseId: e.exerciseId,
          name: e.name,
          restSeconds: restSecondsFor(e.category, e.restSeconds),
          unit,
          sets: e.plannedSets.map((s) => {
            const weight = unit === 'lbs' ? s.weightLbs : s.weightKg;
            return { setNumber: s.setNumber, plannedWeight: weight, plannedReps: s.targetReps, weight, reps: s.targetReps, isAmrap: s.isAmrap, done: false };
          }),
        };
      }),
    finisher: input.finisherTrack
      ? {
          protocolId: '',
          trackId: input.finisherTrack.id,
          stepUp: stepUpText(input.finisherTrack),
          name: input.finisherTrack.name,
          prescription: input.finisherTrack.prescription.title,
          minutes: input.finisherTrack.prescription.minutes || null,
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

export interface GymProgress {
  setsDone: number;
  setsTotal: number;
  drillsDone: number;
  finisherLogged: boolean;
  /** Every drill ticked, every set confirmed and the finisher (if any) logged. */
  complete: boolean;
  /** At least one set confirmed; Finish needs this. */
  canFinish: boolean;
}

export function isFinisherLogged(finisher: GymFinisher | null): boolean {
  return !!finisher && !!finisher.minutes && finisher.minutes > 0 && finisher.effort !== null;
}

export function progressOf(state: GymSessionState): GymProgress {
  const sets = state.lifts.flatMap((l) => l.sets);
  const setsDone = sets.filter((s) => s.done).length;
  const drillsDone = state.drills.filter((d) => d.done).length;
  const finisherLogged = isFinisherLogged(state.finisher);
  return {
    setsDone,
    setsTotal: sets.length,
    drillsDone,
    finisherLogged,
    complete: setsDone === sets.length && drillsDone === state.drills.length && (state.finisher === null || finisherLogged),
    canFinish: setsDone > 0,
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
    performances: state.lifts
      .map((lift) => ({
        exerciseId: lift.exerciseId,
        completedSets: lift.sets
          .filter((s) => s.done)
          .map((s) => ({ setNumber: s.setNumber, weight: s.weight, weightUnit: unit(lift), actualReps: s.reps, wasAmrap: s.isAmrap })),
      }))
      .filter((p) => p.completedSets.length > 0),
    finisher: finisher
      ? {
          durationSeconds: Math.round(finisher.minutes! * 60),
          effort: finisher.effort!,
          level: finisher.level,
          floors: finisher.floors,
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
  if (version !== 1) return null;
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
  return { ...(raw as unknown as GymSessionState), drills: (raw.drills as GymDrill[] | undefined) ?? [], finisher: (raw.finisher as GymFinisher | null | undefined) ?? null, version: GYM_SESSION_VERSION };
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
