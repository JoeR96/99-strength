/**
 * HevyContext Tests
 * Tests for Hevy API key management and validation (session-only, no localStorage)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
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
  });

  describe('Initial State', () => {
    it('should initialize with null API key (session-only storage)', () => {
      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      expect(screen.getByTestId('apiKey')).toHaveTextContent('null');
      expect(screen.getByTestId('isConfigured')).toHaveTextContent('false');
      expect(screen.getByTestId('isValidating')).toHaveTextContent('false');
      expect(screen.getByTestId('isValid')).toHaveTextContent('null');
    });
  });

  describe('setApiKey', () => {
    it('should call hevyApi.setApiKey and update state', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => {
        expect(hevyApi.setApiKey).toHaveBeenCalledWith('test-api-key');
        expect(screen.getByTestId('apiKey')).toHaveTextContent('test-api-key');
        expect(screen.getByTestId('isConfigured')).toHaveTextContent('true');
      });
    });

    it('should not store API key in localStorage', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);
      const setItemSpy = vi.spyOn(Storage.prototype, 'setItem');

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => {
        expect(screen.getByTestId('apiKey')).toHaveTextContent('test-api-key');
      });

      // Verify API key was never written to localStorage
      const hevyKeyCalls = setItemSpy.mock.calls.filter(
        ([key]) => key === 'hevy-api-key'
      );
      expect(hevyKeyCalls).toHaveLength(0);
      setItemSpy.mockRestore();
    });

    it('should handle validation failure gracefully', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(false);

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

    it('should handle validation error gracefully', async () => {
      vi.mocked(hevyApi.validateApiKey).mockRejectedValue(new Error('Network error'));

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => {
        expect(screen.getByTestId('apiKey')).toHaveTextContent('test-api-key');
      });
    });
  });

  describe('clearApiKey', () => {
    it('should call hevyApi.clearApiKey and reset state', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      // Set a key first
      await userEvent.click(screen.getByText('Set API Key'));
      await waitFor(() => {
        expect(screen.getByTestId('apiKey')).toHaveTextContent('test-api-key');
      });

      // Clear it
      await userEvent.click(screen.getByText('Clear API Key'));

      expect(hevyApi.clearApiKey).toHaveBeenCalled();
      expect(screen.getByTestId('apiKey')).toHaveTextContent('null');
      expect(screen.getByTestId('isConfigured')).toHaveTextContent('false');
      expect(screen.getByTestId('isValid')).toHaveTextContent('null');
    });
  });

  describe('validateKey', () => {
    it('should call hevyApi.validateApiKey when key exists', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      // Set a key first
      await userEvent.click(screen.getByText('Set API Key'));
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
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));
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
      vi.mocked(hevyApi.validateApiKey)
        .mockResolvedValueOnce(true)
        .mockRejectedValueOnce(new Error('Network error'));

      render(
        <HevyProvider>
          <TestConsumer />
        </HevyProvider>
      );

      await userEvent.click(screen.getByText('Set API Key'));
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
  });
});
