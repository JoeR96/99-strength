import { describe, it, expect } from 'vitest';
import { inboxTwoItems } from '@/mocks/fixtures/hevyInbox';
import { dayFromSchedule, planFixture } from '@/mocks/fixtures/plan';
import { SessionKind } from '@/types/plan';
import type { HevyInboxItemDto } from '@/types/hevySync';
import {
  assignRequestFor,
  exerciseCounts,
  exerciseNames,
  extraKindFor,
  extraRequestFor,
  workoutMinutes,
} from './inboxAssign';

// The week of Monday 9 November 2026: Sunday 15 (rest) and Wednesday 11 (4×4 + Mobility B).
const [sundayLower, wednesdayPush] = inboxTwoItems('2026-11-09');

const item = (overrides: Partial<HevyInboxItemDto>): HevyInboxItemDto => ({ ...wednesdayPush, ...overrides });

describe('inboxAssign', () => {
  it('assigning to a session sends its date, kind and lift day, and is not an extra', () => {
    const tuesday = dayFromSchedule('2026-11-10').sessions[0];
    expect(assignRequestFor(item({ localDate: '2026-11-10' }), tuesday)).toEqual({
      date: '2026-11-10',
      kind: SessionKind.Lift,
      liftDay: 2,
      extra: false,
    });

    const [fourByFour] = dayFromSchedule('2026-11-11').sessions;
    expect(assignRequestFor(wednesdayPush, fourByFour)).toEqual({
      date: '2026-11-11',
      kind: SessionKind.Cardio,
      liftDay: null,
      extra: false,
    });
  });

  it("reads the extra's kind from why the server could not match it", () => {
    expect(extraKindFor(item({ reason: 'cardio workout with no cardio session that day' }))).toBe(SessionKind.Cardio);
    expect(extraKindFor(item({ reason: 'mobility-only workout with no mobility session that day' }))).toBe(SessionKind.Mobility);
    expect(extraKindFor(item({ reason: 'rest day' }))).toBe(SessionKind.Lift);
    expect(extraKindFor(item({ reason: null }))).toBe(SessionKind.Lift);
  });

  it('an extra lift takes the lift day named in the title, else the first lift day of the plan', () => {
    expect(sundayLower.localDate).toBe('2026-11-15');
    expect(extraRequestFor(sundayLower, planFixture)).toEqual({ date: '2026-11-15', kind: SessionKind.Lift, liftDay: 2, extra: true });
    expect(extraRequestFor(wednesdayPush, planFixture)).toEqual({ date: '2026-11-11', kind: SessionKind.Lift, liftDay: 1, extra: true });
    expect(extraRequestFor(wednesdayPush, null).liftDay).toBe(1);
  });

  it('an extra cardio or mobility session has no lift day', () => {
    const run = item({ title: 'Evening run', reason: 'cardio workout with no cardio session that day' });
    expect(extraRequestFor(run, planFixture)).toEqual({ date: '2026-11-11', kind: SessionKind.Cardio, liftDay: null, extra: true });
  });

  it('summarises the exercises', () => {
    expect(exerciseCounts(sundayLower)).toBe('4 exercises, 16 sets');
    expect(exerciseNames(sundayLower)).toBe('Squat (Barbell), Romanian Deadlift (Barbell), Leg Press + 1 more');
    expect(exerciseNames(wednesdayPush)).toBe('Bench Press (Barbell), Overhead Press (Barbell)');
    expect(exerciseCounts(item({ exercises: [{ title: 'Plank', templateId: 'X', sets: 1 }] }))).toBe('1 exercise, 1 set');
    expect(exerciseCounts(item({ exercises: [] }))).toBe('No exercises recorded');
  });

  it('gives the length of the workout in minutes when the times make sense', () => {
    expect(workoutMinutes(sundayLower)).toBe(69);
    expect(workoutMinutes(item({ endUtc: 'not a date' }))).toBeNull();
    expect(workoutMinutes(item({ endUtc: wednesdayPush.startUtc }))).toBeNull();
  });
});
