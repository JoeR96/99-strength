import { describe, it, expect } from 'vitest';
import {
  SessionKind,
  SessionStatus,
  type PlanDayDto,
  type TodaySessionDto,
  type UpcomingLiftDto,
} from '@/types/plan';
import { dayHeading, isAllDoneOrRest, liftSummary, parseDateParam, pickUpNext } from './upNext';

function session(kind: SessionKind, status: SessionStatus, label = 'S'): TodaySessionDto {
  return {
    kind,
    label,
    status,
    liftDay: kind === SessionKind.Lift ? 1 : null,
    cardioProtocolId: null,
    mobilityRoutineId: null,
    completionId: null,
    hevyRoutineId: null,
  };
}

const rest = (date: string): PlanDayDto => ({
  date,
  phase: null,
  sessions: [session(SessionKind.Rest, SessionStatus.Rest)],
});

describe('pickUpNext', () => {
  it('skips rest days and empty days and returns the first day with a session to do', () => {
    const days: PlanDayDto[] = [
      rest('2026-11-23'),
      { date: '2026-11-24', phase: null, sessions: [] },
      {
        date: '2026-11-25',
        phase: null,
        sessions: [
          session(SessionKind.Rest, SessionStatus.Rest),
          session(SessionKind.Lift, SessionStatus.Planned, 'Lower A'),
        ],
      },
      {
        date: '2026-11-26',
        phase: null,
        sessions: [session(SessionKind.Cardio, SessionStatus.Planned)],
      },
    ];

    const next = pickUpNext(days);

    expect(next?.date).toBe('2026-11-25');
    expect(next?.sessions.map((s) => s.label)).toEqual(['Lower A']);
  });

  it('skips paused and already done sessions', () => {
    const days: PlanDayDto[] = [
      {
        date: '2026-11-23',
        phase: null,
        sessions: [session(SessionKind.Lift, SessionStatus.Paused)],
      },
      {
        date: '2026-11-24',
        phase: null,
        sessions: [session(SessionKind.Lift, SessionStatus.Done)],
      },
      {
        date: '2026-11-25',
        phase: null,
        sessions: [session(SessionKind.Mobility, SessionStatus.Planned, 'Mobility B')],
      },
    ];

    expect(pickUpNext(days)?.date).toBe('2026-11-25');
  });

  it('returns null with no plan or nothing in the window', () => {
    expect(pickUpNext(null)).toBeNull();
    expect(pickUpNext(undefined)).toBeNull();
    expect(
      pickUpNext(
        Array.from({ length: 14 }, (_, i) => rest(`2026-12-${String(i + 1).padStart(2, '0')}`))
      )
    ).toBeNull();
  });
});

describe('isAllDoneOrRest', () => {
  it('is true for a rest day, an empty day or when everything is done', () => {
    expect(isAllDoneOrRest([session(SessionKind.Rest, SessionStatus.Rest)])).toBe(true);
    expect(isAllDoneOrRest([])).toBe(true);
    expect(isAllDoneOrRest([session(SessionKind.Lift, SessionStatus.Done)])).toBe(true);
    expect(
      isAllDoneOrRest([
        session(SessionKind.Lift, SessionStatus.Done),
        session(SessionKind.Cardio, SessionStatus.Planned),
      ])
    ).toBe(false);
  });
});

describe('dayHeading', () => {
  it('names tomorrow and yesterday and otherwise gives the date alone', () => {
    expect(dayHeading('2026-11-24', '2026-11-23')).toBe('Tomorrow · Tue 24 Nov');
    expect(dayHeading('2026-11-22', '2026-11-23')).toBe('Yesterday · Sun 22 Nov');
    expect(dayHeading('2026-11-23', '2026-11-23')).toBe('Today · Mon 23 Nov');
    expect(dayHeading('2026-11-26', '2026-11-23')).toBe('Thu 26 Nov');
  });
});

describe('parseDateParam', () => {
  it('accepts a real yyyy-MM-dd date and rejects anything else', () => {
    expect(parseDateParam('2026-11-24')).toBe('2026-11-24');
    expect(parseDateParam(null)).toBeNull();
    expect(parseDateParam('tomorrow')).toBeNull();
    expect(parseDateParam('2026-02-31')).toBeNull();
  });
});

const upperALift: UpcomingLiftDto = {
  liftDay: 1,
  label: 'Upper A',
  weekNumber: 3,
  isDeloadWeek: false,
  exercises: [
    {
      name: 'Seal Row (Barbell)',
      category: 'Accessory',
      sets: [{ weightKg: 40, reps: 10, isAmrap: false }],
    },
    {
      name: 'Bench Press (Barbell)',
      category: 'MainLift',
      sets: [1, 2, 3, 4, 5].map((n) => ({ weightKg: 75, reps: 5, isAmrap: n === 5 })),
    },
  ],
  finisher: null,
};

describe('liftSummary', () => {
  it('gives the exercise count and the main lift with its top set', () => {
    expect(liftSummary(upperALift)).toBe('2 exercises · Bench Press (Barbell) 5×5 @ 75 kg');
  });

  it('says nothing for a plan with no sets', () => {
    expect(liftSummary({ ...upperALift, exercises: [] })).toBeNull();
  });
});
