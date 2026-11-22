/**
 * Keeps an unsaved form on this phone. While the form differs from what it started with, its value is
 * saved to localStorage (`a2s-draft:{key}`) with a version, so a reload, a closed sheet or a dead battery
 * loses nothing. When the form opens again and a draft is there, `pendingDraft` holds it until the person
 * chooses Restore or Discard; nothing is written meanwhile, so the draft survives until they decide.
 * Call `clear()` after a successful save (or `clearFormDraft(key)` from outside the form).
 *
 * A draft saved under another `version` is ignored and removed: bump the version when the form's shape
 * changes rather than restoring something the form cannot read.
 */

import { useCallback, useEffect, useRef, useState } from 'react';

export const FORM_DRAFT_PREFIX = 'a2s-draft:';

interface StoredDraft<T> {
  version: number;
  savedAt: string;
  value: T;
}

export interface FormDraft<T> {
  value: T;
  savedAt: string;
}

const storageKey = (key: string) => `${FORM_DRAFT_PREFIX}${key}`;

function same(a: unknown, b: unknown): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/** Reads a draft saved under `version`; anything else (missing, unreadable, another version) is null. */
export function readFormDraft<T>(key: string, version: number): FormDraft<T> | null {
  try {
    const raw = localStorage.getItem(storageKey(key));
    if (!raw) return null;
    const stored = JSON.parse(raw) as Partial<StoredDraft<T>> | null;
    if (!stored || stored.version !== version || stored.value === undefined || typeof stored.savedAt !== 'string') {
      localStorage.removeItem(storageKey(key));
      return null;
    }
    return { value: stored.value, savedAt: stored.savedAt };
  } catch {
    return null;
  }
}

export function clearFormDraft(key: string): void {
  try {
    localStorage.removeItem(storageKey(key));
  } catch {
    /* storage unavailable */
  }
}

function writeFormDraft<T>(key: string, version: number, value: T): void {
  try {
    const stored: StoredDraft<T> = { version, savedAt: new Date().toISOString(), value };
    localStorage.setItem(storageKey(key), JSON.stringify(stored));
  } catch {
    /* private mode or full storage: the form still works, it just won't survive a reload */
  }
}

export interface UseFormDraftOptions<T> {
  /** Unique per form and record, e.g. `routine:{id}` or `routine:new`. */
  key: string;
  version: number;
  /** The form's current value. */
  value: T;
  /** What the form started with (the saved record); a value equal to it is not a draft. */
  initial: T;
}

export interface UseFormDraftResult<T> {
  /** A draft found when the form opened, waiting for Restore or Discard. */
  pendingDraft: FormDraft<T> | null;
  /** Hands back the draft's value (put it in the form) and resumes saving. */
  restore: () => T;
  /** Throws the draft away and resumes saving. */
  discard: () => void;
  /** Removes the draft; call after a successful save. */
  clear: () => void;
  /** True while the form differs from `initial`. */
  isDirty: boolean;
}

export function useFormDraft<T>({ key, version, value, initial }: UseFormDraftOptions<T>): UseFormDraftResult<T> {
  const [pendingDraft, setPendingDraft] = useState<FormDraft<T> | null>(() => {
    const found = readFormDraft<T>(key, version);
    return found && !same(found.value, initial) ? found : null;
  });
  const cleared = useRef(false);
  const isDirty = !same(value, initial);

  useEffect(() => {
    if (pendingDraft || cleared.current) return;
    if (isDirty) writeFormDraft(key, version, value);
    else clearFormDraft(key);
  }, [key, version, value, isDirty, pendingDraft]);

  const restore = useCallback(() => {
    const draft = pendingDraft;
    setPendingDraft(null);
    return (draft?.value ?? initial) as T;
  }, [pendingDraft, initial]);

  const discard = useCallback(() => {
    clearFormDraft(key);
    setPendingDraft(null);
  }, [key]);

  const clear = useCallback(() => {
    cleared.current = true;
    clearFormDraft(key);
  }, [key]);

  return { pendingDraft, restore, discard, clear, isDirty };
}
