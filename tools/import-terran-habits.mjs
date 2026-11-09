#!/usr/bin/env node
/**
 * One-off migration of Terran CC habits into 99-Strength.
 *
 * Reads the Terran CC SQLite store read-only, maps the five habits to the v2 definitions decided
 * in docs/v2/plan.md, and posts them to POST /api/v1/habits/import, which is idempotent (habits
 * are matched by externalKey; a log is written only when its value differs). Re-running is safe.
 *
 * Usage:
 *   node tools/import-terran-habits.mjs --dry-run                 # print the payload, change nothing
 *   node tools/import-terran-habits.mjs --token <token>           # import into the API
 *   node tools/import-terran-habits.mjs --api http://localhost:5123/api/v1 --token ...
 *
 * Options:
 *   --db <path>     Terran CC database (default %APPDATA%\terran-cc\tcc.db)
 *   --api <url>     API base (default https://api-99strength.enjoeneer.dev/api/v1 or $A2S_API_BASE)
 *   --token <token> A desktop widget token from Settings (nns_pat_…) or a Clerk session token
 *                   (or $A2S_TOKEN). Never printed, never written to disk.
 *   --out <file>    With --dry-run, write the payload to a file instead of stdout.
 *
 * Mapping (brief § 5 and plan decisions):
 *   Walks                 count  target 2   -> Counter  target 2, minimum 1
 *   Water                 amount 3000 ml    -> Counter  target 6 bottles (532 ml), minimum 4; values = round(ml / 532)
 *   Protein               amount 180 g      -> dropped (protein tracking is out of scope)
 *   Creatine + supplements check           -> Checkbox
 *   Standing desk         count  target 4   -> Counter  target 4, minimum 2
 */

import path from 'node:path';
import fs from 'node:fs';
import { pathToFileURL } from 'node:url';

export const BOTTLE_ML = 532;

/** Terran habit name -> 99-Strength definition. Null means "do not import". */
export const HABIT_MAP = {
  Walks: { name: 'Walks', type: 'Counter', target: 2, minimum: 1, unit: null, icon: '🚶', order: 2, value: (v) => Math.round(v) },
  Water: { name: 'Water (532 ml bottle)', type: 'Counter', target: 6, minimum: 4, unit: 'bottles', icon: '💧', order: 1, value: (ml) => Math.round(ml / BOTTLE_ML) },
  Protein: null,
  'Creatine + supplements': { name: 'Creatine (3–5 g)', type: 'Checkbox', target: 1, minimum: 1, unit: null, icon: '💊', order: 4, value: (v) => (v >= 1 ? 1 : 0) },
  'Standing desk': { name: 'Standing desk', type: 'Counter', target: 4, minimum: 2, unit: null, icon: '🧍', order: 5, value: (v) => Math.round(v) },
};

/**
 * Pure mapping from Terran rows to the ImportHabitsCommand payload.
 * @param {Array<{id:number,name:string,archived:number}>} habits
 * @param {Array<{habit_id:number,date:string,value:number}>} logs
 */
export function buildImportPayload(habits, logs) {
  const byHabit = new Map();
  for (const log of logs) {
    if (!byHabit.has(log.habit_id)) byHabit.set(log.habit_id, []);
    byHabit.get(log.habit_id).push(log);
  }

  const imported = [];
  const skipped = [];
  for (const habit of habits) {
    const spec = HABIT_MAP[habit.name];
    if (spec === undefined) {
      skipped.push({ name: habit.name, reason: 'no mapping' });
      continue;
    }
    if (spec === null) {
      skipped.push({ name: habit.name, reason: 'out of scope' });
      continue;
    }
    const rows = (byHabit.get(habit.id) ?? []).slice().sort((a, b) => a.date.localeCompare(b.date));
    imported.push({
      externalKey: `terran:${habit.id}`,
      name: spec.name,
      type: spec.type,
      target: spec.target,
      minimum: spec.minimum,
      unit: spec.unit,
      icon: spec.icon,
      order: spec.order,
      logs: rows.map((r) => ({ date: r.date, value: spec.value(Number(r.value)) })),
    });
  }
  imported.sort((a, b) => a.order - b.order);
  return { habits: imported, skipped };
}

export function readTerran(dbPath) {
  // node:sqlite ships with Node 22.5+/24; no native build step needed.
  return import('node:sqlite').then(({ DatabaseSync }) => {
    const db = new DatabaseSync(dbPath, { readOnly: true });
    try {
      const habits = db.prepare('select id, name, archived from habits order by "order"').all();
      const logs = db.prepare('select habit_id, date, value from habit_logs').all();
      return { habits, logs };
    } finally {
      db.close();
    }
  });
}

function parseArgs(argv) {
  const args = { dryRun: false, db: null, api: null, token: null, out: null };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--dry-run') args.dryRun = true;
    else if (a === '--db') args.db = argv[++i];
    else if (a === '--api') args.api = argv[++i];
    else if (a === '--token') args.token = argv[++i];
    else if (a === '--out') args.out = argv[++i];
    else throw new Error(`Unknown argument ${a}`);
  }
  return args;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const dbPath = args.db ?? path.join(process.env.APPDATA ?? '', 'terran-cc', 'tcc.db');
  const api = (args.api ?? process.env.A2S_API_BASE ?? 'https://api-99strength.enjoeneer.dev/api/v1').replace(/\/$/, '');

  const { habits, logs } = await readTerran(dbPath);
  const payload = buildImportPayload(habits, logs);

  const summary = payload.habits.map((h) => `${h.name}: ${h.logs.length} day(s)`).join(', ');
  console.error(`Mapped ${payload.habits.length} habit(s) — ${summary}. Skipped: ${payload.skipped.map((s) => `${s.name} (${s.reason})`).join(', ') || 'none'}.`);

  if (args.dryRun) {
    const json = JSON.stringify({ habits: payload.habits }, null, 2);
    if (args.out) fs.writeFileSync(args.out, json);
    else process.stdout.write(json + '\n');
    return;
  }

  const token = args.token ?? process.env.A2S_TOKEN;
  if (!token) {
    console.error('A token is required (--token or A2S_TOKEN): create a desktop widget token in Settings. Use --dry-run to preview.');
    process.exitCode = 2;
    return;
  }

  const response = await fetch(`${api}/habits/import`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
    body: JSON.stringify({ habits: payload.habits }),
  });
  const text = await response.text();
  if (!response.ok) {
    console.error(`Import failed: HTTP ${response.status} ${text}`);
    process.exitCode = 1;
    return;
  }
  console.error(`Import OK: ${text}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((err) => {
    console.error(err.message);
    process.exitCode = 1;
  });
}
