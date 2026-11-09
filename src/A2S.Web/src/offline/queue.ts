/**
 * The `ops` store in IndexedDB. Records are keyed by an auto-incrementing `seq`
 * so a cursor walks them in the order they were queued; `id` is indexed for
 * removal and updates. The store lives in the browser's database, not in the
 * service worker or page memory, so a reload or a service-worker update keeps
 * every pending op.
 */

import { openDB, type DBSchema, type IDBPDatabase } from 'idb';
import { nanoid } from 'nanoid';
import type { NewOfflineOp, OfflineOp } from './types';

export const OFFLINE_DB_NAME = 'a2s-offline';
const STORE = 'ops';

type StoredOp = OfflineOp & { seq?: number };

interface OfflineDb extends DBSchema {
  ops: {
    key: number;
    value: StoredOp;
    indexes: { byId: string };
  };
}

export interface OfflineQueue {
  /** Appends an op and returns it with its id, createdAt and attempts filled in. */
  enqueue(op: NewOfflineOp): Promise<OfflineOp>;
  /** The oldest op, or undefined when the queue is empty. */
  peek(): Promise<OfflineOp | undefined>;
  remove(id: string): Promise<void>;
  /** Replaces the stored copy of an op (used to bump `attempts`). */
  update(op: OfflineOp): Promise<void>;
  count(): Promise<number>;
  /** Every op, oldest first. */
  all(): Promise<OfflineOp[]>;
  clear(): Promise<void>;
  close(): Promise<void>;
}

function strip(stored: StoredOp): OfflineOp {
  const op: StoredOp = { ...stored };
  delete op.seq;
  return op;
}

export function createQueue(dbName: string = OFFLINE_DB_NAME): OfflineQueue {
  let dbPromise: Promise<IDBPDatabase<OfflineDb>> | null = null;

  const db = () =>
    (dbPromise ??= openDB<OfflineDb>(dbName, 1, {
      upgrade(database) {
        const store = database.createObjectStore(STORE, { keyPath: 'seq', autoIncrement: true });
        store.createIndex('byId', 'id', { unique: true });
      },
    }));

  return {
    async enqueue(op) {
      const full = { ...op, id: nanoid(), createdAt: new Date().toISOString(), attempts: 0 } as OfflineOp;
      await (await db()).add(STORE, full);
      return full;
    },

    async peek() {
      const cursor = await (await db()).transaction(STORE).store.openCursor();
      return cursor ? strip(cursor.value) : undefined;
    },

    // One request per transaction: awaiting between two requests in the same transaction
    // lets it auto-commit under some schedulers (fake timers in tests), which throws.
    async remove(id) {
      const database = await db();
      const key = await database.getKeyFromIndex(STORE, 'byId', id);
      if (key !== undefined) await database.delete(STORE, key);
    },

    async update(op) {
      const database = await db();
      const key = await database.getKeyFromIndex(STORE, 'byId', op.id);
      if (key !== undefined) await database.put(STORE, { ...op, seq: key });
    },

    async count() {
      return (await db()).count(STORE);
    },

    async all() {
      return (await (await db()).getAll(STORE)).map(strip);
    },

    async clear() {
      await (await db()).clear(STORE);
    },

    async close() {
      if (!dbPromise) return;
      (await dbPromise).close();
      dbPromise = null;
    },
  };
}
