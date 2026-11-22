/** Story/test handlers for `cardioTracksApi` (the repo stubs API modules directly). */

import { cardioTracksApi } from '@/api/cardioTracks';
import { TrackOutcome, TrackSuggestion } from '@/types/cardioTracks';
import { cardioTemplatesFixture, cardioTracksFixture, detailFor, todayCardioFixture, walkTrack } from '../fixtures/cardioTracks';
import { installHandlers, never, rejectWith } from './install';

type Handlers = Partial<typeof cardioTracksApi>;

const find = (id: string) => structuredClone(cardioTracksFixture.find((t) => t.id === id) ?? walkTrack);

export const cardioTracksHandlers = {
  default: (): Handlers => ({
    list: async () => structuredClone(cardioTracksFixture),
    templates: async () => structuredClone(cardioTemplatesFixture),
    today: async () => structuredClone(todayCardioFixture),
    get: async (id) => structuredClone(detailFor(id)),
    create: async (templateKey) => ({ ...structuredClone(walkTrack), id: `new-${templateKey}`, templateKey }),
    update: async (id, request) => ({ ...find(id), name: request.name, notes: request.notes, stages: request.stages }),
    logSession: async (id) => ({
      outcome: TrackOutcome.SteppedUp,
      reason: 'Completed and felt easy enough: 2 qualifying sessions, volume up',
      qualified: true,
      alreadyRecorded: false,
      track: { ...find(id), sessionsToStepUp: 2, prescription: { ...find(id).prescription, title: '30 min continuous' } },
    }),
    decide: async (id) => ({ ...find(id), suggestion: TrackSuggestion.None, suggestionText: null }),
    undo: async (id) => ({ ...find(id), canUndo: false }),
    pause: async (id) => ({ ...find(id), status: 1 }),
    resume: async (id) => ({ ...find(id), status: 0 }),
    recordBenchmark: async (_id, value) => ({ date: '2026-11-18', value, notes: null, isPersonalBest: true }),
  }),
  empty: (): Handlers => ({
    list: async () => [],
    templates: async () => structuredClone(cardioTemplatesFixture),
    today: async () => ({ tracks: [], highIntensityMinutesThisWeek: 0, warnings: [] }),
  }),
  loading: (): Handlers => ({ list: never, templates: never, today: never }),
  error: (): Handlers => ({ list: rejectWith(500), templates: rejectWith(500), today: rejectWith(500), get: rejectWith(500) }),
};

export function installCardioTracksHandlers(handlers: Handlers = cardioTracksHandlers.default()): void {
  installHandlers(cardioTracksApi, handlers);
}
