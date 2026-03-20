/**
 * HevyContext Tests
 * Tests for Hevy API key management and validation
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HevyProvider, useHevy } from './HevyContext';
import { hevyApi } from '@/services/hevyApi';

// Mock the hevyApi module
vi.mock('@/services/hevyApi', () => ({
  hevyApi: {
    setApiKey: vi.fn(),
    clearApiKey: vi.fn(),
    validateApiKey: vi.fn(),
  },
}));

// Mock react-hot-toast
vi.mock('react-hot-toast', () => ({
  default: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Test component that uses the context
function TestConsumer() {
  const { apiKey, isConfigured, isValidating, isValid, setApiKey, clearApiKey, validateKey } = useHevy();

  return (
    <div>
      <div data-testid="apiKey">{apiKey || 'null'}</div>
      <div data-testid="isConfigured">{String(isConfigured)}</div>
      <div data-testid="isValidating">{String(isValidating)}</div>
      <div data-testid="isValid">{isValid === null ? 'null' : String(isValid)}</div>
      <button onClick={() => setApiKey('test-api-key')}>Set API Key</button>
      <button onClick={() => clearApiKey()}>Clear API Key</button>
      <button onClick={() => validateKey()}>Validate Key</button>
    </div>
  );
}

describe('HevyContext', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset localStorage mock
    vi.mocked(localStorage.getItem).mockReturnValue(null);
    vi.mocked(localStorage.setItem).mockImplementation(() => {});
    vi.mocked(localStorage.removeItem).mockImplementation(() => {});
  });

  describe('Initial State', () => {
    it('should initialize with null API key when localStorage is empty', () => {
      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      expect(screen.getByTestId('apiKey')).toHaveTextContent('null');
      expect(screen.getByTestId('isConfigured')).toHaveTextContent('false');
    });

    it('should load API key from localStorage on mount', async () => {
      vi.mocked(localStorage.getItem).mockReturnValue('stored-api-key');
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      expect(screen.getByTestId('apiKey')).toHaveTextContent('stored-api-key');
      expect(screen.getByTestId('isConfigured')).toHaveTextContent('true');

      // Should validate stored key
      await waitFor(() => {
        expect(hevyApi.setApiKey).toHaveBeenCalledWith('stored-api-key');
        expect(hevyApi.validateApiKey).toHaveBeenCalled();
      });
    });

    it('should start validating when API key exists in localStorage', () => {
      vi.mocked(localStorage.getItem).mockReturnValue('stored-api-key');
      vi.mocked(hevyApi.validateApiKey).mockReturnValue(new Promise(() => {})); // Never resolves

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      expect(screen.getByTestId('isValidating')).toHaveTextContent('true');
    });
  });

  describe('setApiKey', () => {
    it('should save API key to localStorage', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => {
        expect(localStorage.setItem).toHaveBeenCalledWith('hevy-api-key', 'test-api-key');
      });
    });

    it('should call hevyApi.setApiKey', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => {
        expect(hevyApi.setApiKey).toHaveBeenCalledWith('test-api-key');
      });
    });

    it('should update state after setting API key', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => {
        expect(screen.getByTestId('apiKey')).toHaveTextContent('test-api-key');
        expect(screen.getByTestId('isConfigured')).toHaveTextContent('true');
      });
    });

    it('should handle validation failure gracefully', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(false);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));

      // Key should still be saved even if validation fails
      await waitFor(() => {
        expect(screen.getByTestId('apiKey')).toHaveTextContent('test-api-key');
        expect(screen.getByTestId('isConfigured')).toHaveTextContent('true');
      });
    });

    it('should handle validation error gracefully', async () => {
      vi.mocked(hevyApi.validateApiKey).mockRejectedValue(new Error('Network error'));

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));

      // Key should still be saved even if validation errors
      await waitFor(() => {
        expect(screen.getByTestId('apiKey')).toHaveTextContent('test-api-key');
      });
    });
  });

  describe('clearApiKey', () => {
    it('should remove API key from localStorage', async () => {
      vi.mocked(localStorage.getItem).mockReturnValue('stored-api-key');
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId('apiKey')).toHaveTextContent('stored-api-key');
      });

      await userEvent.click(screen.getByText('Clear API Key'));

      expect(localStorage.removeItem).toHaveBeenCalledWith('hevy-api-key');
    });

    it('should call hevyApi.clearApiKey', async () => {
      vi.mocked(localStorage.getItem).mockReturnValue('stored-api-key');
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId('apiKey')).toHaveTextContent('stored-api-key');
      });

      await userEvent.click(screen.getByText('Clear API Key'));

      expect(hevyApi.clearApiKey).toHaveBeenCalled();
    });

    it('should reset state after clearing', async () => {
      vi.mocked(localStorage.getItem).mockReturnValue('stored-api-key');
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId('apiKey')).toHaveTextContent('stored-api-key');
      });

      await userEvent.click(screen.getByText('Clear API Key'));

      expect(screen.getByTestId('apiKey')).toHaveTextContent('null');
      expect(screen.getByTestId('isConfigured')).toHaveTextContent('false');
      expect(screen.getByTestId('isValid')).toHaveTextContent('null');
    });
  });

  describe('validateKey', () => {
    it('should call hevyApi.validateApiKey', async () => {
      vi.mocked(localStorage.getItem).mockReturnValue('stored-api-key');
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId('isValidating')).toHaveTextContent('false');
      });

      vi.mocked(hevyApi.validateApiKey).mockClear();
      await userEvent.click(screen.getByText('Validate Key'));

      await waitFor(() => {
        expect(hevyApi.validateApiKey).toHaveBeenCalled();
      });
    });

    it('should update isValid state based on validation result', async () => {
      vi.mocked(localStorage.getItem).mockReturnValue('stored-api-key');
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId('isValid')).toHaveTextContent('true');
      });

      // Now test with invalid key
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(false);
      await userEvent.click(screen.getByText('Validate Key'));

      await waitFor(() => {
        expect(screen.getByTestId('isValid')).toHaveTextContent('false');
      });
    });

    it('should return false when no API key is set', async () => {
      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Validate Key'));

      expect(hevyApi.validateApiKey).not.toHaveBeenCalled();
    });

    it('should handle validation errors', async () => {
      vi.mocked(localStorage.getItem).mockReturnValue('stored-api-key');
      vi.mocked(hevyApi.validateApiKey)
        .mockResolvedValueOnce(true) // Initial validation
        .mockRejectedValueOnce(new Error('Network error')); // Second validation

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await waitFor(() => {
        expect(screen.getByTestId('isValid')).toHaveTextContent('true');
      });

      await userEvent.click(screen.getByText('Validate Key'));

      await waitFor(() => {
        expect(screen.getByTestId('isValid')).toHaveTextContent('false');
      });
    });
  });

  describe('useHevy outside provider', () => {
    it('should throw error when used outside HevyProvider', () => {
      // Suppress console.error for this test
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      expect(() => {
        render(<TestConsumer />);
      }).toThrow('useHevy must be used within a HevyProvider');

      consoleSpy.mockRestore();
    });
  });

  describe('isConfigured', () => {
    it('should be true when API key is set and non-empty', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => {
        expect(screen.getByTestId('isConfigured')).toHaveTextContent('true');
      });
    });

    it('should be false when API key is null', () => {
      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      expect(screen.getByTestId('isConfigured')).toHaveTextContent('false');
    });

    it('should be false when API key is empty string', () => {
      vi.mocked(localStorage.getItem).mockReturnValue('');

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      expect(screen.getByTestId('isConfigured')).toHaveTextContent('false');
    });
  });
});
