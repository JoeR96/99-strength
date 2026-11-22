/** Story/test handlers for `mobilityRoutinesApi` (the repo stubs API modules directly). */

import { mobilityRoutinesApi } from '@/api/mobilityRoutines';
import { mobilityRoutinesFixture } from '../fixtures/mobility';
import { installHandlers, never, rejectWith } from './install';

type Handlers = Partial<typeof mobilityRoutinesApi>;

export const mobilityRoutinesHandlers = {
  default: (): Handlers => ({
    list: async () => structuredClone(mobilityRoutinesFixture),
    create: async (request) => ({ id: 'mob-new', ...request }),
    replaceDrills: async (id, drills) => ({ ...structuredClone(mobilityRoutinesFixture.find((r) => r.id === id)!), drills }),
  }),
  empty: (): Handlers => ({ list: async () => [] }),
  loading: (): Handlers => ({ list: never }),
  error: (): Handlers => ({ list: rejectWith(500) }),
};

export function installMobilityRoutinesHandlers(handlers: Handlers = mobilityRoutinesHandlers.default()): void {
  installHandlers(mobilityRoutinesApi, handlers);
}
