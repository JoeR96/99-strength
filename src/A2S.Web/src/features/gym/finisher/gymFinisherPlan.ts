/**
 * The gym session's finisher, or the stair climber cardio exercise, as wizard phases. Only a cardio-track
 * finisher (the stair finisher) and the hard-lane cardio exercise get the live wizard; walks do not.
 */

import type { PlannedCardioBlockDto } from '@/api/workouts';
import type { GymFinisher, GymSessionState } from '../gymSession';
import type { GymCardio } from '../gymCardio';
import { buildFinisherPlan, type FinisherPhase } from './finisherPlan';
import type { FinisherSummary } from './finisherEngine';

export function finisherPhases(finisher: GymFinisher | null): FinisherPhase[] {
  if (!finisher || finisher.fixed || !finisher.trackId) return [];
  const segmented = !!finisher.warmUp || !!finisher.coolDown;
  return buildFinisherPlan({
    warmUp: finisher.warmUp,
    coolDown: finisher.coolDown,
    workMinutes: segmented ? (finisher.workMinutes ?? null) : finisher.minutes,
    level: finisher.level,
    intervals: finisher.intervals ?? null,
  });
}

/** Rounds to the nearest half minute (what the log's number fields expect). */
function halfMinutes(seconds: number): number {
  return Math.round((seconds / 60) * 2) / 2;
}

/** What the wizard prefills on the finisher log; effort and floors stay for the person. */
export function finisherLogFromSummary(finisher: GymFinisher, summary: FinisherSummary): Partial<Pick<GymFinisher, 'minutes' | 'workMinutes' | 'level'>> {
  const segmented = !!finisher.warmUp || !!finisher.coolDown;
  return {
    minutes: halfMinutes(summary.totalSeconds),
    ...(segmented ? { workMinutes: halfMinutes(summary.workSeconds) } : {}),
    level: summary.level ?? finisher.level,
  };
}

/** A cardio exercise's block as phases: recoveries only between interval rounds, at the block's recovery level. */
export function cardioPhases(block: PlannedCardioBlockDto): FinisherPhase[] {
  const iv = block.kind === 'Intervals' ? block.intervals : null;
  return buildFinisherPlan({
    warmUp: block.warmUp,
    coolDown: block.coolDown,
    workMinutes: block.workMinutes,
    level: block.intensity,
    intervals: iv
      ? { rounds: iv.rounds, workSeconds: iv.hardMinutes * 60, restSeconds: iv.easyMinutes * 60, restAfterLastRound: false, easyLevel: block.recoveryLevel }
      : null,
  });
}

/** What the wizard prefills on a cardio item: total seconds, work minutes and level; effort and floors stay for the person. */
export function cardioLogFromSummary(item: GymCardio, summary: FinisherSummary): Pick<GymCardio, 'totalSeconds' | 'workMinutes' | 'intensity'> {
  return {
    totalSeconds: Math.round(summary.totalSeconds),
    workMinutes: halfMinutes(summary.workSeconds),
    intensity: summary.level ?? item.intensity ?? null,
  };
}

/** What the live wizard runs: the session's stair finisher, else the stair climber cardio exercise (not skipped, settings set). */
export interface WizardTarget {
  name: string;
  phases: FinisherPhase[];
  /** Index into `state.cardio`; null for the finisher. */
  cardioIndex: number | null;
}

export function wizardTarget(state: Pick<GymSessionState, 'finisher' | 'cardio'>): WizardTarget | null {
  const phases = finisherPhases(state.finisher);
  if (state.finisher && phases.length > 0) return { name: state.finisher.name, phases, cardioIndex: null };
  const index = state.cardio.findIndex((c) => c.lane === 'Hard' && !c.skipped && !c.block.isSettingsPending);
  if (index < 0) return null;
  return { name: state.cardio[index].name, phases: cardioPhases(state.cardio[index].block), cardioIndex: index };
}
