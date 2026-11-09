/** Personal API token types mirroring `A2S.Application.DTOs.ApiTokenDtos`. Timestamps are ISO-8601 UTC. */

/** A token as the list shows it. Never the token itself. */
export interface ApiTokenDto {
  id: string;
  name: string;
  /** The first 12 characters of the token, for telling tokens apart. */
  prefix: string;
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

/** The create response: the only time the full `token` is returned. */
export interface CreatedApiTokenDto {
  id: string;
  name: string;
  prefix: string;
  createdAt: string;
  token: string;
}

export const API_TOKEN_NAME_MAX_LENGTH = 50;
export const API_TOKEN_MAX_ACTIVE = 10;
