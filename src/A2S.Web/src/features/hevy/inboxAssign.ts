/**
 * What the Hevy inbox sends when a pulled workout is placed by hand, and how an inbox item is
 * summarised on its card. Kept apart from the components so the choices can be tested directly.
 */

import { SessionKind, type PlanDayNumber, type TodaySessionDto, type TrainingPlanDto } from '@/types/plan';
import type { AssignHevyWorkoutRequest, HevyInboxItemDto } from '@/types/hevySync';

/** Completes this scheduled session from the workout, exactly as a matched pull would have. */
export function assignRequestFor(item: HevyInboxItemDto, session: TodaySessionDto): AssignHevyWorkoutRequest {
  return { date: item.localDate, kind: session.kind, liftDay: session.liftDay, extra: false };
}

/**
 * The kind an extra session is recorded as. The inbox item carries no exercise types, so this
 * reads the server's reason for not matching it ("cardio workout with no cardio session that
 * day", "mobility-only workout…"); anything else is a lift.
 */
export function extraKindFor(item: HevyInboxItemDto): SessionKind {
  const reason = (item.reason ?? '').toLowerCase();
  if (reason.includes('cardio')) return SessionKind.Cardio;
  if (reason.includes('mobility')) return SessionKind.Mobility;
  return SessionKind.Lift;
}

/**
 * Records the workout as an additional session on its own date: it counts towards the week but
 * leaves A2S2 progression alone. The API wants a lift day for a lift, so the one whose label is
 * in the workout's title is used ("Lower A"), else the plan's first lift day.
 */
export function extraRequestFor(item: HevyInboxItemDto, plan: TrainingPlanDto | null | undefined): AssignHevyWorkoutRequest {
  const kind = extraKindFor(item);
  if (kind !== SessionKind.Lift) {
    return { date: item.localDate, kind, liftDay: null, extra: true };
  }

  const lifts = (plan?.schedule ?? []).filter((s) => s.kind === SessionKind.Lift && s.liftDay !== null);
  const title = item.title.toLowerCase();
  const named = lifts.find((s) => title.includes(s.label.toLowerCase()));
  const first = lifts.map((s) => s.liftDay as PlanDayNumber).sort((a, b) => a - b)[0];
  return { date: item.localDate, kind, liftDay: named?.liftDay ?? first ?? 1, extra: true };
}

const SUMMARY_NAMES = 3;

/** "4 exercises, 16 sets" */
export function exerciseCounts(item: HevyInboxItemDto): string {
  const exercises = item.exercises.length;
  const sets = item.exercises.reduce((sum, e) => sum + e.sets, 0);
  if (exercises === 0) return 'No exercises recorded';
  return `${exercises} ${exercises === 1 ? 'exercise' : 'exercises'}, ${sets} ${sets === 1 ? 'set' : 'sets'}`;
}

/** "Squat (Barbell), Romanian Deadlift (Barbell), Leg Press + 1 more" */
export function exerciseNames(item: HevyInboxItemDto): string {
  const names = item.exercises.slice(0, SUMMARY_NAMES).map((e) => e.title);
  const rest = item.exercises.length - names.length;
  return rest > 0 ? `${names.join(', ')} + ${rest} more` : names.join(', ');
}

/** Whole minutes between start and end, or null when the times are unusable. */
export function workoutMinutes(item: HevyInboxItemDto): number | null {
  const ms = Date.parse(item.endUtc) - Date.parse(item.startUtc);
  return Number.isFinite(ms) && ms > 0 ? Math.round(ms / 60000) : null;
}
