/**
 * Hevy fixtures: a connected account with this week's routines synced into the
 * program's folder, and the lifter's logged Hevy workouts (the same sessions the
 * program history holds, as Hevy would report them).
 */
import type { HevyRoutine, HevyExerciseTemplate } from '@/types/hevy';
import { HEVY_EXERCISE_MAPPING } from '@/data/hevyExercises';
import { getWeekParameters, roundToGymIncrement } from '@/utils/weekParameters';
import {
  CURRENT_WEEK,
  DAYS_PER_WEEK,
  PROGRAM_NAME,
  exerciseStates,
  currentWorkout,
  sessionDate,
  workoutHistory,
} from './program';
import { exerciseHistoryFor } from './exerciseHistory';

export const HEVY_API_KEY = 'hevy_live_3f9c2a7d41b84e0c9a6f';
const FOLDER_ID = 1184223;

const DAY_TITLES = ['Squat', 'Bench', 'Deadlift', 'Press'];

function routineFor(week: number, day: number): HevyRoutine {
  const params = getWeekParameters(week);
  const exercises = currentWorkout.exercises
    .filter((e) => e.assignedDay === day)
    .sort((a, b) => a.orderInDay - b.orderInDay)
    .map((e, index) => {
      const state = exerciseStates.find((s) => s.spec.id === e.id)!;
      const isLinear = state.spec.spec.type === 'Linear';
      const setCount = isLinear ? params.sets : state.sets;
      const weight = isLinear ? roundToGymIncrement(state.tm * params.intensity) : state.weight;
      return {
        index,
        exercise_template_id: e.hevyExerciseTemplateId,
        rest_seconds: isLinear ? 180 : 90,
        notes: isLinear
          ? `TM ${state.tm} kg · last set AMRAP, aim for ${params.repOutTarget}+`
          : null,
        sets: Array.from({ length: setCount }, (_, i) => ({
          index: i,
          type: 'normal' as const,
          weight_kg: weight,
          reps: isLinear ? params.targetReps : null,
        })),
      };
    });
  const stamp = sessionDate(week, 1, 6, 0).toISOString();
  return {
    id: `rt-w${week}-d${day}`,
    title: `${PROGRAM_NAME} - Week ${week} Day ${day}`,
    folder_id: FOLDER_ID,
    notes: `${DAY_TITLES[day - 1]} day`,
    exercises,
    created_at: stamp,
    updated_at: stamp,
  };
}

/** GET /hevy/routines — this week (synced) plus last week's, and one hand-made routine. */
export const hevyRoutines: HevyRoutine[] = [
  ...Array.from({ length: DAYS_PER_WEEK }, (_, i) => routineFor(CURRENT_WEEK, i + 1)),
  ...Array.from({ length: DAYS_PER_WEEK }, (_, i) => routineFor(CURRENT_WEEK - 1, i + 1)),
  {
    id: 'rt-mobility',
    title: 'Hip Mobility Circuit',
    folder_id: null,
    exercises: [],
    created_at: sessionDate(2, 1).toISOString(),
    updated_at: sessionDate(2, 1).toISOString(),
  },
];

/** GET /hevy/exercise_templates — the built-in library, as Hevy lists it. */
export const hevyExerciseTemplates: HevyExerciseTemplate[] = Object.values(HEVY_EXERCISE_MAPPING)
  .filter((e) => !e.is_custom)
  .map((e) => ({
    id: e.id,
    title: e.title,
    exercise_type: 'weight_reps',
    equipment_category: e.equipment,
    muscle_group: e.muscle_group,
    is_custom: false,
  })) as HevyExerciseTemplate[];

interface HevyWorkoutSummary {
  id: string;
  title: string;
  startTime: string;
  endTime: string;
  exerciseCount: number;
  exercises: {
    exerciseTemplateId: string;
    title: string;
    setCount: number;
    bestWeight: number;
    bestReps: number;
    totalVolume: number;
  }[];
}

/** GET /hevy/data/workouts — newest first, 10 per page. */
export const hevyWorkouts: HevyWorkoutSummary[] = [...workoutHistory.completedActivities]
  .reverse()
  .map((activity) => {
    const start = new Date(activity.completedAt);
    const minutes = 62 + ((activity.weekNumber * 7 + activity.dayNumber * 5) % 24);
    return {
      id: `hv-w${activity.weekNumber}-d${activity.dayNumber}`,
      title: `${PROGRAM_NAME} - Week ${activity.weekNumber} Day ${activity.dayNumber}`,
      startTime: new Date(start.getTime() - minutes * 60000).toISOString(),
      endTime: start.toISOString(),
      exerciseCount: activity.performances.length,
      exercises: activity.performances.map((perf) => {
        const ex = currentWorkout.exercises.find((e) => e.id === perf.exerciseId)!;
        const best = perf.completedSets.reduce((b, s) =>
          s.weight * s.actualReps > b.weight * b.actualReps ? s : b
        );
        return {
          exerciseTemplateId: ex.hevyExerciseTemplateId,
          title: ex.name,
          setCount: perf.completedSets.length,
          bestWeight: best.weight,
          bestReps: best.actualReps,
          totalVolume: perf.completedSets.reduce((a, s) => a + s.weight * s.actualReps, 0),
        };
      }),
    };
  });

/** GET /hevy/data/exercises/:templateId/history */
export function hevyExerciseHistory(templateId: string) {
  const ex = currentWorkout.exercises.find((e) => e.hevyExerciseTemplateId === templateId);
  const title =
    ex?.name ?? Object.values(HEVY_EXERCISE_MAPPING).find((e) => e.id === templateId)?.title;
  const history = title ? exerciseHistoryFor(title) : null;
  const sessions = (history?.sessions ?? []).map((s) => {
    const weights = s.sets.map((x) => x.weight);
    const reps = s.sets.map((x) => x.actualReps);
    return {
      date: s.completedAt,
      workoutTitle: `${s.workoutName} - Week ${s.weekNumber}`,
      sets: s.sets.length,
      maxWeight: Math.max(...weights),
      maxReps: Math.max(...reps),
      totalVolume: s.sessionVolume,
      avgWeight: weights.reduce((a, b) => a + b, 0) / weights.length,
      avgReps: reps.reduce((a, b) => a + b, 0) / reps.length,
      setDetails: s.sets.map((x) => ({
        weightKg: x.weight,
        reps: x.actualReps,
        type: x.wasAmrap ? 'failure' : 'normal',
      })),
    };
  });
  return { exerciseTemplateId: templateId, sessions, totalSessions: sessions.length };
}
