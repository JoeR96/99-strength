/** Pure helpers for applying a plan template: Monday snapping and the request from the form. */

import type { ApplyPlanTemplateRequest } from '@/api/planTemplates';
import type { SuggestedPlanInput } from '@/api/suggestedPlans';

const pad = (n: number) => String(n).padStart(2, '0');

function parse(date: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date);
  if (!m) return null;
  return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
}

export function toIsoDate(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** The Monday on or after a yyyy-MM-dd date (a Monday is itself), as the server snaps it. Null when unreadable. */
export function snapToMonday(date: string): string | null {
  const d = parse(date);
  if (!d) return null;
  d.setDate(d.getDate() + ((1 - d.getDay() + 7) % 7));
  return toIsoDate(d);
}

/** "Monday 23 Nov 2026". */
export function formatLongDate(date: string): string {
  const d = parse(date);
  return d ? d.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'short', year: 'numeric' }) : date;
}

/** "23 Nov". */
export function formatShortDate(date: string): string {
  const d = parse(date);
  return d ? d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' }) : date;
}

export const START_WEIGHT_KEY = 'startWeightKg';

function parseNumber(value: string): number | null {
  const trimmed = value.trim().replace(',', '.');
  if (trimmed === '') return null;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : NaN;
}

export interface ApplyForm {
  request: ApplyPlanTemplateRequest | null;
  errors: Record<string, string>;
}

/**
 * Builds the apply body from the inputs' text values. Optional inputs may be blank (a blank training
 * max is sent as null: pending). Required blanks and out-of-range numbers are errors.
 */
export function buildApplyRequest(
  inputs: SuggestedPlanInput[],
  values: Record<string, string>,
  startDate: string,
  confirmReplace = false
): ApplyForm {
  const errors: Record<string, string> = {};
  const trainingMaxes: Record<string, number | null> = {};
  let startWeightKg: number | undefined;
  for (const input of inputs) {
    const n = parseNumber(values[input.key] ?? '');
    if (n === null) {
      if (!input.optional && input.default === null) errors[input.key] = `Enter ${input.label} (${input.unit})`;
      if (input.key !== START_WEIGHT_KEY) trainingMaxes[input.key] = null;
      else if (input.default !== null) startWeightKg = input.default;
      continue;
    }
    if (Number.isNaN(n) || n < input.minimum || n > input.maximum) {
      errors[input.key] = `Between ${input.minimum} and ${input.maximum} ${input.unit}`;
      continue;
    }
    if (input.key === START_WEIGHT_KEY) startWeightKg = n;
    else trainingMaxes[input.key] = n;
  }
  if (Object.keys(errors).length > 0) return { request: null, errors };
  return {
    request: { startDate, trainingMaxes, ...(startWeightKg !== undefined ? { startWeightKg } : {}), confirmReplace },
    errors,
  };
}
