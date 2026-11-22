import type { LocalDate } from '@/lib/localDate';

/** The WHOOP recovery colour, as the server sends it. */
export type ReadinessColour = 'Green' | 'Yellow' | 'Red';

/** A day's readiness and what Today suggests. Suggestions only: no plan or track maths change. */
export interface ReadinessDto {
  date: LocalDate;
  colour: ReadinessColour | null;
  keepFinisher: boolean;
  finisherSkipped: boolean;
  skipHardCardio: boolean;
  headline: string | null;
  suggestions: string[];
  updatedAtUtc: string | null;
}
