import apiClient from './apiClient';
import type { ApiTokenDto, CreatedApiTokenDto } from '@/types/apiTokens';

/**
 * `/api/v1/users/me/api-tokens`: personal API tokens for programs that cannot sign in
 * (the desktop habit widget). These three calls need the signed-in session; a token
 * cannot create or revoke tokens.
 */
export const apiTokensApi = {
  /** Every token the user has created, revoked ones included, newest first. */
  list: async (): Promise<ApiTokenDto[]> => {
    const response = await apiClient.get<ApiTokenDto[]>('/users/me/api-tokens');
    return response.data;
  },

  /** Creates a token. The response is the only time the token itself is returned. */
  create: async (name: string): Promise<CreatedApiTokenDto> => {
    const response = await apiClient.post<CreatedApiTokenDto>('/users/me/api-tokens', { name });
    return response.data;
  },

  /** Revokes a token; it stops working immediately. */
  revoke: async (id: string): Promise<void> => {
    await apiClient.delete(`/users/me/api-tokens/${id}`);
  },
};
