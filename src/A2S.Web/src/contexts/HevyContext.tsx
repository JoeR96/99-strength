/**
 * Hevy Context
 * Manages Hevy API key storage and sync state
 */

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
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

const STORAGE_KEY = 'hevy-api-key';

export function HevyProvider({ children }: { children: ReactNode }) {
  const [apiKey, setApiKeyState] = useState<string | null>(null);
  const [isValidating, setIsValidating] = useState(false);
  const [isValid, setIsValid] = useState<boolean | null>(null);

  // Load API key from localStorage on mount
  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) {
      setApiKeyState(stored);
      hevyApi.setApiKey(stored);
      // Validate the stored key
      validateStoredKey(stored);
    }
  }, []);

  const validateStoredKey = async (key: string) => {
    setIsValidating(true);
    try {
      hevyApi.setApiKey(key);
      const valid = await hevyApi.validateApiKey();
      setIsValid(valid);
    } catch {
      setIsValid(false);
    } finally {
      setIsValidating(false);
    }
  };

  const setApiKey = async (key: string): Promise<boolean> => {
    setIsValidating(true);
    try {
      // Save the key immediately - validation happens on first actual API call
      hevyApi.setApiKey(key);
      setApiKeyState(key);
      localStorage.setItem(STORAGE_KEY, key);

      // Try to validate in the background, but don't block on it
      try {
        const valid = await hevyApi.validateApiKey();
        setIsValid(valid);
        if (valid) {
          toast.success('Connected to Hevy successfully!');
        } else {
          // Key is saved but validation failed - might be a network issue
          toast.success('API key saved. Will verify on next sync.');
          setIsValid(null);
        }
      } catch (validationError) {
        console.warn('Hevy validation check failed:', validationError);
        // Key is saved, validation will happen on actual use
        toast.success('API key saved. Will verify on next sync.');
        setIsValid(null);
      }

      return true;
    } catch (error) {
      console.error('Error setting Hevy API key:', error);
      toast.error('Failed to save API key.');
      return false;
    } finally {
      setIsValidating(false);
    }
  };

  const clearApiKey = () => {
    setApiKeyState(null);
    localStorage.removeItem(STORAGE_KEY);
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
