/** Fixtures for the "Desktop widget tokens" stories and tests. No real token appears here. */

import type { ApiTokenDto, CreatedApiTokenDto } from '@/types/apiTokens';

/** Shaped like a real token (`nns_pat_` + 43 characters) but never issued by any server. */
export const STORY_TOKEN = 'nns_pat_StoryOnlyExampleTokenNotARealSecret00000000';

export const terranToken: ApiTokenDto = {
  id: '0b0f6a52-5a4e-4a53-9a0f-6c8e4f3f1a01',
  name: 'Terran CC',
  prefix: 'nns_pat_Stor',
  createdAt: '2026-11-02T12:00:00Z',
  lastUsedAt: '2026-11-09T12:05:00Z',
  revokedAt: null,
};

export const importToken: ApiTokenDto = {
  id: '0b0f6a52-5a4e-4a53-9a0f-6c8e4f3f1a02',
  name: 'Habit import script',
  prefix: 'nns_pat_q7Lm',
  createdAt: '2026-11-08T12:00:00Z',
  lastUsedAt: null,
  revokedAt: null,
};

export const revokedToken: ApiTokenDto = {
  id: '0b0f6a52-5a4e-4a53-9a0f-6c8e4f3f1a03',
  name: 'Old laptop',
  prefix: 'nns_pat_Zx9c',
  createdAt: '2026-10-01T12:00:00Z',
  lastUsedAt: '2026-10-20T12:00:00Z',
  revokedAt: '2026-10-21T12:00:00Z',
};

/** Newest first, as the API returns them; the revoked one is there to prove it is not listed. */
export const apiTokensFixture: ApiTokenDto[] = [importToken, terranToken, revokedToken];

export const createdTokenFixture: CreatedApiTokenDto = {
  id: terranToken.id,
  name: terranToken.name,
  prefix: terranToken.prefix,
  createdAt: terranToken.createdAt,
  token: STORY_TOKEN,
};
