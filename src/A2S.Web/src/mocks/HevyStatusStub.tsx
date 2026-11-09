/**
 * A stand-in HevyProvider for stories and tests: says whether a key is stored without asking the
 * server. Pages read `useHevyStatus()`, which falls back to "no key" when nothing is mounted, so
 * only the configured states need this. The story decorator is `withHevyStatus` in storyProviders.
 */

import type { ReactNode } from 'react';
import { HevyContext } from '@/contexts/hevyStatusContext';

export function HevyStatusStub({ isConfigured, children }: { isConfigured: boolean; children: ReactNode }) {
  return (
    <HevyContext.Provider
      value={{
        isConfigured,
        keyLastFour: isConfigured ? '7781' : null,
        isValidating: false,
        isValid: isConfigured ? true : null,
        isLoading: false,
        setApiKey: async () => true,
        clearApiKey: async () => {},
        validateKey: async () => isConfigured,
      }}
    >
      {children}
    </HevyContext.Provider>
  );
}
