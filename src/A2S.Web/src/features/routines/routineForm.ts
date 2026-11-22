/** Routine editor state and validation, separate from the page so it can be unit tested. */

import { DoseUnit, SideBias, type MobilityDrillDto, type MobilityRoutineDto } from '@/types/mobility';

export interface DrillDraft {
  /** Stable key for React lists while drills are reordered. */
  key: string;
  name: string;
  sets: string;
  amount: string;
  unit: DoseUnit;
  perSide: boolean;
  extraSetSide: SideBias;
  cues: string;
  standingAlternative: string;
}

export interface RoutineDraft {
  name: string;
  targetMinutes: string;
  drills: DrillDraft[];
}

export interface RoutineErrors {
  name?: string;
  targetMinutes?: string;
  drills?: string;
  /** Per drill, by key. */
  drill: Record<string, Partial<Record<'name' | 'sets' | 'amount', string>>>;
}

let nextKey = 0;
const newKey = () => `drill-${++nextKey}`;

export function emptyDrill(): DrillDraft {
  return { key: newKey(), name: '', sets: '2', amount: '30', unit: DoseUnit.Seconds, perSide: false, extraSetSide: SideBias.None, cues: '', standingAlternative: '' };
}

export function emptyRoutine(): RoutineDraft {
  return { name: '', targetMinutes: '15', drills: [emptyDrill()] };
}

export function draftFromRoutine(routine: MobilityRoutineDto): RoutineDraft {
  return {
    name: routine.name,
    targetMinutes: String(routine.targetMinutes),
    drills: routine.drills.map((d) => ({
      key: newKey(),
      name: d.name,
      sets: String(d.sets),
      amount: String(d.dose.amount),
      unit: d.dose.unit,
      perSide: d.perSide,
      extraSetSide: d.extraSetSide,
      cues: d.cues ?? '',
      standingAlternative: d.standingAlternative ?? '',
    })),
  };
}

const wholeNumber = (value: string) => /^\d+$/.test(value.trim()) ? Number(value) : NaN;

export function validateRoutine(draft: RoutineDraft): RoutineErrors {
  const errors: RoutineErrors = { drill: {} };
  if (!draft.name.trim()) errors.name = 'Give the routine a name';
  const minutes = wholeNumber(draft.targetMinutes);
  if (!(minutes >= 1 && minutes <= 240)) errors.targetMinutes = 'Between 1 and 240 minutes';
  if (draft.drills.length === 0) errors.drills = 'Add at least one drill';

  const seen = new Set<string>();
  for (const d of draft.drills) {
    const e: Partial<Record<'name' | 'sets' | 'amount', string>> = {};
    const name = d.name.trim().toLowerCase();
    if (!name) e.name = 'Name the drill';
    else if (seen.has(name)) e.name = 'Each drill needs a different name';
    seen.add(name);
    const sets = wholeNumber(d.sets);
    if (!(sets >= 1 && sets <= 20)) e.sets = '1 to 20';
    const amount = wholeNumber(d.amount);
    if (!(amount >= 1 && amount <= 3600)) e.amount = 'At least 1';
    if (Object.keys(e).length) errors.drill[d.key] = e;
  }
  return errors;
}

export function hasErrors(errors: RoutineErrors): boolean {
  return !!(errors.name || errors.targetMinutes || errors.drills || Object.keys(errors.drill).length);
}

export function toDrillDtos(draft: RoutineDraft): MobilityDrillDto[] {
  return draft.drills.map((d) => ({
    name: d.name.trim(),
    sets: Number(d.sets),
    dose: { unit: d.unit, amount: Number(d.amount) },
    extraSetSide: d.extraSetSide,
    cues: d.cues.trim() || null,
    standingAlternative: d.standingAlternative.trim() || null,
    perSide: d.perSide,
  }));
}

/** Moves the drill at `from` one place up (-1) or down (+1); out of range is a no-op. */
export function moveDrill(drills: DrillDraft[], from: number, by: -1 | 1): DrillDraft[] {
  const to = from + by;
  if (from < 0 || from >= drills.length || to < 0 || to >= drills.length) return drills;
  const next = drills.slice();
  [next[from], next[to]] = [next[to], next[from]];
  return next;
}
