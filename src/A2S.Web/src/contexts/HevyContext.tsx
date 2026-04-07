/**
 * Hevy Context
 * Manages Hevy API key in session memory only (never persisted to localStorage)
 */

import { createContext, useContext, useState, type ReactNode } from 'react';
import { hevyApi } from '@/services/hevyApi';
import toast from 'react-hot-toast';

interface HevyContextType {
  apiKey: string | null;
  isConfigured: boolean;
  isValidating: boolean;
  isValid: boolean | null;
  setApiKey: (key: string) => Promise<boolean>;
  clearApiKey: () => void;
  validateKey: () => Promise<boolean>;
}

const HevyContext = createContext<HevyContextType | undefined>(undefined);

export function HevyProvider({ children }: { children: ReactNode }) {
  const [apiKey, setApiKeyState] = useState<string | null>(null);
  const [isValidating, setIsValidating] = useState(false);
  const [isValid, setIsValid] = useState<boolean | null>(null);

  const setApiKey = async (key: string): Promise<boolean> => {
    setIsValidating(true);
    try {
      hevyApi.setApiKey(key);
      setApiKeyState(key);

      try {
        const valid = await hevyApi.validateApiKey();
        setIsValid(valid);
        if (valid) {
          toast.success('Connected to Hevy successfully!');
        } else {
          toast.success('API key saved for this session. Will verify on next sync.');
          setIsValid(null);
        }
      } catch {
        toast.success('API key saved for this session. Will verify on next sync.');
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
