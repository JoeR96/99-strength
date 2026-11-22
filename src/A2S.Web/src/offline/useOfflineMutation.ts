/**
 * `useMutation` that falls back to the queue. Offline, the request is not even
 * attempted: the op goes straight to IndexedDB and the mutation "succeeds" with
 * `{ queued: true }` so optimistic updates stay put. Online, a request that dies
 * on the wire (no HTTP answer) is queued the same way; a real answer, success or
 * 4xx/5xx, is handled exactly as before, so the online behaviour is unchanged.
 *
 * Each call gets one idempotency key up front: it goes out as the `Idempotency-Key` header on the first
 * attempt and becomes the queued op's id, so a request that reached the server but whose answer was lost
 * is not run a second time when the queue replays it.
 *
 * `networkMode: 'always'` matters: TanStack Query's default pauses mutations
 * while `navigator.onLine` is false, which would stop the queueing from running.
 */

import { nanoid } from 'nanoid';
import { useMutation, type UseMutationOptions, type UseMutationResult } from '@tanstack/react-query';
import { useOfflineSync } from './context';
import { isNetworkError } from './errors';
import type { NewOfflineOp, OfflineOp } from './types';

export type OfflineOutcome<TData> = { queued: false; data: TData } | { queued: true; op: OfflineOp };

export interface UseOfflineMutationOptions<TData, TVariables, TContext>
  extends Omit<UseMutationOptions<OfflineOutcome<TData>, Error, TVariables, TContext>, 'mutationFn' | 'networkMode'> {
  /** The real request; send `idempotencyKey` as the Idempotency-Key header (the api functions take it last). */
  run: (variables: TVariables, idempotencyKey: string) => Promise<TData>;
  /** The op to queue when the request cannot be made. */
  toOp: (variables: TVariables) => NewOfflineOp;
}

export function useOfflineMutation<TData, TVariables, TContext = unknown>(
  options: UseOfflineMutationOptions<TData, TVariables, TContext>
): UseMutationResult<OfflineOutcome<TData>, Error, TVariables, TContext> {
  const sync = useOfflineSync();
  const { run, toOp, ...rest } = options;

  return useMutation<OfflineOutcome<TData>, Error, TVariables, TContext>({
    ...rest,
    networkMode: 'always',
    mutationFn: async (variables) => {
      const id = nanoid();
      const queue = async () => ({ queued: true as const, op: await sync.enqueue({ ...toOp(variables), id }) });
      if (!sync.isOnline()) {
        return queue();
      }
      try {
        return { queued: false, data: await run(variables, id) };
      } catch (error) {
        if (isNetworkError(error)) {
          return queue();
        }
        throw error;
      }
    },
  });
}
