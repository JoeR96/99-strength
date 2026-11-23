/** Wording for Hevy-based starting-number suggestions, shared by every place a TM or starting weight is set. */

import type { StartingNumberItem, StartingNumbersSuggestions, StartingNumberSuggestion } from '@/api/workouts';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function kg(value: number): string {
  return `${Number(value.toFixed(2))} kg`;
}

/** `2026-11-14` → `14 Nov` (the date is already the Europe/London day; no time-zone shifting). */
export function shortDate(isoDate: string): string {
  const [, month, day] = isoDate.slice(0, 10).split('-').map(Number);
  return `${day} ${MONTHS[(month ?? 1) - 1]}`;
}

/** "Suggested 77.5 kg — from 75 kg × 5 on 14 Nov (est. 1RM 87.5 kg)", plus "older history — estimated" when stale. */
export function suggestionLine(s: StartingNumberSuggestion): string {
  const base = `Suggested ${kg(s.valueKg)} — from ${kg(s.setWeightKg)} × ${s.setReps} on ${shortDate(s.setDate)} (est. 1RM ${kg(s.estimatedOneRepMaxKg)})`;
  return s.stale ? `${base} · older history — estimated` : base;
}

/** Why there is no suggestion, or null when Hevy answered fine. */
export function hevyFallbackNote(data: StartingNumbersSuggestions | undefined, failed: boolean): string | null {
  if (failed || data?.hevyStatus === 'unavailable') return 'Hevy unavailable — enter it yourself.';
  if (data?.hevyStatus === 'noKey') return 'Connect Hevy in Settings to get a suggestion.';
  return null;
}

export function itemFor(data: StartingNumbersSuggestions | undefined, exerciseId: string): StartingNumberItem | undefined {
  return data?.items.find((i) => i.exerciseId === exerciseId);
}
