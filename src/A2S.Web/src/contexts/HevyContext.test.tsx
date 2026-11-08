/**
 * HevyContext Tests
 * The browser only learns whether a key is stored (and its last four chars); it never holds the key.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HevyProvider, useHevy } from './HevyContext';
import { hevyApi } from '@/services/hevyApi';
import { apiClient } from '@/api/apiClient';

vi.mock('@/services/hevyApi', () => ({
  hevyApi: {
    setConfigured: vi.fn(),
    validateApiKey: vi.fn(),
  },
}));

vi.mock('@/api/apiClient', () => ({
  apiClient: {
    get: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('react-hot-toast', () => ({
  default: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

function TestConsumer() {
  const { isConfigured, keyLastFour, isValidating, isValid, isLoading, setApiKey, clearApiKey, validateKey } = useHevy();

  return (
    <div>
      <div data-testid="isConfigured">{String(isConfigured)}</div>
      <div data-testid="lastFour">{keyLastFour ?? 'null'}</div>
      <div data-testid="isValidating">{String(isValidating)}</div>
      <div data-testid="isValid">{isValid === null ? 'null' : String(isValid)}</div>
      <div data-testid="isLoading">{String(isLoading)}</div>
      <button onClick={() => setApiKey('test-api-key-9876')}>Set API Key</button>
      <button onClick={() => clearApiKey()}>Clear API Key</button>
      <button onClick={() => validateKey()}>Validate Key</button>
    </div>
  );
}

function renderWithProvider() {
  return render(
    <HevyProvider>
      <TestConsumer />
    </HevyProvider>
  );
}

describe('HevyContext', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(apiClient.get).mockResolvedValue({ data: { hasKey: false, lastFour: null } });
    vi.mocked(apiClient.put).mockResolvedValue({ data: null });
    vi.mocked(apiClient.delete).mockResolvedValue({ data: null });
  });

  describe('initial load', () => {
    it('asks the server for key status, not the key', async () => {
      renderWithProvider();

      await waitFor(() => expect(screen.getByTestId('isLoading')).toHaveTextContent('false'));
      expect(apiClient.get).toHaveBeenCalledWith('/users/me/hevy-api-key');
      expect(screen.getByTestId('isConfigured')).toHaveTextContent('false');
      expect(screen.getByTestId('lastFour')).toHaveTextContent('null');
      expect(hevyApi.setConfigured).toHaveBeenCalledWith(false);
    });

    it('reflects a stored key as configured with its last four characters', async () => {
      vi.mocked(apiClient.get).mockResolvedValue({ data: { hasKey: true, lastFour: '7781' } });

      renderWithProvider();

      await waitFor(() => expect(screen.getByTestId('isConfigured')).toHaveTextContent('true'));
      expect(screen.getByTestId('lastFour')).toHaveTextContent('7781');
      expect(hevyApi.setConfigured).toHaveBeenCalledWith(true);
    });

    it('never touches localStorage', async () => {
      const setItemSpy = vi.spyOn(Storage.prototype, 'setItem');
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      renderWithProvider();
      await userEvent.click(screen.getByText('Set API Key'));
      await waitFor(() => expect(screen.getByTestId('isConfigured')).toHaveTextContent('true'));

      expect(setItemSpy).not.toHaveBeenCalled();
      setItemSpy.mockRestore();
    });
  });

  describe('setApiKey', () => {
    it('PUTs the key to the server, marks configured and validates', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(true);

      renderWithProvider();
      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => {
        expect(apiClient.put).toHaveBeenCalledWith('/users/me/hevy-api-key', { apiKey: 'test-api-key-9876' });
        expect(screen.getByTestId('isConfigured')).toHaveTextContent('true');
        expect(screen.getByTestId('lastFour')).toHaveTextContent('9876');
        expect(screen.getByTestId('isValid')).toHaveTextContent('true');
      });
      expect(hevyApi.setConfigured).toHaveBeenLastCalledWith(true);
    });

    it('keeps the key saved when validation fails', async () => {
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(false);

      renderWithProvider();
      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => expect(screen.getByTestId('isConfigured')).toHaveTextContent('true'));
      expect(screen.getByTestId('isValid')).toHaveTextContent('null');
    });

    it('keeps the key saved when validation throws', async () => {
      vi.mocked(hevyApi.validateApiKey).mockRejectedValue(new Error('Network error'));

      renderWithProvider();
      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => expect(screen.getByTestId('isConfigured')).toHaveTextContent('true'));
    });

    it('does not mark configured when the server rejects the save', async () => {
      vi.mocked(apiClient.put).mockRejectedValue(new Error('500'));

      renderWithProvider();
      await userEvent.click(screen.getByText('Set API Key'));

      await waitFor(() => expect(screen.getByTestId('isValidating')).toHaveTextContent('false'));
      expect(screen.getByTestId('isConfigured')).toHaveTextContent('false');
    });
  });

  describe('clearApiKey', () => {
    it('DELETEs the key on the server and resets state', async () => {
      vi.mocked(apiClient.get).mockResolvedValue({ data: { hasKey: true, lastFour: '7781' } });

      renderWithProvider();
      await waitFor(() => expect(screen.getByTestId('isConfigured')).toHaveTextContent('true'));
      await userEvent.click(screen.getByText('Clear API Key'));

      await waitFor(() => expect(screen.getByTestId('isConfigured')).toHaveTextContent('false'));
      expect(apiClient.delete).toHaveBeenCalledWith('/users/me/hevy-api-key');
      expect(screen.getByTestId('lastFour')).toHaveTextContent('null');
      expect(screen.getByTestId('isValid')).toHaveTextContent('null');
      expect(hevyApi.setConfigured).toHaveBeenLastCalledWith(false);
    });
  });

  describe('validateKey', () => {
    it('does nothing when no key is stored', async () => {
      renderWithProvider();
      await waitFor(() => expect(screen.getByTestId('isLoading')).toHaveTextContent('false'));

      await userEvent.click(screen.getByText('Validate Key'));

      expect(hevyApi.validateApiKey).not.toHaveBeenCalled();
    });

    it('updates isValid from the server check', async () => {
      vi.mocked(apiClient.get).mockResolvedValue({ data: { hasKey: true, lastFour: '7781' } });
      vi.mocked(hevyApi.validateApiKey).mockResolvedValue(false);

      renderWithProvider();
      await waitFor(() => expect(screen.getByTestId('isConfigured')).toHaveTextContent('true'));
      await userEvent.click(screen.getByText('Validate Key'));

      await waitFor(() => expect(screen.getByTestId('isValid')).toHaveTextContent('false'));
    });

    it('treats a thrown validation as invalid', async () => {
      vi.mocked(apiClient.get).mockResolvedValue({ data: { hasKey: true, lastFour: '7781' } });
      vi.mocked(hevyApi.validateApiKey).mockRejectedValue(new Error('Network error'));

      renderWithProvider();
      await waitFor(() => expect(screen.getByTestId('isConfigured')).toHaveTextContent('true'));
      await userEvent.click(screen.getByText('Validate Key'));

      await waitFor(() => expect(screen.getByTestId('isValid')).toHaveTextContent('false'));
    });
  });

  describe('useHevy outside provider', () => {
    it('throws', () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      expect(() => render(<TestConsumer />)).toThrow('useHevy must be used within a HevyProvider');
      consoleSpy.mockRestore();
    });
  });
});
