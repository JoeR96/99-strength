/**
 * The live finisher wizard's wall-clock engine. The state holds only instants (ms since epoch) and
 * what the person did (time spent per finished phase, the ±30 s on the current one); where the session
 * is "now" is always derived from the clock, so a page that was backgrounded, suspended or reloaded
 * lands on the right phase the moment it looks again. Pure functions; `useFinisherEngine` adds the
 * ticking and localStorage.
 */

import { isWorkPhase, phaseCue, type FinisherPhase } from './finisherPlan';

export const FINISHER_ENGINE_VERSION = 1;

export interface FinisherEngineState {
  version: number;
  clientSessionId: string;
  phases: FinisherPhase[];
  /** When Start was tapped. */
  startedAt: number;
  /** The current phase's index (phases.length: finished). */
  index: number;
  /** When the current phase started (wall clock). */
  phaseStartedAt: number;
  /** Set while paused. */
  pausedAt: number | null;
  /** Pause time inside the current phase (already resumed). */
  phasePausedMs: number;
  /** Pause time over the whole session. */
  pausedTotalMs: number;
  /** ±30 s taps on the current phase. */
  adjustMs: number;
  /** Time actually spent in each finished phase, in order. */
  doneMs: number[];
}

export interface FinisherView {
  phase: FinisherPhase | null;
  index: number;
  next: FinisherPhase | null;
  remainingMs: number;
  phaseMs: number;
  /** 0..1 */
  phaseProgress: number;
  /** 0..1 */
  sessionProgress: number;
  paused: boolean;
  finished: boolean;
}

export interface FinisherSummary {
  totalSeconds: number;
  workSeconds: number;
  /** The highest work level performed. */
  level: number | null;
  rounds: number;
}

export interface ScheduledNudge {
  at: string;
  body: string;
}

const RESTART_WINDOW_MS = 3000;

export function startEngine(clientSessionId: string, phases: FinisherPhase[], now: number): FinisherEngineState {
  return {
    version: FINISHER_ENGINE_VERSION,
    clientSessionId,
    phases,
    startedAt: now,
    index: 0,
    phaseStartedAt: now,
    pausedAt: null,
    phasePausedMs: 0,
    pausedTotalMs: 0,
    adjustMs: 0,
    doneMs: [],
  };
}

function durationMs(state: FinisherEngineState): number {
  const phase = state.phases[state.index];
  return phase ? Math.max(0, phase.seconds * 1000 + state.adjustMs) : 0;
}

/** Time spent in the current phase (pauses excluded). */
function intoMs(state: FinisherEngineState, now: number): number {
  const until = state.pausedAt ?? now;
  return Math.max(0, until - state.phaseStartedAt - state.phasePausedMs);
}

export function isFinished(state: FinisherEngineState): boolean {
  return state.index >= state.phases.length;
}

/** Rolls the state forward over every phase that has ended by `now`. Returns the same object when nothing changed. */
export function advance(state: FinisherEngineState, now: number): FinisherEngineState {
  if (state.pausedAt !== null || isFinished(state)) return state;
  let s = state;
  while (!isFinished(s)) {
    const dur = durationMs(s);
    if (intoMs(s, now) < dur) break;
    s = {
      ...s,
      doneMs: [...s.doneMs, dur],
      index: s.index + 1,
      phaseStartedAt: s.phaseStartedAt + s.phasePausedMs + dur,
      phasePausedMs: 0,
      adjustMs: 0,
    };
  }
  return s;
}

export function pause(state: FinisherEngineState, now: number): FinisherEngineState {
  const s = advance(state, now);
  return s.pausedAt !== null || isFinished(s) ? s : { ...s, pausedAt: now };
}

export function resume(state: FinisherEngineState, now: number): FinisherEngineState {
  if (state.pausedAt === null) return state;
  const paused = Math.max(0, now - state.pausedAt);
  return { ...state, pausedAt: null, phasePausedMs: state.phasePausedMs + paused, pausedTotalMs: state.pausedTotalMs + paused };
}

/** Ends the current phase now (time spent so far is what counts). */
export function skip(state: FinisherEngineState, now: number): FinisherEngineState {
  const s = advance(state, now);
  if (isFinished(s)) return s;
  return {
    ...s,
    doneMs: [...s.doneMs, intoMs(s, now)],
    index: s.index + 1,
    phaseStartedAt: now,
    pausedAt: s.pausedAt === null ? null : now,
    phasePausedMs: 0,
    adjustMs: 0,
  };
}

/** Restarts the current phase; within its first 3 s, goes back to the previous one instead. */
export function back(state: FinisherEngineState, now: number): FinisherEngineState {
  const s = advance(state, now);
  const restart = { phaseStartedAt: now, pausedAt: s.pausedAt === null ? null : now, phasePausedMs: 0, adjustMs: 0 };
  if (!isFinished(s) && (intoMs(s, now) > RESTART_WINDOW_MS || s.index === 0)) {
    return { ...s, ...restart };
  }
  if (s.index === 0) return s;
  return { ...s, ...restart, index: s.index - 1, doneMs: s.doneMs.slice(0, -1) };
}

/** ±30 s on the current phase; taking time off never ends it in the past (it ends now at the earliest). */
export function adjust(state: FinisherEngineState, deltaSeconds: number, now: number): FinisherEngineState {
  const s = advance(state, now);
  const phase = s.phases[s.index];
  if (!phase) return s;
  const minAdjust = intoMs(s, now) - phase.seconds * 1000;
  const next = Math.max(minAdjust, s.adjustMs + deltaSeconds * 1000);
  return advance({ ...s, adjustMs: next }, now);
}

export function viewOf(state: FinisherEngineState, now: number): FinisherView {
  const s = advance(state, now);
  const phase = s.phases[s.index] ?? null;
  const phaseMs = durationMs(s);
  const into = phase ? Math.min(intoMs(s, now), phaseMs) : 0;
  const doneTotal = s.doneMs.reduce((a, b) => a + b, 0);
  const ahead = s.phases.slice(s.index + 1).reduce((a, p) => a + p.seconds * 1000, 0);
  const total = doneTotal + phaseMs + ahead;
  return {
    phase,
    index: s.index,
    next: s.phases[s.index + 1] ?? null,
    remainingMs: phase ? phaseMs - into : 0,
    phaseMs,
    phaseProgress: phase && phaseMs > 0 ? into / phaseMs : 1,
    sessionProgress: total > 0 ? Math.min(1, (doneTotal + into) / total) : 1,
    paused: s.pausedAt !== null,
    finished: isFinished(s),
  };
}

/** Pass 
ow to count phases that ended on the clock but were not rolled into the state yet. */
export function summaryOf(input: FinisherEngineState, now?: number): FinisherSummary {
  const state = now === undefined ? input : advance(input, now);
  let total = 0;
  let work = 0;
  let level: number | null = null;
  let rounds = 0;
  state.doneMs.forEach((ms, i) => {
    const phase = state.phases[i];
    if (!phase) return;
    total += ms;
    if (isWorkPhase(phase.kind)) work += ms;
    if ((phase.kind === 'work' || phase.kind === 'hard') && ms > 0 && phase.level != null) level = Math.max(level ?? 0, phase.level);
    if (phase.kind === 'hard' && ms > 0) rounds++;
  });
  return { totalSeconds: Math.round(total / 1000), workSeconds: Math.round(work / 1000), level, rounds };
}

/** Every phase change still ahead as absolute UTC times; empty while paused or finished (nothing to announce). */
export function scheduleOf(state: FinisherEngineState, now: number): ScheduledNudge[] {
  const s = advance(state, now);
  if (s.pausedAt !== null || isFinished(s)) return [];
  const nudges: ScheduledNudge[] = [];
  let at = now + (durationMs(s) - intoMs(s, now));
  for (let i = s.index + 1; i < s.phases.length; i++) {
    nudges.push({ at: new Date(at).toISOString(), body: phaseCue(s.phases[i]) });
    at += s.phases[i].seconds * 1000;
  }
  nudges.push({ at: new Date(at).toISOString(), body: 'Done: log your floors' });
  return nudges;
}

// --- persistence (keyed by the gym session's client id) ---

export function finisherStorageKey(clientSessionId: string): string {
  return `a2s.finisher.${clientSessionId}`;
}

export function saveEngine(state: FinisherEngineState): void {
  try {
    localStorage.setItem(finisherStorageKey(state.clientSessionId), JSON.stringify(state));
  } catch {
    /* storage full or blocked: the wizard still runs from memory */
  }
}

export function loadEngine(clientSessionId: string): FinisherEngineState | null {
  try {
    const raw = localStorage.getItem(finisherStorageKey(clientSessionId));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as FinisherEngineState;
    if (parsed?.version !== FINISHER_ENGINE_VERSION || parsed.clientSessionId !== clientSessionId || !Array.isArray(parsed.phases)) return null;
    return parsed;
  } catch {
    return null;
  }
}

export function clearEngine(clientSessionId: string): void {
  try {
    localStorage.removeItem(finisherStorageKey(clientSessionId));
  } catch {
    /* ignore */
  }
}
