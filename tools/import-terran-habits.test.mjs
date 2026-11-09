// Run: node --test tools/import-terran-habits.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { buildImportPayload, BOTTLE_ML, HABIT_MAP } from './import-terran-habits.mjs';

const TERRAN_HABITS = [
  { id: 1, name: 'Walks', archived: 0 },
  { id: 2, name: 'Water', archived: 0 },
  { id: 3, name: 'Protein', archived: 0 },
  { id: 4, name: 'Creatine + supplements', archived: 0 },
  { id: 5, name: 'Standing desk', archived: 0 },
];

const TERRAN_LOGS = [
  { habit_id: 1, date: '2026-10-17', value: 1 },
  { habit_id: 2, date: '2026-10-17', value: 750 },
  { habit_id: 2, date: '2026-10-20', value: 2000 },
  { habit_id: 3, date: '2026-10-17', value: 129 },
  { habit_id: 4, date: '2026-10-17', value: 1 },
  { habit_id: 5, date: '2026-10-17', value: 2 },
  { habit_id: 1, date: '2026-11-02', value: 2 },
];

test('maps four habits, drops Protein, uses the keys the suggested plan seeds', () => {
  const { habits, skipped } = buildImportPayload(TERRAN_HABITS, TERRAN_LOGS);

  assert.deepEqual(habits.map((h) => h.externalKey), ['seed:water', 'terran:walks', 'terran:creatine', 'terran:standing-desk']);
  assert.deepEqual(skipped, [{ name: 'Protein', reason: 'out of scope' }]);
});

test('targets and minimums follow the brief', () => {
  const { habits } = buildImportPayload(TERRAN_HABITS, []);
  const byKey = Object.fromEntries(habits.map((h) => [h.externalKey, h]));

  assert.deepEqual([byKey['terran:walks'].type, byKey['terran:walks'].target, byKey['terran:walks'].minimum], ['Counter', 2, 1]);
  assert.deepEqual([byKey['seed:water'].type, byKey['seed:water'].target, byKey['seed:water'].minimum, byKey['seed:water'].unit], ['Counter', 6, 4, 'bottles']);
  assert.deepEqual([byKey['terran:creatine'].type, byKey['terran:creatine'].target, byKey['terran:creatine'].minimum], ['Checkbox', 1, 1]);
  assert.deepEqual([byKey['terran:standing-desk'].type, byKey['terran:standing-desk'].target, byKey['terran:standing-desk'].minimum], ['Counter', 4, 2]);
});

test('water millilitres become 532 ml bottles, rounded', () => {
  const { habits } = buildImportPayload(TERRAN_HABITS, TERRAN_LOGS);
  const water = habits.find((h) => h.externalKey === 'seed:water');

  assert.equal(BOTTLE_ML, 532);
  assert.deepEqual(water.logs, [
    { date: '2026-10-17', value: 1 }, // 750 ml
    { date: '2026-10-20', value: 4 }, // 2000 ml
  ]);
});

test('logs are sorted by date and every mapped habit carries its history', () => {
  const { habits } = buildImportPayload(TERRAN_HABITS, TERRAN_LOGS);
  const walks = habits.find((h) => h.externalKey === 'terran:walks');

  assert.deepEqual(walks.logs, [
    { date: '2026-10-17', value: 1 },
    { date: '2026-11-02', value: 2 },
  ]);
});

test('an unknown Terran habit is reported, not silently dropped', () => {
  const { habits, skipped } = buildImportPayload([{ id: 9, name: 'Meditation', archived: 0 }], []);

  assert.equal(habits.length, 0);
  assert.deepEqual(skipped, [{ name: 'Meditation', reason: 'no mapping' }]);
});

test('every imported habit the plan also seeds uses the seed\x27s key, name and icon', () => {
  // The API matches an import by externalKey only and the plan seed matches by key, then name. A
  // mismatch in either direction gives Joe two Water habits. Read the seed source so this cannot drift.
  const seed = fs.readFileSync(new URL('../src/A2S.Application/SuggestedPlans/CutTo85Nov2026.cs', import.meta.url), 'utf8');
  const seeded = [...seed.matchAll(/new\("([^"]+)", HabitType\.(\w+), (\d+), (\d+), (?:null|"[^"]*"), \d+, "([^"]+)", "([^"]+)"\)/g)]
    .map((m) => ({ name: m[1], type: m[2], target: Number(m[3]), minimum: Number(m[4]), icon: m[5], key: m[6] }));
  assert.equal(seeded.length, 5, 'expected the five seeded habits; has the seed format changed?');

  const shared = Object.values(HABIT_MAP).filter((spec) => spec && seeded.some((s) => s.key === spec.key));
  assert.deepEqual(shared.map((s) => s.key).sort(), ['seed:water', 'terran:creatine', 'terran:walks']);
  for (const spec of shared) {
    const s = seeded.find((x) => x.key === spec.key);
    assert.deepEqual(
      { name: spec.name, type: spec.type, target: spec.target, minimum: spec.minimum, icon: spec.icon },
      { name: s.name, type: s.type, target: s.target, minimum: s.minimum, icon: s.icon },
    );
  }
});
