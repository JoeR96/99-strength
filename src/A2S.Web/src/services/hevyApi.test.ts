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
    hevyApi.clearApiKey();
  });

  describe('API key management', () => {
    it('starts with no API key', () => {
      expect(hevyApi.isConfigured()).toBe(false);
      expect(hevyApi.getApiKey()).toBeNull();
    });

    it('sets and retrieves API key', () => {
      hevyApi.setApiKey('test-key');
      expect(hevyApi.isConfigured()).toBe(true);
      expect(hevyApi.getApiKey()).toBe('test-key');
    });

    it('clears API key', () => {
      hevyApi.setApiKey('test-key');
      hevyApi.clearApiKey();
      expect(hevyApi.isConfigured()).toBe(false);
      expect(hevyApi.getApiKey()).toBeNull();
    });

    it('considers empty string as not configured', () => {
      hevyApi.setApiKey('');
      expect(hevyApi.isConfigured()).toBe(false);
    });
  });

  describe('request handling', () => {
    it('throws when no API key configured', async () => {
      await expect(hevyApi.getWorkouts()).rejects.toThrow(
        'Hevy API key not configured'
      );
    });

    it('makes GET request with API key header', async () => {
      hevyApi.setApiKey('my-key');
      vi.mocked(apiClient.get).mockResolvedValue({
        data: { workouts: [], page: 1, page_count: 1 },
      });

      await hevyApi.getWorkouts(1, 10);
      expect(apiClient.get).toHaveBeenCalledWith(
        '/hevy/workouts?page=1&page_size=10',
        { headers: { 'X-Hevy-Api-Key': 'my-key' } }
      );
    });

    it('makes POST request for createWorkout', async () => {
      hevyApi.setApiKey('my-key');
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
        requestBody,
        { headers: { 'X-Hevy-Api-Key': 'my-key' } }
      );
    });

    it('extracts error message from API response', async () => {
      hevyApi.setApiKey('my-key');
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
      hevyApi.setApiKey('my-key');
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
      hevyApi.setApiKey('my-key');
      vi.mocked(apiClient.get).mockResolvedValue({
        data: { workout_count: 42 },
      });

      const count = await hevyApi.getWorkoutCount();
      expect(count).toBe(42);
    });
  });

  describe('validateApiKey', () => {
    it('returns true for valid key', async () => {
      hevyApi.setApiKey('my-key');
      vi.mocked(apiClient.get).mockResolvedValue({
        data: { valid: true },
      });

      const result = await hevyApi.validateApiKey();
      expect(result).toBe(true);
      expect(apiClient.get).toHaveBeenCalledWith('/hevy/validate', {
        headers: { 'X-Hevy-Api-Key': 'my-key' },
      });
    });

    it('throws on network error', async () => {
      hevyApi.setApiKey('bad-key');
      vi.mocked(apiClient.get).mockRejectedValue(new Error('Unauthorized'));

      await expect(hevyApi.validateApiKey()).rejects.toThrow('Unauthorized');
    });

    it('returns false when no API key set', async () => {
      const result = await hevyApi.validateApiKey();
      expect(result).toBe(false);
    });
  });
});
