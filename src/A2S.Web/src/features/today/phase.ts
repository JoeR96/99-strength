/** Which plan phase a date falls in. */

import { compareLocalDates, type LocalDate } from '@/lib/localDate';
import type { PlanPhaseDto } from '@/types/plan';

export function currentPhase(phases: PlanPhaseDto[] | undefined, date: LocalDate): PlanPhaseDto | null {
  if (!phases) return null;
  return phases.find((p) => compareLocalDates(p.from, date) <= 0 && compareLocalDates(date, p.to) <= 0) ?? null;
}
