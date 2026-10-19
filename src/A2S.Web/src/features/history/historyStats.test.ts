import { describe, expect, it } from 'vitest';
import {
  estimateOneRepMax,
  oneRepMaxSeries,
  programTotals,
  sessionTotals,
  topSet,
} from './historyStats';
import type {
  CompletedSetDto,
  ExerciseHistoryDto,
  WeeklyPerformanceDto,
  WorkoutActivityDto,
  WorkoutHistoryDto,
} from './historyTypes';

const set = (setNumber: number, weight: number, actualReps: number, wasAmrap = false): CompletedSetDto => ({
  setNumber,
  weight,
  weightUnit: 'Kilograms',
  actualReps,
  wasAmrap,
});

const week = (weekNumber: number, sets: CompletedSetDto[], isDeloadWeek = false): WeeklyPerformanceDto => ({
  weekNumber,
  blockNumber: Math.ceil(weekNumber / 7),
  completedAt: null,
  isDeloadWeek,
  totalVolume: 0,
  averageWeight: 0,
  totalReps: 0,
  setsCompleted: sets.length,
  amrapReps: sets.find((s) => s.wasAmrap)?.actualReps ?? null,
  sets,
});

const squat: ExerciseHistoryDto = {
  exerciseId: 'squat',
  name: 'Squat (Barbell)',
  progressionType: 'Linear',
  assignedDay: 1,
  category: 'MainLift',
  equipment: 'Barbell',
  currentWeight: 0,
  weightUnit: 'Kilograms',
  currentSets: 4,
  targetSets: 4,
  trainingMax: 140,
  weeklyHistory: [
    week(1, [set(1, 100, 8), set(2, 100, 8), set(3, 100, 12, true)]),
    week(2, [set(1, 105, 7), set(2, 105, 10, true)]),
    // Deload: lighter, no AMRAP set
    week(7, [set(1, 70, 5), set(2, 70, 5)], true),
  ],
};

const activity: WorkoutActivityDto = {
  day: 'Day1',
  dayNumber: 1,
  weekNumber: 2,
  blockNumber: 1,
  completedAt: '2026-09-07T07:30:00Z',
  isDeloadWeek: false,
  performances: [
    { exerciseId: 'squat', completedAt: '2026-09-07T07:30:00Z', completedSets: [set(1, 105, 7), set(2, 105, 10, true)] },
    { exerciseId: 'curl', completedAt: '2026-09-07T07:30:00Z', completedSets: [set(1, 14, 12), set(2, 14, 11)] },
  ],
};

describe('estimateOneRepMax', () => {
  it('uses the Epley formula', () => {
    expect(estimateOneRepMax(100, 12)).toBeCloseTo(140);
    expect(estimateOneRepMax(105, 10)).toBeCloseTo(140);
  });

  it('treats a single rep as the weight itself, and no reps as nothing', () => {
    expect(estimateOneRepMax(150, 1)).toBe(150);
    expect(estimateOneRepMax(150, 0)).toBe(0);
  });
});

describe('sessionTotals', () => {
  it('counts exercises and sets, and sums weight × reps', () => {
    expect(sessionTotals(activity)).toEqual({
      exercises: 2,
      sets: 4,
      volume: 105 * 7 + 105 * 10 + 14 * 12 + 14 * 11,
    });
  });
});

describe('topSet', () => {
  it('picks the set with the highest estimated 1RM', () => {
    expect(topSet(activity)).toMatchObject({ exerciseId: 'squat', weight: 105, reps: 10, wasAmrap: true });
  });

  it('is null for a session with no sets', () => {
    expect(topSet({ ...activity, performances: [] })).toBeNull();
  });
});

describe('oneRepMaxSeries', () => {
  it('gives one point per week from that week\'s AMRAP set', () => {
    const series = oneRepMaxSeries(squat);
    expect(series.map((p) => p.weekNumber)).toEqual([1, 2]);
    expect(series[0]).toMatchObject({ label: 'W1', weight: 100, reps: 12, blockNumber: 1 });
    expect(series[0].estimate).toBeCloseTo(140);
  });

  it('skips weeks without an AMRAP set, so a deload does not show as a drop', () => {
    expect(oneRepMaxSeries(squat).some((p) => p.weekNumber === 7)).toBe(false);
  });
});

describe('programTotals', () => {
  it('adds up the logged sessions against the planned ones', () => {
    const history = {
      totalWeeks: 21,
      daysPerWeek: 4,
      completedActivities: [activity, activity],
    } as WorkoutHistoryDto;
    expect(programTotals(history)).toEqual({
      sessions: 2,
      planned: 84,
      sets: 8,
      volume: 2 * (105 * 7 + 105 * 10 + 14 * 12 + 14 * 11),
    });
  });
});
