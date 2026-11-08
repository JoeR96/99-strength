/**
 * Sorting a failed write into "the network dropped it" (queue it), "the server
 * is having a bad moment" (retry with backoff) and "the server said no" (drop it).
 */

interface ErrorShape {
  code?: string;
  isAxiosError?: boolean;
  response?: { status?: number };
  request?: unknown;
}

function shape(error: unknown): ErrorShape | null {
  return typeof error === 'object' && error !== null ? (error as ErrorShape) : null;
}

/** The HTTP status the server answered with, or undefined when no answer came back. */
export function statusOf(error: unknown): number | undefined {
  return shape(error)?.response?.status;
}

const NETWORK_CODES = new Set(['ERR_NETWORK', 'ECONNABORTED', 'ETIMEDOUT', 'ERR_INTERNET_DISCONNECTED']);

/** True when the request never got an HTTP answer: offline, DNS, timeout, aborted. */
export function isNetworkError(error: unknown): boolean {
  const e = shape(error);
  if (!e) return false;
  if (e.response) return false;
  if (e.code && NETWORK_CODES.has(e.code)) return true;
  if (e.isAxiosError) return true;
  return error instanceof TypeError && /fetch|network/i.test(error.message);
}

/** Network trouble or a 5xx / 408 / 429: the write may well succeed later. */
export function isRetryable(error: unknown): boolean {
  if (isNetworkError(error)) return true;
  const status = statusOf(error);
  return status !== undefined && (status >= 500 || status === 408 || status === 429);
}
