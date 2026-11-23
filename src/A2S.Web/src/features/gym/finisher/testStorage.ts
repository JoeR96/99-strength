import { vi } from 'vitest';

/** The global test setup stubs localStorage with no-op mocks; this backs them with a real map. */
export function memoryStorage(): Map<string, string> {
  const store = new Map<string, string>();
  vi.mocked(localStorage.getItem).mockImplementation((k: string) => store.get(k) ?? null);
  vi.mocked(localStorage.setItem).mockImplementation((k: string, v: string) => void store.set(k, String(v)));
  vi.mocked(localStorage.removeItem).mockImplementation((k: string) => void store.delete(k));
  return store;
}
