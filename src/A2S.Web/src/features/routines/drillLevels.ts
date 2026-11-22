/**
 * Pure helpers for drills with a progression chain (Easier / Standard / Harder, or a Level 1-3 ladder):
 * the easier and harder neighbours the player offers, the default per-drill feedback after a session,
 * and the "Next time" lines shown once the server has run the progression rule.
 */

import {
  DoseUnit,
  DrillChangeKind,
  DrillOutcome,
  type DrillDoseDto,
  type DrillFeedbackDto,
  type DrillLevelDto,
  type DrillProgressionEntryDto,
  type MobilityDrillDto,
  type MobilityRoutineDto,
} from '@/types/mobility';
import { currentLevel } from './routinePlayer';

/** The levels either side of the current one. */
export function levelNeighbours(drill: MobilityDrillDto): { easier: DrillLevelDto | null; harder: DrillLevelDto | null } {
  const levels = drill.levels ?? [];
  const at = drill.currentLevel ?? 0;
  return { easier: levels[at - 1] ?? null, harder: levels[at + 1] ?? null };
}

/** "Level 2: hands on a bench". */
export function levelLabel(level: DrillLevelDto): string {
  return level.description ? `${level.name}: ${level.description}` : level.name;
}

/** "3 × 15 s", "2 × 5 breaths". */
export function shortDose(sets: number, dose: DrillDoseDto): string {
  const unit = { [DoseUnit.Reps]: '', [DoseUnit.Seconds]: ' s', [DoseUnit.Breaths]: ' breaths' }[dose.unit];
  return `${sets} × ${dose.amount}${unit}`;
}

/** Ticked drills default to clean, the rest to skipped. Only drills with levels progress, but all are sent. */
export function defaultFeedback(routine: MobilityRoutineDto, ticks: boolean[]): DrillFeedbackDto[] {
  return routine.drills.map((d, i) => ({ drillName: d.name, outcome: ticks[i] ? DrillOutcome.Clean : DrillOutcome.Skipped }));
}

/** Drills worth asking about after a session: ticked ones with a progression chain. */
export function drillsToRate(routine: MobilityRoutineDto, ticks: boolean[]): MobilityDrillDto[] {
  return routine.drills.filter((d, i) => ticks[i] && (d.levels?.length ?? 0) > 0);
}

/**
 * One line per change the session made, phrased for next time:
 * "Next time: Reverse plank ladder — 3 × 20 s", "Next time: Level 2: hands on a bench (accept it on the routine)".
 */
export function nextTimeLines(routine: MobilityRoutineDto, changes: DrillProgressionEntryDto[]): string[] {
  return changes.flatMap((change) => {
    const drill = routine.drills.find((d) => d.name === change.drillName);
    if (!drill) return [];
    switch (change.kind) {
      case DrillChangeKind.DoseIncreased:
        return [`Next time: ${drill.name} — ${shortDose(drill.sets, drill.dose)}`];
      case DrillChangeKind.LevelUpSuggested: {
        const target = drill.suggestedLevel != null ? drill.levels?.[drill.suggestedLevel] : null;
        return target ? [`Next time: ${levelLabel(target)}`] : [];
      }
      case DrillChangeKind.SteppedBack:
      case DrillChangeKind.Held:
        return [`${drill.name}: ${change.reason}`];
      default:
        return [];
    }
  });
}

/** The drill's prescription now, with its level when it has one: "Level 2 · 3 × 15 s". */
export function prescription(drill: MobilityDrillDto): string {
  const level = currentLevel(drill);
  const dose = shortDose(drill.sets, drill.dose);
  return level ? `${level.name} · ${dose}` : dose;
}
