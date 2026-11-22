/** Mobility page grouping: routines by kind, in page order. */

import { MobilityRoutineKind, type MobilityRoutineDto } from '@/types/mobility';

/** Section order and headings on the page. */
export const ROUTINE_GROUPS: { kind: MobilityRoutineKind; heading: string }[] = [
  { kind: MobilityRoutineKind.WarmUp, heading: 'Warm-ups' },
  { kind: MobilityRoutineKind.Corrective, heading: 'Corrective' },
  { kind: MobilityRoutineKind.Stretch, heading: 'Stretch' },
  { kind: MobilityRoutineKind.YogaFlow, heading: 'Yoga flow' },
  { kind: MobilityRoutineKind.FollowAlong, heading: 'Follow-along' },
];

/** Routines by kind, in page order, leaving out empty groups. A routine with no kind is a stretch. */
export function groupRoutines(routines: MobilityRoutineDto[]): { kind: MobilityRoutineKind; heading: string; routines: MobilityRoutineDto[] }[] {
  return ROUTINE_GROUPS.map((g) => ({ ...g, routines: routines.filter((r) => (r.kind ?? MobilityRoutineKind.Stretch) === g.kind) })).filter(
    (g) => g.routines.length > 0
  );
}
