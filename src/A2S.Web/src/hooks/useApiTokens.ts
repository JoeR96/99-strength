/** Personal API tokens (the "Desktop widget tokens" section on Settings). */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiTokensApi } from '@/api/apiTokens';

export const apiTokenKeys = {
  all: ['api-tokens'] as const,
  list: () => [...apiTokenKeys.all, 'list'] as const,
};

export function useApiTokens() {
  return useQuery({
    queryKey: apiTokenKeys.list(),
    queryFn: () => apiTokensApi.list(),
  });
}

/** Creates a token and refreshes the list. The mutation result carries the token, once. */
export function useCreateApiToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => apiTokensApi.create(name),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: apiTokenKeys.list() }),
  });
}

export function useRevokeApiToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiTokensApi.revoke(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: apiTokenKeys.list() }),
  });
}
