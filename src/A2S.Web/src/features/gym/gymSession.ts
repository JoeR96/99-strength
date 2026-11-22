/**
 * Gym mode's checklist state and the pure steps that change it (brief § 3a). One session, in order:
 * warm-up drills, then each lift with its planned sets pre-filled, then the attached finisher. Every
 * step returns a new state, which the page saves to localStorage straight away, so nothing logged is
 * lost to a reload or a dead battery. `toFinishRequest` turns it into the server's finish body: only
 * sets that were confirmed are sent, and a lift with none is left out (its progression waits).
 */

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
  name: string;
  prescription: string;
  minutes: number | null;
  level: number | null;
  floors: number | null;
  effort: Effort | null;
}

export interface GymSessionState {
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
    finisher: input.finisher
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

export function updateFinisher(state: GymSessionState, change: Partial<Omit<GymFinisher, 'protocolId' | 'name' | 'prescription'>>): GymSessionState {
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
      ? { durationSeconds: Math.round(finisher.minutes! * 60), effort: finisher.effort!, level: finisher.level, floors: finisher.floors }
      : null,
  };
}

const storageKey = (workoutId: string, week: number, day: number) => `a2s-gym-session:${workoutId}:${week}:${day}`;

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
    const parsed = JSON.parse(raw) as GymSessionState;
    return parsed && parsed.clientSessionId && Array.isArray(parsed.lifts) ? parsed : null;
  } catch {
    return null;
  }
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
