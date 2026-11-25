/**
 * The live finisher wizard's phase plan: the workout's stair finisher prescription (warm-up, work
 * block, cool-down) as an ordered list of timed phases. A 1:1 interval block is expanded into its
 * hard and easy rounds. Pure: no clock, no storage.
 */

import type { TrackSegmentDto } from '@/types/cardioTracks';

export type FinisherPhaseKind = 'warmup' | 'work' | 'hard' | 'easy' | 'cooldown';

export interface FinisherPhase {
  kind: FinisherPhaseKind;
  seconds: number;
  level: number | null;
  zone: string | null;
  label: string;
  /** Interval phases: which round (1-based) and how many there are. */
  round?: number;
  rounds?: number;
}

export interface FinisherIntervals {
  rounds: number;
  workSeconds: number;
  restSeconds: number;
  /** Default true (1:1 finisher rounds). False: recoveries only between rounds (the stair climber's intervals). */
  restAfterLastRound?: boolean;
  /** The easy rounds' level; default the warm-up's (or cool-down's). */
  easyLevel?: number | null;
}

export interface FinisherPlanInput {
  warmUp?: TrackSegmentDto | null;
  coolDown?: TrackSegmentDto | null;
  /** The work block (continuous minutes, or the interval block's length when `intervals` is absent). */
  workMinutes: number | null;
  /** The work level (hard rounds for intervals). */
  level: number | null;
  intervals?: FinisherIntervals | null;
}

const LABELS: Record<FinisherPhaseKind, string> = {
  warmup: 'Warm-up',
  work: 'Work',
  hard: 'Hard',
  easy: 'Easy',
  cooldown: 'Cool-down',
};

/** Work-block phases count towards "work minutes" on the log. */
export function isWorkPhase(kind: FinisherPhaseKind): boolean {
  return kind === 'work' || kind === 'hard' || kind === 'easy';
}

function segment(kind: 'warmup' | 'cooldown', seg: TrackSegmentDto | null | undefined): FinisherPhase[] {
  if (!seg || !(seg.minutes > 0)) return [];
  return [{ kind, seconds: Math.round(seg.minutes * 60), level: seg.level ?? null, zone: seg.zone ?? null, label: LABELS[kind] }];
}

export function buildFinisherPlan(input: FinisherPlanInput): FinisherPhase[] {
  const phases: FinisherPhase[] = [...segment('warmup', input.warmUp)];
  const iv = input.intervals;
  const easyLevel = iv?.easyLevel ?? input.warmUp?.level ?? input.coolDown?.level ?? null;
  const easyZone = input.warmUp?.zone ?? input.coolDown?.zone ?? null;
  if (iv && iv.rounds > 0 && iv.workSeconds > 0) {
    for (let round = 1; round <= iv.rounds; round++) {
      phases.push({ kind: 'hard', seconds: iv.workSeconds, level: input.level, zone: null, label: LABELS.hard, round, rounds: iv.rounds });
      const last = round === iv.rounds;
      if (iv.restSeconds > 0 && !(last && iv.restAfterLastRound === false)) {
        phases.push({ kind: 'easy', seconds: iv.restSeconds, level: easyLevel, zone: easyZone, label: LABELS.easy, round, rounds: iv.rounds });
      }
    }
  } else if (input.workMinutes && input.workMinutes > 0) {
    phases.push({ kind: 'work', seconds: Math.round(input.workMinutes * 60), level: input.level, zone: null, label: LABELS.work });
  }
  phases.push(...segment('cooldown', input.coolDown));
  return phases;
}

/** "1 min", "30 s", "1 min 30 s". */
export function durationText(seconds: number): string {
  const s = Math.max(0, Math.round(seconds));
  const m = Math.floor(s / 60);
  const r = s % 60;
  if (m === 0) return `${r} s`;
  return r === 0 ? `${m} min` : `${m} min ${r} s`;
}

/** "level 6" or "" when there is no level. */
function levelText(level: number | null): string {
  return level == null ? '' : `level ${level}`;
}

/** "Hard: level 6 for 1 min" — the spoken cue and the notification text. */
export function phaseCue(phase: FinisherPhase): string {
  const lvl = levelText(phase.level);
  return `${phase.label}: ${lvl ? `${lvl} for ` : ''}${durationText(phase.seconds)}`;
}

/** "1 min hard @ L6" for the "Next:" line. */
export function nextText(phase: FinisherPhase): string {
  return `${durationText(phase.seconds)} ${phase.label.toLowerCase()}${phase.level != null ? ` @ L${phase.level}` : ''}`;
}
