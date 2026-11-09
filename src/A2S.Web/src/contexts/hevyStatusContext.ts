/**
 * The Hevy context object and a tolerant reader for it. `HevyProvider` (HevyContext.tsx) fills
 * it in; pages that only need to know whether a key is stored read `useHevyStatus()`, which
 * answers "no key" when no provider is mounted (tests and most stories) instead of throwing.
 */

import { createContext, useContext } from 'react';

export interface HevyContextType {
  /** A key is stored server-side. */
  isConfigured: boolean;
  /** Last four characters of the stored key, for recognition only. */
  keyLastFour: string | null;
  isValidating: boolean;
  isValid: boolean | null;
  isLoading: boolean;
  /** Stores a new key (replacing any existing one) and validates it against Hevy. */
  setApiKey: (key: string) => Promise<boolean>;
  /** Removes the stored key. */
  clearApiKey: () => Promise<void>;
  /** Re-checks the stored key against Hevy. */
  validateKey: () => Promise<boolean>;
}

export const HevyContext = createContext<HevyContextType | undefined>(undefined);

export interface HevyStatus {
  /** A Hevy key is stored, so pushes and pulls can run. */
  isConfigured: boolean;
  /** Still asking the server whether a key is stored. */
  isLoading: boolean;
}

const NO_KEY: HevyStatus = { isConfigured: false, isLoading: false };

/** Whether a Hevy key is stored. Safe without a `HevyProvider`: reads as "no key". */
export function useHevyStatus(): HevyStatus {
  const context = useContext(HevyContext);
  if (!context) return NO_KEY;
  return { isConfigured: context.isConfigured, isLoading: context.isLoading };
}
