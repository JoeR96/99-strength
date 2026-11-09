/** Story/test handlers for `apiTokensApi`: a small in-memory token store. */

import { apiTokensApi } from '@/api/apiTokens';
import { API_TOKEN_MAX_ACTIVE, type ApiTokenDto } from '@/types/apiTokens';
import { apiTokensFixture, STORY_TOKEN } from '../fixtures/apiTokens';
import { installHandlers, never, rejectWith } from './install';

type ApiTokenHandlers = Partial<typeof apiTokensApi>;

/** Behaves like the server: create adds a token, revoke stamps it, the cap is ten active tokens. */
export function statefulApiTokenHandlers(initial: ApiTokenDto[] = []): ApiTokenHandlers {
  let tokens = [...initial];

  return {
    list: async () => tokens,
    create: async (name) => {
      if (tokens.filter((t) => t.revokedAt === null).length >= API_TOKEN_MAX_ACTIVE) {
        const detail = `You can have at most ${API_TOKEN_MAX_ACTIVE} active tokens. Revoke one you no longer use first.`;
        throw Object.assign(new Error(detail), { response: { status: 422, data: { title: 'Business Rule Violation', detail } } });
      }
      const created = {
        id: `story-token-${tokens.length + 1}`,
        name: name.trim(),
        prefix: STORY_TOKEN.slice(0, 12),
        createdAt: new Date().toISOString(),
        token: STORY_TOKEN,
      };
      tokens = [{ id: created.id, name: created.name, prefix: created.prefix, createdAt: created.createdAt, lastUsedAt: null, revokedAt: null }, ...tokens];
      return created;
    },
    revoke: async (id) => {
      tokens = tokens.map((t) => (t.id === id && t.revokedAt === null ? { ...t, revokedAt: new Date().toISOString() } : t));
    },
  };
}

/** Ten active tokens: the create form is closed until one is revoked. */
export function tenActiveTokens(): ApiTokenDto[] {
  return Array.from({ length: API_TOKEN_MAX_ACTIVE }, (_, index) => ({
    id: `story-token-cap-${index + 1}`,
    name: `Token ${index + 1}`,
    prefix: 'nns_pat_Stor',
    createdAt: '2026-11-02T12:00:00Z',
    lastUsedAt: null,
    revokedAt: null,
  }));
}

export const apiTokenHandlers = {
  empty: (): ApiTokenHandlers => statefulApiTokenHandlers(),
  withTokens: (): ApiTokenHandlers => statefulApiTokenHandlers(apiTokensFixture),
  atCap: (): ApiTokenHandlers => statefulApiTokenHandlers(tenActiveTokens()),
  loading: (): ApiTokenHandlers => ({ list: never }),
  error: (): ApiTokenHandlers => ({ list: rejectWith(500) }),
};

export function installApiTokenHandlers(handlers: ApiTokenHandlers = apiTokenHandlers.empty()): void {
  installHandlers(apiTokensApi, handlers);
}
