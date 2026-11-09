// Run: node --test tools/import-terran-habits.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildImportPayload, BOTTLE_ML } from './import-terran-habits.mjs';

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

test('maps four habits, drops Protein, keeps Terran ids as external keys', () => {
  const { habits, skipped } = buildImportPayload(TERRAN_HABITS, TERRAN_LOGS);

  assert.deepEqual(habits.map((h) => h.externalKey), ['terran:2', 'terran:1', 'terran:4', 'terran:5']);
  assert.deepEqual(skipped, [{ name: 'Protein', reason: 'out of scope' }]);
});

test('targets and minimums follow the brief', () => {
  const { habits } = buildImportPayload(TERRAN_HABITS, []);
  const byKey = Object.fromEntries(habits.map((h) => [h.externalKey, h]));

  assert.deepEqual([byKey['terran:1'].type, byKey['terran:1'].target, byKey['terran:1'].minimum], ['Counter', 2, 1]);
  assert.deepEqual([byKey['terran:2'].type, byKey['terran:2'].target, byKey['terran:2'].minimum, byKey['terran:2'].unit], ['Counter', 6, 4, 'bottles']);
  assert.deepEqual([byKey['terran:4'].type, byKey['terran:4'].target, byKey['terran:4'].minimum], ['Checkbox', 1, 1]);
  assert.deepEqual([byKey['terran:5'].type, byKey['terran:5'].target, byKey['terran:5'].minimum], ['Counter', 4, 2]);
});

test('water millilitres become 532 ml bottles, rounded', () => {
  const { habits } = buildImportPayload(TERRAN_HABITS, TERRAN_LOGS);
  const water = habits.find((h) => h.externalKey === 'terran:2');

  assert.equal(BOTTLE_ML, 532);
  assert.deepEqual(water.logs, [
    { date: '2026-10-17', value: 1 }, // 750 ml
    { date: '2026-10-20', value: 4 }, // 2000 ml
  ]);
});

test('logs are sorted by date and every mapped habit carries its history', () => {
  const { habits } = buildImportPayload(TERRAN_HABITS, TERRAN_LOGS);
  const walks = habits.find((h) => h.externalKey === 'terran:1');

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
