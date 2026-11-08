/**
 * Habit fixtures matching the brief's seed: water 2/6 (min 4), walks 1/2 (min 1),
 * morning weigh-in 94.6 kg, Mobility A and Creatine ticks; streak 5 (best 12).
 */

import { addDays, type LocalDate } from '@/lib/localDate';
import type { HabitDto, HabitHistoryDto, HabitHistoryEntryDto, HabitTodayDto, HabitsTodayDto } from '@/types/habits';

export const WATER_ID = 'habit-water-0000-4000-8000-000000000001';
export const WALKS_ID = 'habit-walks-0000-4000-8000-000000000002';
export const WEIGH_IN_ID = 'habit-weigh-0000-4000-8000-000000000003';
export const MOBILITY_A_HABIT_ID = 'habit-mobil-0000-4000-8000-000000000004';
export const CREATINE_ID = 'habit-creat-0000-4000-8000-000000000005';

export const habitDefinitions: HabitDto[] = [
  { id: WATER_ID, name: 'Water', type: 'Counter', target: 6, minimum: 4, unit: 'bottles', order: 1, archived: false, icon: 'droplets', externalKey: 'terran:water' },
  { id: WALKS_ID, name: 'Walks', type: 'Counter', target: 2, minimum: 1, unit: 'walks', order: 2, archived: false, icon: 'footprints', externalKey: 'terran:walks' },
  { id: WEIGH_IN_ID, name: 'Morning weigh-in', type: 'Numeric', target: 1, minimum: 1, unit: 'kg', order: 3, archived: false, icon: 'scale', externalKey: null },
  { id: MOBILITY_A_HABIT_ID, name: 'Mobility A', type: 'Checkbox', target: 1, minimum: 1, unit: null, order: 4, archived: false, icon: 'stretch-horizontal', externalKey: null },
  { id: CREATINE_ID, name: 'Creatine', type: 'Checkbox', target: 1, minimum: 1, unit: null, order: 5, archived: false, icon: 'pill', externalKey: null },
];

function todayRow(def: HabitDto, value: number): HabitTodayDto {
  return {
    id: def.id,
    name: def.name,
    type: def.type,
    target: def.target,
    minimum: def.minimum,
    unit: def.unit,
    value,
    metMinimum: value >= def.minimum,
    metTarget: value >= def.target,
    order: def.order,
    icon: def.icon,
  };
}

export function habitsTodayFixture(date: LocalDate, values: Partial<Record<string, number>> = {}): HabitsTodayDto {
  const defaults: Record<string, number> = {
    [WATER_ID]: 2,
    [WALKS_ID]: 1,
    [WEIGH_IN_ID]: 94.6,
    [MOBILITY_A_HABIT_ID]: 0,
    [CREATINE_ID]: 1,
  };
  return {
    date,
    habits: habitDefinitions.map((def) => todayRow(def, values[def.id] ?? defaults[def.id] ?? 0)),
    streak: { current: 5, best: 12, lastCountedDay: addDays(date, -1), todayCounted: false, graceUsed: false },
  };
}

export function emptyHabitsToday(date: LocalDate): HabitsTodayDto {
  return { date, habits: [], streak: { current: 0, best: 0, lastCountedDay: null, todayCounted: false, graceUsed: false } };
}

/** Deterministic history: a repeatable pattern so the heatmap shows all three states. */
export function habitHistoryFixture(to: LocalDate, days = 84): HabitHistoryDto {
  const from = addDays(to, -(days - 1));
  const entries: HabitHistoryEntryDto[] = [];
  for (let i = 0; i < days; i++) {
    const date = addDays(from, i);
    for (const def of habitDefinitions) {
      const seed = (i * 7 + def.order * 13) % 10;
      let value: number;
      if (def.type === 'Counter') {
        value = seed < 2 ? 0 : seed < 5 ? def.minimum : def.target;
      } else if (def.type === 'Numeric') {
        value = seed < 1 ? 0 : 95 - i * 0.01;
      } else {
        value = seed < 3 ? 0 : 1;
      }
      if (value > 0) {
        entries.push({ habitId: def.id, date, value, metMinimum: value >= def.minimum, metTarget: value >= def.target });
      }
    }
  }
  return { from, to, habits: habitDefinitions, entries };
}
