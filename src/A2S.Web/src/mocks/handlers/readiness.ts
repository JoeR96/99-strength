/** Story/test handlers for `readinessApi`. */

import { readinessApi } from '@/api/readiness';
import type { LocalDate } from '@/lib/localDate';
import type { ReadinessColour, ReadinessDto } from '@/types/readiness';
import { installHandlers, rejectWith } from './install';

type ReadinessHandlers = Partial<typeof readinessApi>;

/** Mirrors the server's ReadinessAdvice so stories and tests read like the real thing. */
export function readinessFixture(date: LocalDate, colour: ReadinessColour | null, keepFinisher = false): ReadinessDto {
  const red = colour === 'Red';
  const skipped = red && !keepFinisher;
  return {
    date,
    colour,
    keepFinisher: red && keepFinisher,
    finisherSkipped: skipped,
    skipHardCardio: red,
    headline: red ? 'Red recovery: take it easy' : colour === 'Yellow' ? 'Yellow recovery' : null,
    suggestions: red
      ? [
          skipped ? 'Finisher skipped for recovery.' : 'You kept the finisher. Keep it steady.',
          'Skip hard cardio (stair intervals, 4×4) today.',
          'Walks and home routines are fine.',
        ]
      : colour === 'Yellow'
        ? ['Train as planned and go by feel. Nothing in your plan changes.']
        : [],
    updatedAtUtc: colour ? '2026-11-22T07:00:00Z' : null,
  };
}

/** A stateful stub: taps change what the next read returns. */
export function readinessWith(colour: ReadinessColour | null, keepFinisher = false): ReadinessHandlers {
  let current = { colour, keepFinisher };
  return {
    get: async (date) => readinessFixture(date, current.colour, current.keepFinisher),
    set: async (date, next) => {
      current = { colour: next, keepFinisher: false };
      return readinessFixture(date, current.colour, false);
    },
    setFinisher: async (date, keep) => {
      current = { ...current, keepFinisher: keep };
      return readinessFixture(date, current.colour, keep);
    },
  };
}

export const readinessHandlers = {
  default: (): ReadinessHandlers => readinessWith(null),
  green: (): ReadinessHandlers => readinessWith('Green'),
  yellow: (): ReadinessHandlers => readinessWith('Yellow'),
  red: (): ReadinessHandlers => readinessWith('Red'),
  redKeptFinisher: (): ReadinessHandlers => readinessWith('Red', true),
  error: (): ReadinessHandlers => ({ get: rejectWith(500), set: rejectWith(500), setFinisher: rejectWith(500) }),
};

export function installReadinessHandlers(handlers: ReadinessHandlers = readinessHandlers.default()): void {
  installHandlers(readinessApi, handlers);
}
