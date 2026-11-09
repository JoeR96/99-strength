import { describe, it, expect, vi, beforeEach } from 'vitest';
import { apiTokensApi } from './apiTokens';
import apiClient from './apiClient';
import { createdTokenFixture, terranToken } from '@/mocks/fixtures/apiTokens';

vi.mock('./apiClient', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

describe('apiTokensApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('list reads the user-scoped collection', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: [terranToken] });

    const tokens = await apiTokensApi.list();

    expect(apiClient.get).toHaveBeenCalledWith('/users/me/api-tokens');
    expect(tokens).toEqual([terranToken]);
  });

  it('create posts the name and returns the one-time token', async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: createdTokenFixture });

    const created = await apiTokensApi.create('Terran CC');

    expect(apiClient.post).toHaveBeenCalledWith('/users/me/api-tokens', { name: 'Terran CC' });
    expect(created.token).toBe(createdTokenFixture.token);
  });

  it('revoke deletes by id', async () => {
    vi.mocked(apiClient.delete).mockResolvedValue({ data: undefined });

    await apiTokensApi.revoke(terranToken.id);

    expect(apiClient.delete).toHaveBeenCalledWith(`/users/me/api-tokens/${terranToken.id}`);
  });
});
