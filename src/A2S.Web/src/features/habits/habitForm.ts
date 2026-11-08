/** Habit edit form state and validation, separate from the sheet so it can be unit tested. */

import type { CreateHabitRequest, HabitDto, HabitType, UpdateHabitRequest } from '@/types/habits';

export interface HabitFormValues {
  name: string;
  type: HabitType;
  target: string;
  minimum: string;
  unit: string;
  icon: string | null;
}

export type HabitFormErrors = Partial<Record<'name' | 'target' | 'minimum', string>>;

export const EMPTY_HABIT_FORM: HabitFormValues = {
  name: '',
  type: 'Counter',
  target: '1',
  minimum: '1',
  unit: '',
  icon: null,
};

export function formFromHabit(habit: HabitDto): HabitFormValues {
  return {
    name: habit.name,
    type: habit.type,
    target: String(habit.target),
    minimum: String(habit.minimum),
    unit: habit.unit ?? '',
    icon: habit.icon,
  };
}

function parseNumber(raw: string): number | null {
  const trimmed = raw.trim().replace(',', '.');
  if (trimmed === '') return null;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : null;
}

export interface HabitFormResult {
  errors: HabitFormErrors;
  /** Present only when there are no errors. */
  request?: Omit<CreateHabitRequest, 'order'>;
}

export function validateHabitForm(values: HabitFormValues): HabitFormResult {
  const errors: HabitFormErrors = {};
  const name = values.name.trim();
  if (name === '') errors.name = 'Give it a name';
  else if (name.length > 60) errors.name = 'Keep the name under 60 characters';

  let target: number | null = 1;
  let minimum: number | null = 1;
  if (values.type !== 'Checkbox') {
    target = parseNumber(values.target);
    minimum = parseNumber(values.minimum);
    if (target === null || target <= 0) errors.target = 'Target must be more than 0';
    if (minimum === null || minimum < 0) errors.minimum = 'Minimum must be 0 or more';
    if (target !== null && minimum !== null && !errors.target && !errors.minimum && minimum > target) {
      errors.minimum = "Minimum can't be above the target";
    }
  }

  if (Object.keys(errors).length > 0) return { errors };
  const unit = values.unit.trim();
  return {
    errors,
    request: {
      name,
      type: values.type,
      target: target!,
      minimum: minimum!,
      unit: unit === '' ? null : unit,
      icon: values.icon,
    },
  };
}

export function toUpdateRequest(request: Omit<CreateHabitRequest, 'order'>, order: number): UpdateHabitRequest {
  return { name: request.name, target: request.target, minimum: request.minimum, unit: request.unit, order, icon: request.icon };
}
