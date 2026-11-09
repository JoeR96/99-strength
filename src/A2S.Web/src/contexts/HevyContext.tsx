/**
 * Hevy Context
 * Tracks whether a Hevy API key is stored for the signed-in user. The key itself lives
 * encrypted on the server and never reaches the browser: the API only tells us whether one
 * exists and its last four characters, and every Hevy call is made server-side.
 */

import { useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import { hevyApi } from '@/services/hevyApi';
import { apiClient } from '@/api/apiClient';
import toast from 'react-hot-toast';
import { HevyContext } from './hevyStatusContext';

export interface HevyKeyStatus {
  hasKey: boolean;
  lastFour: string | null;
}

export function HevyProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<HevyKeyStatus>({ hasKey: false, lastFour: null });
  const [isValidating, setIsValidating] = useState(false);
  const [isValid, setIsValid] = useState<boolean | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const applyStatus = useCallback((next: HevyKeyStatus) => {
    setStatus(next);
    hevyApi.setConfigured(next.hasKey);
  }, []);

  // Ask the server whether a key is stored. Never fetches the key itself.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await apiClient.get<HevyKeyStatus>('/users/me/hevy-api-key');
        if (!cancelled) applyStatus({ hasKey: !!data.hasKey, lastFour: data.lastFour ?? null });
      } catch {
        // Not logged in yet or endpoint unavailable — ignore
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [applyStatus]);

  const validateKey = useCallback(async (): Promise<boolean> => {
    if (!status.hasKey) return false;
    setIsValidating(true);
    try {
      const valid = await hevyApi.validateApiKey();
      setIsValid(valid);
      return valid;
    } catch {
      setIsValid(false);
      return false;
    } finally {
      setIsValidating(false);
    }
  }, [status.hasKey]);

  const setApiKey = async (key: string): Promise<boolean> => {
    const trimmed = key.trim();
    if (!trimmed) return false;
    setIsValidating(true);
    try {
      await apiClient.put('/users/me/hevy-api-key', { apiKey: trimmed });
      applyStatus({ hasKey: true, lastFour: trimmed.slice(-4) });

      try {
        const valid = await hevyApi.validateApiKey();
        setIsValid(valid);
        if (valid) {
          toast.success('Connected to Hevy successfully!');
        } else {
          toast.success('API key saved. Will verify on next sync.');
          setIsValid(null);
        }
      } catch {
        toast.success('API key saved. Will verify on next sync.');
        setIsValid(null);
      }

      return true;
    } catch {
      toast.error('Failed to save API key.');
      return false;
    } finally {
      setIsValidating(false);
    }
  };

  const clearApiKey = async (): Promise<void> => {
    try {
      await apiClient.delete('/users/me/hevy-api-key');
      applyStatus({ hasKey: false, lastFour: null });
      setIsValid(null);
      toast.success('Disconnected from Hevy');
    } catch {
      toast.error('Failed to remove API key.');
    }
  };

  return (
    <HevyContext.Provider
      value={{
        isConfigured: status.hasKey,
        keyLastFour: status.lastFour,
        isValidating,
        isValid,
        isLoading,
        setApiKey,
        clearApiKey,
        validateKey,
      }}
    >
      {children}
    </HevyContext.Provider>
  );
}

export function useHevy() {
  const context = useContext(HevyContext);
  if (!context) {
    throw new Error('useHevy must be used within a HevyProvider');
  }
  return context;
}
