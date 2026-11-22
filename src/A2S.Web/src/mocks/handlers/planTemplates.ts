/** Story/test handlers for `planTemplatesApi` (the Plan templates page and its apply dialog). */

import { planTemplatesApi } from '@/api/planTemplates';
import { planTemplatesFixture } from '@/mocks/fixtures/planTemplates';
import { startedResult } from './suggestedPlans';
import { installHandlers, never, rejectWith } from './install';

type PlanTemplatesHandlers = Partial<typeof planTemplatesApi>;

/** Shifts a preview to a requested start date (only the start date; enough for stories). */
const byKey = async (key: string, startDate?: string) => {
  const found = planTemplatesFixture.find((t) => t.key === key);
  if (!found) return Promise.reject(Object.assign(new Error('Not found'), { response: { status: 404, data: { title: 'Not found' } } }));
  return startDate ? { ...found, startDate } : found;
};

/** A 409 "confirm to replace" until the request says confirmReplace: true. */
export const replaceConflict = () =>
  Promise.reject(
    Object.assign(new Error('Request failed with status code 409'), {
      response: {
        status: 409,
        data: {
          status: 409,
          title: 'Conflict',
          detail: 'You already have an active plan (Lumbridge Lard Shed). Confirm to replace it: resend with confirmReplace.',
        },
      },
    })
  );

export const planTemplatesHandlers = {
  default: (): PlanTemplatesHandlers => ({
    list: async (startDate) => planTemplatesFixture.map((t) => (startDate ? { ...t, startDate } : t)),
    get: byKey,
    apply: async () => startedResult,
  }),
  /** Another plan is active: the first apply is refused with 409, the confirmed one goes through. */
  needsReplace: (): PlanTemplatesHandlers => ({
    list: async () => planTemplatesFixture,
    get: byKey,
    apply: async (_key, request) => (request.confirmReplace ? startedResult : replaceConflict()),
  }),
  loading: (): PlanTemplatesHandlers => ({ list: never, get: never, apply: never }),
  error: (): PlanTemplatesHandlers => ({ list: rejectWith(500), get: rejectWith(500), apply: rejectWith(500) }),
};

export function installPlanTemplatesHandlers(handlers: PlanTemplatesHandlers = planTemplatesHandlers.default()): void {
  installHandlers(planTemplatesApi, handlers);
}
