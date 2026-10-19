/**
 * Simulator fixtures: GET /workouts/:id/simulate (projection time series) and the
 * NDJSON body of GET /workouts/:id/simulate/stream (a persistent day-by-day run).
 * Both continue the program fixture from the current session.
 */
import { getWeekParameters } from '@/utils/weekParameters';
import {
  CURRENT_DAY,
  CURRENT_WEEK,
  DAYS_PER_WEEK,
  PROGRAM_NAME,
  TOTAL_WEEKS,
  exerciseStates,
} from './program';
import { createRandom } from './random';

const round1 = (n: number) => Math.round(n * 10) / 10;

function dayAt(sessionIndex: number) {
  const offset = CURRENT_DAY - 1 + sessionIndex; // 0-based from W1D1 of current week
  return {
    week: CURRENT_WEEK + Math.floor(offset / DAYS_PER_WEEK),
    day: (offset % DAYS_PER_WEEK) + 1,
  };
}

const remainingSessions = (TOTAL_WEEKS - CURRENT_WEEK + 1) * DAYS_PER_WEEK - (CURRENT_DAY - 1);

export function simulationResult(sessions: number) {
  const count = Math.max(1, Math.min(sessions, remainingSessions));
  const rand = createRandom(2026);
  const state = exerciseStates.map((s) => ({
    spec: s.spec,
    tm: s.tm,
    weight: s.weight,
    sets: s.sets,
  }));
  const series = state.map((s) => ({
    exerciseId: s.spec.id,
    exerciseName: s.spec.name,
    progressionType: s.spec.spec.type,
    dataPoints: [] as {
      session: number;
      week: number;
      block: number;
      trainingMax: number | null;
      trainingMaxUnit: string | null;
      currentWeight: number | null;
      currentWeightUnit: string | null;
      summary: { type: string; details: Record<string, string> };
    }[],
  }));

  for (let i = 0; i < count; i++) {
    const { week, day } = dayAt(i);
    const params = getWeekParameters(Math.min(week, 21));
    state.forEach((s, idx) => {
      let detail = 'rest day';
      if (s.spec.day === day && !params.isDeload) {
        if (s.spec.spec.type === 'Linear') {
          const delta = rand.pick([0, 1, 1, 2, 2, 3, 4, -1]);
          const pct =
            delta >= 5 ? 0.03 : delta >= 3 ? 0.02 : delta >= 1 ? 0.01 : delta === 0 ? 0 : -0.02;
          s.tm = round1(s.tm * (1 + pct));
          detail = `AMRAP ${delta >= 0 ? '+' : ''}${delta}`;
        } else if (s.spec.spec.type === 'RepsPerSet') {
          const spec = s.spec.spec;
          if (rand.next() < 0.4) {
            if (s.sets < spec.targetSets) s.sets += 1;
            else {
              s.weight = round1(s.weight + spec.increment);
              s.sets = spec.startSets;
            }
            detail = 'success';
          } else detail = 'maintained';
        }
      }
      series[idx].dataPoints.push({
        session: i + 1,
        week,
        block: Math.ceil(week / 7),
        trainingMax: s.spec.spec.type === 'Linear' ? s.tm : null,
        trainingMaxUnit: s.spec.spec.type === 'Linear' ? 'Kilograms' : null,
        currentWeight: s.spec.spec.type === 'Linear' ? null : s.weight,
        currentWeightUnit: s.spec.spec.type === 'Linear' ? null : 'Kilograms',
        summary: { type: s.spec.spec.type, details: { result: detail } },
      });
    });
  }

  const last = dayAt(count - 1);
  return {
    workoutName: PROGRAM_NAME,
    variant: 'FourDay',
    startWeek: CURRENT_WEEK,
    endWeek: last.week,
    totalWeeks: TOTAL_WEEKS,
    exerciseTimeSeries: series,
  };
}

/** NDJSON lines for a persistent run of `days` sessions. */
export function simulationStreamLines(
  days: number,
  successRate: number,
  maintainRate: number
): string[] {
  const rand = createRandom(7);
  const count = Math.max(1, Math.min(days, remainingSessions));
  const lines: string[] = [JSON.stringify({ type: 'started', totalDays: count })];
  for (let i = 0; i < count; i++) {
    const { week, day } = dayAt(i);
    const next = dayAt(i + 1);
    const deload = getWeekParameters(Math.min(week, 21)).isDeload;
    const r = rand.next();
    const outcome = deload
      ? 'Deload'
      : r < successRate
        ? 'Success'
        : r < successRate + maintainRate
          ? 'Maintained'
          : 'Fail';
    const dayExercises = exerciseStates.filter((s) => s.spec.day === day);
    lines.push(
      JSON.stringify({
        type: 'day',
        outcome,
        day,
        weekNumber: week,
        blockNumber: Math.ceil(week / 7),
        exercisesCompleted: dayExercises.length,
        newCurrentWeek: next.week,
        newCurrentDay: next.day,
        weekProgressed: next.week !== week,
        programComplete: i === remainingSessions - 1,
        isDeloadWeek: deload,
        progressionChanges: dayExercises.slice(0, 2).map((s) => ({
          exerciseId: s.spec.id,
          exerciseName: s.spec.name,
          change:
            outcome === 'Success'
              ? 'Training max increased'
              : outcome === 'Fail'
                ? 'Training max decreased'
                : 'No change',
        })),
      })
    );
  }
  lines.push(JSON.stringify({ type: 'completed' }));
  return lines;
}
