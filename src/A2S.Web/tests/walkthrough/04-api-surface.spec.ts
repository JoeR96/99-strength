import { test, expect } from '@playwright/test';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { API_URL, WEB_URL } from './harness';

// What the API answers to a caller with no session. Needs no sign-in, so it runs even when the
// Clerk test user cannot be created. Every route must refuse with 401 (never 200, never 5xx),
// routes that exist only outside Production must be gone, and CORS must let the web origin in
// and nobody else.

const ID = '00000000-0000-4000-8000-000000000001';
const DATE = '2026-11-09';

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE';
const PROTECTED: [Method, string][] = [
  ['GET', '/users/me'],
  ['GET', `/users/${ID}`],
  ['GET', '/users/me/hevy-api-key'],
  ['PUT', '/users/me/hevy-api-key'],
  ['DELETE', '/users/me/hevy-api-key'],
  ['GET', '/users/me/api-tokens'],
  ['POST', '/users/me/api-tokens'],
  ['DELETE', `/users/me/api-tokens/${ID}`],
  ['GET', '/users/me/api-tokens/whoami'],
  ['GET', '/habits'],
  ['POST', '/habits'],
  ['PUT', `/habits/${ID}`],
  ['POST', `/habits/${ID}/archive`],
  ['POST', `/habits/${ID}/unarchive`],
  ['GET', `/habits/today?date=${DATE}`],
  ['GET', `/habits/history?from=${DATE}&to=${DATE}`],
  ['PUT', `/habits/${ID}/logs/${DATE}`],
  ['POST', `/habits/${ID}/logs/${DATE}/increment`],
  ['POST', '/habits/import'],
  ['PUT', `/body-weight/${DATE}`],
  ['GET', `/body-weight/trend?from=${DATE}&to=${DATE}`],
  ['GET', '/plans/active'],
  ['POST', '/plans'],
  ['GET', '/plans/suggested'],
  ['POST', '/plans/suggested/cut-to-85'],
  ['PUT', `/plans/${ID}/schedule`],
  ['POST', `/plans/${ID}/phases`],
  ['DELETE', `/plans/${ID}/phases/${DATE}`],
  ['POST', `/plans/${ID}/checkpoints`],
  ['DELETE', `/plans/${ID}/checkpoints/${DATE}`],
  ['POST', `/plans/${ID}/link-workout`],
  ['POST', `/plans/${ID}/completions`],
  ['DELETE', `/plans/${ID}/completions/${ID}`],
  ['POST', `/plans/${ID}/sessions/push`],
  ['POST', `/plans/${ID}/sessions/push-week`],
  ['POST', `/plans/${ID}/allowances`],
  ['DELETE', `/plans/${ID}/allowances`],
  ['PUT', `/plans/${ID}/high-intensity-target`],
  ['GET', `/plans/today?date=${DATE}`],
  ['GET', '/plans/calendar?month=2026-11'],
  ['GET', `/plans/weekly-goals?date=${DATE}`],
  ['GET', '/cardio-protocols'],
  ['POST', '/cardio-protocols'],
  ['POST', `/cardio-protocols/${ID}/advance`],
  ['PUT', `/cardio-protocols/${ID}/repeat-week`],
  ['GET', '/mobility-routines'],
  ['POST', '/mobility-routines'],
  ['PUT', `/mobility-routines/${ID}/drills`],
  ['GET', '/meals'],
  ['POST', '/meals'],
  ['PUT', `/meals/${ID}`],
  ['DELETE', `/meals/${ID}`],
  ['GET', `/meals/week?weekStart=${DATE}`],
  ['PUT', '/meals/week'],
  ['GET', `/shopping-list?weekStart=${DATE}`],
  ['POST', '/shopping-list/generate'],
  ['POST', '/shopping-list/items/toggle'],
  ['GET', `/shopping-list/export?weekStart=${DATE}`],
  ['GET', '/push/vapid-public-key'],
  ['GET', '/push/subscriptions'],
  ['POST', '/push/subscribe'],
  ['DELETE', '/push/subscribe'],
  ['GET', '/workouts'],
  ['POST', '/workouts'],
  ['GET', '/workouts/current'],
  ['POST', `/workouts/${ID}/activate`],
  ['DELETE', `/workouts/${ID}`],
  ['POST', `/workouts/${ID}/days/1/complete`],
  ['GET', `/workouts/${ID}/days/1/budget`],
  ['POST', `/workouts/${ID}/undo-last-completion`],
  ['GET', '/workouts/exercises/Squat/history'],
  ['GET', '/workouts/history'],
  ['GET', '/workouts/exercises/library'],
  ['PUT', `/workouts/${ID}/exercises`],
  ['PUT', `/workouts/${ID}/exercises/${ID}/substitute`],
  ['DELETE', `/workouts/${ID}/exercises/${ID}`],
  ['PUT', `/workouts/${ID}/exercises/${ID}/working-weight`],
  ['POST', `/workouts/${ID}/exercises/${ID}/confirm-weight`],
  ['POST', `/workouts/${ID}/exercises/${ID}/confirm-working-weight`],
  ['POST', `/workouts/${ID}/exercises/${ID}/retrofix-tm`],
  ['POST', `/workouts/${ID}/progress-week`],
  ['PUT', `/workouts/${ID}/block-sequence`],
  ['GET', '/workouts/weeks/1/days/1/plan'],
  ['GET', `/workouts/${ID}/weeks/1/days/1/plan`],
  ['GET', '/hevy/validate'],
  ['GET', '/hevy/routines'],
  ['POST', '/hevy/routines'],
  ['PUT', `/hevy/routines/${ID}`],
  ['DELETE', `/hevy/routines/${ID}`],
  ['GET', '/hevy/data/workouts'],
  ['GET', `/hevy/data/exercises/${ID}/history`],
  ['POST', '/hevy/pull'],
  ['GET', '/hevy/inbox'],
  ['POST', `/hevy/inbox/${ID}/assign`],
  ['POST', `/hevy/inbox/${ID}/ignore`],
  ['POST', '/hevy/sync/routine'],
];

const NON_PRODUCTION: [Method, string][] = [
  ['GET', `/workouts/${ID}/simulate`],
  ['GET', `/workouts/${ID}/simulate/stream`],
];

function call(method: Method, path: string, headers: Record<string, string> = {}) {
  const hasBody = method === 'POST' || method === 'PUT';
  return fetch(`${API_URL}/api/v1${path}`, {
    method,
    headers: { ...(hasBody ? { 'Content-Type': 'application/json' } : {}), ...headers },
    body: hasBody ? '{}' : undefined,
  });
}

test.describe('API with no session', () => {
  test('health answers 200', async () => {
    const response = await fetch(`${API_URL}/health`);
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ status: 'healthy' });
  });

  test('every route refuses an anonymous caller with 401', async () => {
    const wrong: string[] = [];
    for (const [method, path] of PROTECTED) {
      const response = await call(method, path);
      if (response.status !== 401) wrong.push(`${method} ${path} -> ${response.status}`);
    }
    expect(wrong, wrong.join('\n')).toEqual([]);
  });

  test('a made-up personal token and a made-up JWT are refused with 401, on allowed and other routes alike', async () => {
    const wrong: string[] = [];
    const tokens = ['nns_pat_' + 'A'.repeat(43), 'nns_pat_', 'not-a-jwt', 'eyJhbGciOiJub25lIn0.eyJzdWIiOiJ1c2VyXzEyMyJ9.'];
    const paths: [Method, string][] = [
      ['GET', '/users/me/api-tokens/whoami'],
      ['GET', `/habits/today?date=${DATE}`],
      ['GET', '/users/me/hevy-api-key'],
      ['POST', '/users/me/api-tokens'],
    ];
    for (const token of tokens) {
      for (const [method, path] of paths) {
        const response = await call(method, path, { Authorization: `Bearer ${token}` });
        if (response.status !== 401) wrong.push(`${method} ${path} with "${token.slice(0, 12)}…" -> ${response.status}`);
      }
    }
    expect(wrong, wrong.join('\n')).toEqual([]);
  });

  test('simulation routes and Swagger do not exist in Production', async () => {
    for (const [method, path] of NON_PRODUCTION) {
      const response = await call(method, path);
      expect(response.status, `${method} ${path}`).toBe(404);
    }
    expect((await fetch(`${API_URL}/swagger/index.html`)).status).toBe(404);
    expect((await fetch(`${API_URL}/swagger/v1/swagger.json`)).status).toBe(404);
  });

  test('CORS lets the web origin in and nobody else', async () => {
    const preflight = (origin: string) =>
      fetch(`${API_URL}/api/v1/habits/today?date=${DATE}`, {
        method: 'OPTIONS',
        headers: {
          Origin: origin,
          'Access-Control-Request-Method': 'GET',
          'Access-Control-Request-Headers': 'authorization,content-type',
        },
      });

    const allowed = await preflight(WEB_URL);
    expect(allowed.status).toBe(204);
    expect(allowed.headers.get('access-control-allow-origin')).toBe(WEB_URL);
    expect(allowed.headers.get('access-control-allow-headers')?.toLowerCase()).toContain('authorization');

    const stranger = await preflight('https://example.com');
    expect(stranger.headers.get('access-control-allow-origin')).toBeNull();

    // The 401 itself must carry the CORS header, or the browser reports a network error instead of 401.
    const refused = await fetch(`${API_URL}/api/v1/habits/today?date=${DATE}`, { headers: { Origin: WEB_URL } });
    expect(refused.status).toBe(401);
    expect(refused.headers.get('access-control-allow-origin')).toBe(WEB_URL);
  });
});

// ---------------------------------------------------------------------------------------------
// The web client against the live routes. Unit tests mock axios, so a path the API does not have
// only shows up here: a route that exists answers 401 to an anonymous caller, one that does not
// answers 404 (405 for the wrong verb).

const SRC = resolve(dirname(fileURLToPath(import.meta.url)), '../../src');

/** Client calls known to have no route on a Production API. Delete an entry when it is fixed. */
const KNOWN_MISSING: Record<string, string> = {
  'PUT /workouts/{id}/hevy-folder': 'v1 browser-side Hevy sync (Plan tab Sync buttons); the API never had this route',
  'POST /workouts/{id}/hevy-synced-routine': 'v1 browser-side Hevy sync; the API never had this route',
  'GET /workouts/{id}/simulate': 'simulation is removed from routing in Production ([NonProductionOnly])',
};

interface ClientCall {
  method: Method;
  path: string;
  where: string;
}

function clientCalls(): ClientCall[] {
  const files: string[] = [];
  const walk = (dir: string) => {
    for (const name of readdirSync(dir)) {
      const full = join(dir, name);
      if (statSync(full).isDirectory()) {
        if (name !== 'mocks' && name !== 'test') walk(full);
      } else if (/\.tsx?$/.test(name) && !/\.(test|stories)\./.test(name)) {
        files.push(full);
      }
    }
  };
  walk(SRC);

  const calls: ClientCall[] = [];
  for (const file of files) {
    const text = readFileSync(file, 'utf8');
    const constants = new Map([...text.matchAll(/const\s+([A-Z][A-Z0-9_]*)\s*=\s*['"`]([^'"`]+)['"`]/g)].map((m) => [m[1], m[2]]));
    for (const m of text.matchAll(/apiClient\s*\.\s*(get|post|put|delete)\s*(?:<[^;]*?>)?\(\s*([`'"])([\s\S]*?)\2/g)) {
      const path = m[3]
        .replace(/\$\{([^}]+)\}/g, (_, expr: string) => {
          const constant = constants.get(expr);
          if (constant !== undefined) return constant;
          if (/params|query/i.test(expr)) return '';
          if (/date/i.test(expr)) return DATE;
          if (/^(day|week|dayNumber|weekNumber|page|sessions)$/i.test(expr)) return '1';
          return ID;
        })
        .replace(/\?.*$/, '');
      const line = text.slice(0, m.index).split('\n').length;
      calls.push({ method: m[1].toUpperCase() as Method, path, where: `${relative(SRC, file).split(sep).join('/')}:${line}` });
    }
  }
  return calls;
}

test.describe('web client paths against the live API', () => {
  test('every path the client calls has a route', async () => {
    const calls = clientCalls();
    expect(calls.length, 'the scan found the client calls').toBeGreaterThan(60);

    const missing: string[] = [];
    const fixed = new Set(Object.keys(KNOWN_MISSING));
    for (const { method, path, where } of calls) {
      const response = await call(method, path);
      const key = `${method} ${path.split(ID).join('{id}')}`;
      if (response.status === 401) continue;
      if (key in KNOWN_MISSING) {
        fixed.delete(key);
        continue;
      }
      missing.push(`${method} ${path} -> ${response.status} (${where})`);
    }

    expect(missing, `client calls with no route:\n${missing.join('\n')}`).toEqual([]);
    expect([...fixed], 'these now have a route: remove them from KNOWN_MISSING').toEqual([]);
  });
});
