/** Shared bits of gym mode's log fields: the Easy / Solid / Brutal buttons and number parsing. */

import type { Effort } from '@/types/gym';

export const EFFORTS: { value: Effort; hint: string }[] = [
  { value: 'Easy', hint: 'could chat' },
  { value: 'Solid', hint: 'hard but steady' },
  { value: 'Brutal', hint: 'all out' },
];

/** A non-negative number, or null for empty or invalid text. */
export function numberOrNull(text: string): number | null {
  if (text.trim() === '') return null;
  const value = Number(text);
  return Number.isFinite(value) && value >= 0 ? value : null;
}
