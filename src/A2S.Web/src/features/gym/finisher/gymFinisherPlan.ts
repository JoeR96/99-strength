/** The gym session's finisher as wizard phases: only a cardio-track finisher (the stair finisher) gets the live wizard. */

import type { GymFinisher } from '../gymSession';
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
