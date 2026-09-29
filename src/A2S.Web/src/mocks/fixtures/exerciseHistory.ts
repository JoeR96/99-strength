/**
 * GET /workouts/exercises/:name/history — per-exercise history across programs, used by
 * the Exercise Library drill-down (ExerciseHistoryModal → ExerciseHistoryChart).
 *
 * Current-program sessions come from the program fixture; main lifts also get the
 * previous (completed) 21-week program so the 6M / 1Y ranges have something to show.
 */
import { getWeekParameters, roundToGymIncrement } from '@/utils/weekParameters';
import { PROGRAM_ID, PROGRAM_NAME, PROGRAM_START, exerciseStates } from './program';
import { createRandom } from './random';

interface SetDto {
  setNumber: number;
  weight: number;
  weightUnit: string;
  actualReps: number;
  wasAmrap: boolean;
}

interface SessionDto {
  workoutId: string;
  workoutName: string;
  weekNumber: number;
  blockNumber: number;
  completedAt: string;
  progressionType: string;
  sessionVolume: number;
  sets: SetDto[];
}

export interface AggregatedExerciseHistory {
  exerciseName: string;
  totalSessions: number;
  totalVolume: number;
  totalSets: number;
  totalReps: number;
  personalRecordWeight: number;
  personalRecordVolume: number;
  weightUnit: string;
  firstPerformed: string | null;
  lastPerformed: string | null;
  sessions: SessionDto[];
}

const volume = (sets: SetDto[]) => sets.reduce((a, s) => a + s.weight * s.actualReps, 0);

/** Previous program: same lifts, lighter, finished just before this one started. */
function previousProgramSessions(name: string, startTm: number, day: number): SessionDto[] {
  const rand = createRandom(name.length * 31 + day);
  const start = new Date(PROGRAM_START.getTime() - 24 * 7 * 86400000);
  let tm = startTm * 0.88;
  const out: SessionDto[] = [];
  for (let week = 1; week <= 21; week++) {
    const p = getWeekParameters(week);
    const weight = roundToGymIncrement(tm * p.intensity);
    const sets: SetDto[] = [];
    for (let n = 1; n <= p.sets; n++) {
      const amrap = !p.isDeload && n === p.sets;
      const reps = amrap
        ? (p.repOutTarget ?? p.targetReps) + rand.pick([0, 1, 1, 2, 2, 3])
        : p.targetReps;
      sets.push({
        setNumber: n,
        weight,
        weightUnit: 'Kilograms',
        actualReps: reps,
        wasAmrap: amrap,
      });
    }
    const date = new Date(start);
    date.setDate(date.getDate() + (week - 1) * 7 + [0, 1, 3, 4][day - 1]);
    date.setHours(7, 30, 0, 0);
    out.push({
      workoutId: 'wk-a2s-strength-spring',
      workoutName: 'A2S Strength — Spring Block',
      weekNumber: week,
      blockNumber: Math.ceil(week / 7),
      completedAt: date.toISOString(),
      progressionType: 'Linear',
      sessionVolume: volume(sets),
      sets,
    });
    if (!p.isDeload) tm *= 1.006;
  }
  return out;
}

function buildHistory(name: string): AggregatedExerciseHistory | null {
  const state = exerciseStates.find((s) => s.spec.name.toLowerCase() === name.toLowerCase());
  if (!state) return null;
  const current: SessionDto[] = state.sessions.map((s) => ({
    workoutId: PROGRAM_ID,
    workoutName: PROGRAM_NAME,
    weekNumber: s.week,
    blockNumber: Math.ceil(s.week / 7),
    completedAt: s.completedAt,
    progressionType: state.spec.spec.type,
    sessionVolume: volume(s.sets),
    sets: s.sets,
  }));
  const previous =
    state.spec.spec.type === 'Linear'
      ? previousProgramSessions(name, state.spec.spec.tm, state.spec.day)
      : [];
  const sessions = [...previous, ...current];
  const allSets = sessions.flatMap((s) => s.sets);
  return {
    exerciseName: state.spec.name,
    totalSessions: sessions.length,
    totalVolume: sessions.reduce((a, s) => a + s.sessionVolume, 0),
    totalSets: allSets.length,
    totalReps: allSets.reduce((a, s) => a + s.actualReps, 0),
    personalRecordWeight: Math.max(...allSets.map((s) => s.weight)),
    personalRecordVolume: Math.max(...sessions.map((s) => s.sessionVolume)),
    weightUnit: 'Kilograms',
    firstPerformed: sessions[0]?.completedAt ?? null,
    lastPerformed: sessions[sessions.length - 1]?.completedAt ?? null,
    sessions,
  };
}

const cache = new Map<string, AggregatedExerciseHistory | null>();

export function exerciseHistoryFor(name: string): AggregatedExerciseHistory | null {
  if (!cache.has(name)) cache.set(name, buildHistory(name));
  return cache.get(name) ?? null;
}
