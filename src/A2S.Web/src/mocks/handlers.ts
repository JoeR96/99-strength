/**
 * MSW request handlers for Storybook (and anything else that wants a believable
 * backend). One coherent data set: see ./fixtures/program.ts.
 *
 * Paths start with `*` so they match whatever VITE_API_BASE_URL the build used.
 * `handlers` is the full signed-in, Hevy-connected account; the named sets below
 * override pieces of it for specific stories (pass them first in `msw.handlers`).
 */
import { http, HttpResponse, delay } from 'msw';
import {
  currentWorkout,
  workoutHistory,
  workoutSummaries,
  PROGRAM_ID,
  CURRENT_WEEK,
  CURRENT_DAY,
} from './fixtures/program';
import { exerciseLibrary } from './fixtures/library';
import { exerciseHistoryFor } from './fixtures/exerciseHistory';
import { simulationResult, simulationStreamLines } from './fixtures/simulation';
import {
  HEVY_API_KEY,
  hevyExerciseHistory,
  hevyExerciseTemplates,
  hevyRoutines,
  hevyWorkouts,
} from './fixtures/hevy';

const API = '*/api/v1';

const paginate = <T>(items: T[], page: number, pageSize: number) => ({
  page,
  page_count: Math.max(1, Math.ceil(items.length / pageSize)),
  slice: items.slice((page - 1) * pageSize, page * pageSize),
});

const intParam = (url: string, name: string, fallback: number) => {
  const v = Number(new URL(url).searchParams.get(name));
  return Number.isFinite(v) && v > 0 ? v : fallback;
};

export const workoutHandlers = [
  http.get(`${API}/workouts/current`, () => HttpResponse.json(currentWorkout)),
  http.get(`${API}/workouts/history`, () => HttpResponse.json(workoutHistory)),
  http.get(`${API}/workouts/exercises/library`, () => HttpResponse.json(exerciseLibrary)),
  http.get(`${API}/workouts/exercises/:name/history`, ({ params }) => {
    const history = exerciseHistoryFor(decodeURIComponent(String(params.name)));
    return history ? HttpResponse.json(history) : new HttpResponse(null, { status: 404 });
  }),
  http.get(`${API}/workouts/:id/simulate/stream`, ({ request }) => {
    const days = intParam(request.url, 'days', 84);
    const url = new URL(request.url);
    const success = Number(url.searchParams.get('successRate') ?? 0.65);
    const maintain = Number(url.searchParams.get('maintainRate') ?? 0.25);
    const lines = simulationStreamLines(days, success, maintain);
    const encoder = new TextEncoder();
    const stream = new ReadableStream({
      async start(controller) {
        for (const line of lines) {
          controller.enqueue(encoder.encode(line + '\n'));
          await delay(5);
        }
        controller.close();
      },
    });
    return new HttpResponse(stream, { headers: { 'Content-Type': 'application/x-ndjson' } });
  }),
  http.get(`${API}/workouts/:id/simulate`, ({ request }) =>
    HttpResponse.json(simulationResult(intParam(request.url, 'sessions', 30)))
  ),
  http.get(`${API}/workouts`, () => HttpResponse.json(workoutSummaries)),
  http.post(`${API}/workouts`, () => HttpResponse.json({ id: 'wk-new' }, { status: 201 })),
  http.post(`${API}/workouts/:id/days/:day/complete`, ({ params }) =>
    HttpResponse.json({
      workoutId: PROGRAM_ID,
      day: Number(params.day),
      weekNumber: CURRENT_WEEK,
      blockNumber: 2,
      exercisesCompleted: 5,
      progressionChanges: [],
      newCurrentWeek: CURRENT_WEEK,
      newCurrentDay: CURRENT_DAY + 1,
      weekProgressed: false,
      programComplete: false,
      isDeloadWeek: false,
      exercisesPendingWeightConfirmation: [],
      nextSessionExercises: [],
    })
  ),
  // Mutations the stories can trigger: accept and move on.
  http.post(`${API}/workouts/*`, () => new HttpResponse(null, { status: 204 })),
  http.put(`${API}/workouts/*`, () => HttpResponse.json({})),
  http.delete(`${API}/workouts/*`, () => new HttpResponse(null, { status: 204 })),
];

export const userHandlers = [
  http.get(`${API}/users/me/hevy-api-key`, () => HttpResponse.json({ apiKey: HEVY_API_KEY })),
  http.put(`${API}/users/me/hevy-api-key`, () => new HttpResponse(null, { status: 204 })),
  http.get(`${API}/users/me`, () =>
    HttpResponse.json({
      id: 'user_2m9Qk',
      email: 'joe@example.com',
      name: 'Joe Richardson',
      createdAt: '2025-11-02T09:00:00Z',
    })
  ),
];

export const hevyHandlers = [
  http.get(`${API}/hevy/validate`, () => HttpResponse.json({ valid: true })),
  http.get(`${API}/hevy/routines`, ({ request }) => {
    const { page, page_count, slice } = paginate(
      hevyRoutines,
      intParam(request.url, 'page', 1),
      10
    );
    return HttpResponse.json({ page, page_count, routines: slice });
  }),
  http.get(`${API}/hevy/routine_folders`, () =>
    HttpResponse.json({
      page: 1,
      page_count: 1,
      routine_folders: [
        {
          id: 1184223,
          title: 'A2S Hypertrophy',
          created_at: '2026-07-01T06:00:00Z',
          updated_at: '2026-07-01T06:00:00Z',
        },
      ],
    })
  ),
  http.get(`${API}/hevy/exercise_templates`, ({ request }) => {
    const { page, page_count, slice } = paginate(
      hevyExerciseTemplates,
      intParam(request.url, 'page', 1),
      100
    );
    return HttpResponse.json({ page, page_count, exercise_templates: slice });
  }),
  http.get(`${API}/hevy/data/workouts`, ({ request }) => {
    const page = intParam(request.url, 'page', 1);
    const pageSize = intParam(request.url, 'pageSize', 10);
    const { page_count, slice } = paginate(hevyWorkouts, page, pageSize);
    return HttpResponse.json({ workouts: slice, page, pageCount: page_count });
  }),
  http.get(`${API}/hevy/data/exercises/:id/history`, ({ params }) =>
    HttpResponse.json(hevyExerciseHistory(String(params.id)))
  ),
  http.all(`${API}/hevy/*`, () => HttpResponse.json({})),
];

export const handlers = [...workoutHandlers, ...userHandlers, ...hevyHandlers];

/** No program yet: /workouts/current 404s, no programs, no history. */
export const noProgramHandlers = [
  http.get(`${API}/workouts/current`, () => new HttpResponse(null, { status: 404 })),
  http.get(`${API}/workouts/history`, () => new HttpResponse(null, { status: 404 })),
  http.get(`${API}/workouts`, () => HttpResponse.json([])),
];

/** Hevy never connected: no saved key. */
export const hevyDisconnectedHandlers = [
  http.get(`${API}/users/me/hevy-api-key`, () => HttpResponse.json({ apiKey: null })),
];

/** Every request hangs, for loading-state stories. */
export const loadingHandlers = [http.all(`${API}/*`, () => delay('infinite'))];
