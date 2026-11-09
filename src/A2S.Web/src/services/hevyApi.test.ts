import { describe, it, expect, vi, beforeEach } from 'vitest';
import { hevyApi } from './hevyApi';

vi.mock('@/api/apiClient', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

import { apiClient } from '@/api/apiClient';

describe('hevyApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    hevyApi.setConfigured(false);
  });

  describe('configuration flag', () => {
    it('starts not configured', () => {
      expect(hevyApi.isConfigured()).toBe(false);
    });

    it('reflects the server-side key status set by the provider', () => {
      hevyApi.setConfigured(true);
      expect(hevyApi.isConfigured()).toBe(true);
      hevyApi.setConfigured(false);
      expect(hevyApi.isConfigured()).toBe(false);
    });
  });

  describe('request handling', () => {
    it('throws when no API key configured', async () => {
      await expect(hevyApi.getWorkouts()).rejects.toThrow(
        'Hevy API key not configured'
      );
    });

    it('makes GET request through the proxy without any key header', async () => {
      hevyApi.setConfigured(true);
      vi.mocked(apiClient.get).mockResolvedValue({
        data: { workouts: [], page: 1, page_count: 1 },
      });

      await hevyApi.getWorkouts(1, 10);
      expect(apiClient.get).toHaveBeenCalledWith(
        '/hevy/workouts?page=1&page_size=10'
      );
    });

    it('makes POST request for createWorkout', async () => {
      hevyApi.setConfigured(true);
      const requestBody = {
        workout: {
          title: 'Test',
          description: '',
          start_time: '2024-01-01T00:00:00Z',
          end_time: '2024-01-01T01:00:00Z',
          is_private: false,
          exercises: [],
        },
      };
      vi.mocked(apiClient.post).mockResolvedValue({
        data: { workout: { id: 'w-1', ...requestBody.workout } },
      });

      await hevyApi.createWorkout(requestBody);
      expect(apiClient.post).toHaveBeenCalledWith(
        '/hevy/workouts',
        requestBody
      );
    });

    it('extracts error message from API response', async () => {
      hevyApi.setConfigured(true);
      const error = {
        response: {
          data: { message: 'Rate limited' },
          status: 429,
          statusText: 'Too Many Requests',
        },
      };
      vi.mocked(apiClient.get).mockRejectedValue(error);

      await expect(hevyApi.getWorkouts()).rejects.toThrow('Rate limited');
    });

    it('falls back to status text when no message', async () => {
      hevyApi.setConfigured(true);
      const error = {
        response: {
          data: {},
          status: 500,
          statusText: 'Internal Server Error',
        },
      };
      vi.mocked(apiClient.get).mockRejectedValue(error);

      await expect(hevyApi.getWorkouts()).rejects.toThrow(
        'Hevy API error: 500 Internal Server Error'
      );
    });
  });

  describe('getWorkoutCount', () => {
    it('returns workout count', async () => {
      hevyApi.setConfigured(true);
      vi.mocked(apiClient.get).mockResolvedValue({
        data: { workout_count: 42 },
      });

      const count = await hevyApi.getWorkoutCount();
      expect(count).toBe(42);
    });
  });

  describe('validateApiKey', () => {
    it('returns true for valid key', async () => {
      hevyApi.setConfigured(true);
      vi.mocked(apiClient.get).mockResolvedValue({
        data: { valid: true },
      });

      const result = await hevyApi.validateApiKey();
      expect(result).toBe(true);
      expect(apiClient.get).toHaveBeenCalledWith('/hevy/validate');
    });

    it('throws on network error', async () => {
      hevyApi.setConfigured(true);
      vi.mocked(apiClient.get).mockRejectedValue(new Error('Unauthorized'));

      await expect(hevyApi.validateApiKey()).rejects.toThrow('Unauthorized');
    });

    it('returns false when the server reports the stored key as invalid', async () => {
      vi.mocked(apiClient.get).mockResolvedValue({ data: { valid: false } });
      const result = await hevyApi.validateApiKey();
      expect(result).toBe(false);
    });
  });
});
