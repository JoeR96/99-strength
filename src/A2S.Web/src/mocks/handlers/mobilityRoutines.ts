/** Story/test handlers for `mobilityRoutinesApi` (the repo stubs API modules directly). */

import { mobilityRoutinesApi } from '@/api/mobilityRoutines';
import { answeredSuggestion, followAlongFixture, homeYogaFixture, homeYogaPendingFixture, ladderLevelUpResult, ladderStepUpResult, mobilityRoutinesFixture } from '../fixtures/mobility';
import { installHandlers, never, rejectWith } from './install';

type Handlers = Partial<typeof mobilityRoutinesApi>;

export const mobilityRoutinesHandlers = {
  default: (): Handlers => ({
    list: async () => structuredClone(mobilityRoutinesFixture),
    create: async (request) => ({ id: 'mob-new', ...request }),
    update: async (id, request) => ({ ...structuredClone(mobilityRoutinesFixture.find((r) => r.id === id)!), ...request }),
    remove: async () => {},
    replaceDrills: async (id, drills) => ({ ...structuredClone(mobilityRoutinesFixture.find((r) => r.id === id)!), drills }),
    recordFeedback: async () => ladderStepUpResult(),
  }),
  /** The home yoga routine (levels) and a follow-along video alongside the usual two; feedback steps the ladder up. */
  home: (): Handlers => ({
    ...mobilityRoutinesHandlers.default(),
    list: async () => structuredClone([...mobilityRoutinesFixture, homeYogaFixture, followAlongFixture]),
    replaceDrills: async (id, drills) => ({ ...structuredClone([homeYogaFixture, ...mobilityRoutinesFixture].find((r) => r.id === id)!), drills }),
    recordFeedback: async () => ladderStepUpResult(),
    respondToSuggestion: async (_id, drillName, accept) => answeredSuggestion(ladderLevelUpResult().routine, drillName, accept),
    undoProgression: async () => structuredClone(homeYogaFixture),
  }),
  /** The ladder already has Level 2 suggested, with changes to undo (the Ready screen's Progress section). */
  homePending: (): Handlers => ({
    ...mobilityRoutinesHandlers.home(),
    list: async () => structuredClone([...mobilityRoutinesFixture, homeYogaPendingFixture(), followAlongFixture]),
    respondToSuggestion: async (_id, drillName, accept) => answeredSuggestion(homeYogaPendingFixture(), drillName, accept),
  }),
  /** Like `home`, but the ladder is at the top of Level 1, so the feedback suggests Level 2. */
  homeLevelUp: (): Handlers => ({
    ...mobilityRoutinesHandlers.home(),
    recordFeedback: async () => ladderLevelUpResult(),
  }),
  empty: (): Handlers => ({ list: async () => [] }),
  loading: (): Handlers => ({ list: never }),
  error: (): Handlers => ({ list: rejectWith(500) }),
};

export function installMobilityRoutinesHandlers(handlers: Handlers = mobilityRoutinesHandlers.default()): void {
  installHandlers(mobilityRoutinesApi, handlers);
}
