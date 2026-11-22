import type { AxiosRequestConfig } from 'axios';

/** The header the offline queue's writes carry; the server stores the first answer per user and key. */
export const IDEMPOTENCY_HEADER = 'Idempotency-Key';

/** Request config carrying an `Idempotency-Key`, or nothing when there is no key. */
export function idempotent(key?: string): AxiosRequestConfig | undefined {
  return key ? { headers: { [IDEMPOTENCY_HEADER]: key } } : undefined;
}
