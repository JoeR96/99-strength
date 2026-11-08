/** Shared helpers for stubbing an API module object in stories and tests. */

/** Overwrites the given functions on an API module object (the repo's story pattern). */
export function installHandlers<T extends object>(api: T, handlers: Partial<T>): void {
  Object.assign(api, handlers);
}

/** A request that never settles: drives Loading stories. */
export const never = () => new Promise<never>(() => {});

/** An axios-shaped rejection with the given status. */
export function rejectWith(status: number, message = `Request failed with status code ${status}`) {
  return () =>
    Promise.reject(Object.assign(new Error(message), { response: { status, data: { title: message } } }));
}
