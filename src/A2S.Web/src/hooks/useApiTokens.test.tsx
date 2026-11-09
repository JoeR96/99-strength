import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { apiTokensApi } from '@/api/apiTokens';
import { createdTokenFixture, importToken, terranToken } from '@/mocks/fixtures/apiTokens';
import { apiTokenKeys, useApiTokens, useCreateApiToken, useRevokeApiToken } from './useApiTokens';

vi.mock('@/api/apiTokens', () => ({
  apiTokensApi: { list: vi.fn(), create: vi.fn(), revoke: vi.fn() },
}));

function setup() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  return { client, wrapper };
}

describe('useApiTokens', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads the list under its query key', async () => {
    vi.mocked(apiTokensApi.list).mockResolvedValue([terranToken]);
    const { client, wrapper } = setup();

    const { result } = renderHook(() => useApiTokens(), { wrapper });

    await waitFor(() => expect(result.current.data).toEqual([terranToken]));
    expect(client.getQueryData(apiTokenKeys.list())).toEqual([terranToken]);
  });

  it('creating returns the token and refetches the list', async () => {
    vi.mocked(apiTokensApi.list).mockResolvedValueOnce([]).mockResolvedValue([terranToken]);
    vi.mocked(apiTokensApi.create).mockResolvedValue(createdTokenFixture);
    const { wrapper } = setup();
    const { result } = renderHook(() => ({ list: useApiTokens(), create: useCreateApiToken() }), { wrapper });
    await waitFor(() => expect(result.current.list.data).toEqual([]));

    let created;
    await act(async () => {
      created = await result.current.create.mutateAsync('Terran CC');
    });

    expect(created).toEqual(createdTokenFixture);
    expect(apiTokensApi.create).toHaveBeenCalledWith('Terran CC');
    await waitFor(() => expect(result.current.list.data).toEqual([terranToken]));
  });

  it('revoking refetches the list', async () => {
    vi.mocked(apiTokensApi.list).mockResolvedValueOnce([importToken, terranToken]).mockResolvedValue([importToken]);
    vi.mocked(apiTokensApi.revoke).mockResolvedValue(undefined);
    const { wrapper } = setup();
    const { result } = renderHook(() => ({ list: useApiTokens(), revoke: useRevokeApiToken() }), { wrapper });
    await waitFor(() => expect(result.current.list.data).toHaveLength(2));

    await act(async () => {
      await result.current.revoke.mutateAsync(terranToken.id);
    });

    expect(apiTokensApi.revoke).toHaveBeenCalledWith(terranToken.id);
    await waitFor(() => expect(result.current.list.data).toEqual([importToken]));
  });
});
