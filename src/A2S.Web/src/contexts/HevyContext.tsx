/**
 * Hevy Context
 * Manages Hevy API key — persisted server-side on the User entity so it
 * survives page reloads and device switches.
 */

import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import { hevyApi } from '@/services/hevyApi';
import { apiClient } from '@/api/apiClient';
import toast from 'react-hot-toast';

interface HevyContextType {
  apiKey: string | null;
  isConfigured: boolean;
  isValidating: boolean;
  isValid: boolean | null;
  isLoading: boolean;
  setApiKey: (key: string) => Promise<boolean>;
  clearApiKey: () => void;
  validateKey: () => Promise<boolean>;
}

const HevyContext = createContext<HevyContextType | undefined>(undefined);

export function HevyProvider({ children }: { children: ReactNode }) {
  const [apiKey, setApiKeyState] = useState<string | null>(null);
  const [isValidating, setIsValidating] = useState(false);
  const [isValid, setIsValid] = useState<boolean | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Load persisted key from the backend on mount
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await apiClient.get<{ apiKey: string | null }>('/users/me/hevy-api-key');
        if (!cancelled && data.apiKey) {
          setApiKeyState(data.apiKey);
          hevyApi.setApiKey(data.apiKey);
        }
      } catch {
        // Not logged in yet or endpoint unavailable — ignore
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const setApiKey = async (key: string): Promise<boolean> => {
    setIsValidating(true);
    try {
      hevyApi.setApiKey(key);
      setApiKeyState(key);

      // Persist to backend
      try {
        await apiClient.put('/users/me/hevy-api-key', { apiKey: key });
      } catch {
        // Non-fatal — key is still in memory for this session
      }

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

  const clearApiKey = () => {
    setApiKeyState(null);
    hevyApi.clearApiKey();
    setIsValid(null);

    // Remove from backend
    apiClient.put('/users/me/hevy-api-key', { apiKey: null }).catch(() => {});

    toast.success('Disconnected from Hevy');
  };

  const validateKey = async (): Promise<boolean> => {
    if (!apiKey) return false;
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
  };

  return (
    <HevyContext.Provider
      value={{
        apiKey,
        isConfigured: apiKey !== null && apiKey.length > 0,
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
