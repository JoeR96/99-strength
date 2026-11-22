/** Routine editor state and validation, separate from the page so it can be unit tested. */

import { DoseUnit, MobilityRoutineKind, SideBias, type DrillLevelDto, type MobilityDrillDto, type MobilityRoutineDto } from '@/types/mobility';

/** One level of a drill's progression chain, as typed. Range fields are blank when the level has no range. */
export interface LevelDraft {
  key: string;
  name: string;
  description: string;
  sets: string;
  amount: string;
  rangeMin: string;
  rangeMax: string;
  rangeStep: string;
  cues: string;
}

/** Where the drill had got to when the editor opened, so saving without changing the level keeps it. */
export interface DrillProgress {
  currentLevel: number;
  cleanStreak: number;
  suggestedLevel: number | null;
  sets: number;
  amount: number;
}

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
  /** Progression chain, easiest first; empty when the drill doesn't progress. */
  levels: LevelDraft[];
  startingLevel: number;
  progress: DrillProgress | null;
}

export interface RoutineDraft {
  name: string;
  targetMinutes: string;
  kind?: MobilityRoutineKind;
  followAlongUrl?: string;
  drills: DrillDraft[];
}

export type LevelField = 'name' | 'sets' | 'amount' | 'range';

export interface RoutineErrors {
  name?: string;
  targetMinutes?: string;
  followAlongUrl?: string;
  drills?: string;
  /** Per level, by level key. */
  level: Record<string, Partial<Record<LevelField, string>>>;
  /** Per drill, by key. */
  drill: Record<string, Partial<Record<'name' | 'sets' | 'amount', string>>>;
}

let nextKey = 0;
const newKey = () => `drill-${++nextKey}`;
export const newLevelKey = () => `level-${++nextKey}`;

export function emptyDrill(): DrillDraft {
  return {
    key: newKey(),
    name: '',
    sets: '2',
    amount: '30',
    unit: DoseUnit.Seconds,
    perSide: false,
    extraSetSide: SideBias.None,
    cues: '',
    standingAlternative: '',
    levels: [],
    startingLevel: 0,
    progress: null,
  };
}

/** A new level named after its place in the chain, copying the drill's sets and amount. */
export function emptyLevel(drill: DrillDraft): LevelDraft {
  const names = ['Easier', 'Standard', 'Harder'];
  const name = drill.levels.length < names.length && drill.levels.every((l, i) => l.name === names[i]) ? names[drill.levels.length] : `Level ${drill.levels.length + 1}`;
  return { key: newLevelKey(), name, description: '', sets: drill.sets, amount: drill.amount, rangeMin: '', rangeMax: '', rangeStep: '', cues: '' };
}

export function emptyRoutine(): RoutineDraft {
  return { name: '', targetMinutes: '15', kind: MobilityRoutineKind.Stretch, followAlongUrl: '', drills: [emptyDrill()] };
}

function levelDraft(level: DrillLevelDto): LevelDraft {
  return {
    key: newLevelKey(),
    name: level.name,
    description: level.description ?? '',
    sets: String(level.sets),
    amount: String(level.dose.amount),
    rangeMin: level.range ? String(level.range.min) : '',
    rangeMax: level.range ? String(level.range.max) : '',
    rangeStep: level.range ? String(level.range.step) : '',
    cues: level.cues ?? '',
  };
}

export function draftFromRoutine(routine: MobilityRoutineDto): RoutineDraft {
  return {
    name: routine.name,
    targetMinutes: String(routine.targetMinutes),
    kind: routine.kind ?? MobilityRoutineKind.Stretch,
    followAlongUrl: routine.followAlongUrl ?? '',
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
      levels: (d.levels ?? []).map(levelDraft),
      startingLevel: d.currentLevel ?? 0,
      progress: d.levels?.length
        ? { currentLevel: d.currentLevel ?? 0, cleanStreak: d.cleanStreak ?? 0, suggestedLevel: d.suggestedLevel ?? null, sets: d.sets, amount: d.dose.amount }
        : null,
    })),
  };
}

const isWebLink = (value: string) => {
  try {
    const url = new URL(value.trim());
    return url.protocol === 'https:' || url.protocol === 'http:';
  } catch {
    return false;
  }
};

const wholeNumber = (value: string) => /^\d+$/.test(value.trim()) ? Number(value) : NaN;

export function validateRoutine(draft: RoutineDraft): RoutineErrors {
  const errors: RoutineErrors = { drill: {}, level: {} };
  if (!draft.name.trim()) errors.name = 'Give the routine a name';
  const minutes = wholeNumber(draft.targetMinutes);
  if (!(minutes >= 1 && minutes <= 240)) errors.targetMinutes = 'Between 1 and 240 minutes';
  const followAlong = draft.kind === MobilityRoutineKind.FollowAlong;
  if (followAlong) {
    if (!isWebLink(draft.followAlongUrl ?? '')) errors.followAlongUrl = 'Paste the video link (starting https://)';
    return errors;
  }
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

    const levelNames = new Set<string>();
    for (const l of d.levels) {
      const le: Partial<Record<LevelField, string>> = {};
      const levelName = l.name.trim().toLowerCase();
      if (!levelName) le.name = 'Name the level';
      else if (levelNames.has(levelName)) le.name = 'Each level needs a different name';
      levelNames.add(levelName);
      const levelSets = wholeNumber(l.sets);
      if (!(levelSets >= 1 && levelSets <= 20)) le.sets = '1 to 20';
      const levelAmount = wholeNumber(l.amount);
      if (!(levelAmount >= 1 && levelAmount <= 3600)) le.amount = 'At least 1';
      const anyRange = [l.rangeMin, l.rangeMax, l.rangeStep].some((v) => v.trim());
      if (anyRange) {
        const [min, max, step] = [wholeNumber(l.rangeMin), wholeNumber(l.rangeMax), wholeNumber(l.rangeStep)];
        if (!(min >= 1 && max >= min && step >= 1)) le.range = 'From, to and step: whole numbers, "to" at least "from"';
      }
      if (Object.keys(le).length) errors.level[l.key] = le;
    }
  }
  return errors;
}

export function hasErrors(errors: RoutineErrors): boolean {
  return !!(errors.name || errors.targetMinutes || errors.followAlongUrl || errors.drills || Object.keys(errors.drill).length || Object.keys(errors.level ?? {}).length);
}

function levelDto(l: LevelDraft, unit: DoseUnit): DrillLevelDto {
  const hasRange = [l.rangeMin, l.rangeMax, l.rangeStep].every((v) => v.trim());
  return {
    name: l.name.trim(),
    description: l.description.trim() || null,
    sets: Number(l.sets),
    dose: { unit, amount: Number(l.amount) },
    range: hasRange ? { min: Number(l.rangeMin), max: Number(l.rangeMax), step: Number(l.rangeStep) } : null,
    cues: l.cues.trim() || null,
  };
}

/**
 * The drills to save. A drill with levels takes its sets and dose from the starting level (the bottom of its
 * range); when the starting level is where the drill already was, its progress (dose, streak, suggestion) is kept.
 */
export function toDrillDtos(draft: RoutineDraft): MobilityDrillDto[] {
  if (draft.kind === MobilityRoutineKind.FollowAlong) return [];
  return draft.drills.map((d) => {
    const base: MobilityDrillDto = {
      name: d.name.trim(),
      sets: Number(d.sets),
      dose: { unit: d.unit, amount: Number(d.amount) },
      extraSetSide: d.extraSetSide,
      cues: d.cues.trim() || null,
      standingAlternative: d.standingAlternative.trim() || null,
      perSide: d.perSide,
    };
    if (d.levels.length === 0) return base;

    const levels = d.levels.map((l) => levelDto(l, d.unit));
    const at = Math.min(Math.max(d.startingLevel, 0), levels.length - 1);
    const level = levels[at];
    const kept = d.progress && d.progress.currentLevel === at && d.progress.currentLevel < levels.length;
    const suggested = kept && d.progress!.suggestedLevel !== null && d.progress!.suggestedLevel < levels.length ? d.progress!.suggestedLevel : null;
    return {
      ...base,
      sets: level.sets,
      dose: { unit: d.unit, amount: kept ? d.progress!.amount : (level.range?.min ?? level.dose.amount) },
      levels,
      currentLevel: at,
      cleanStreak: kept ? d.progress!.cleanStreak : 0,
      suggestedLevel: suggested,
    };
  });
}

/** Moves the item at `from` one place up (-1) or down (+1); out of range is a no-op. */
export function moveItem<T>(items: T[], from: number, by: -1 | 1): T[] {
  const to = from + by;
  if (from < 0 || from >= items.length || to < 0 || to >= items.length) return items;
  const next = items.slice();
  [next[from], next[to]] = [next[to], next[from]];
  return next;
}

/** Moves the drill at `from` one place up (-1) or down (+1); out of range is a no-op. */
export function moveDrill(drills: DrillDraft[], from: number, by: -1 | 1): DrillDraft[] {
  const to = from + by;
  if (from < 0 || from >= drills.length || to < 0 || to >= drills.length) return drills;
  const next = drills.slice();
  [next[from], next[to]] = [next[to], next[from]];
  return next;
}
