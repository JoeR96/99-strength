import type { Meta, StoryObj } from '@storybook/react';
import { ApiTokensSettings } from './ApiTokensSettings';
import { withAppProviders, withApiMocks } from '@/mocks/storyProviders';
import { apiTokenHandlers, installApiTokenHandlers } from '@/mocks/handlers/apiTokens';
import { createdTokenFixture } from '@/mocks/fixtures/apiTokens';

// Titled under "Pages" so the 390 px phone checks (tests/storybook/pages.spec.ts: no sideways
// scroll, 44 px tap targets, axe) run against every story here.
const meta = {
  title: 'Pages/Settings/ApiTokensSettings',
  component: ApiTokensSettings,
  parameters: { layout: 'padded', viewport: { defaultViewport: 'mobile2' } },
  decorators: [withAppProviders('/settings')],
} satisfies Meta<typeof ApiTokensSettings>;

export default meta;
type Story = StoryObj<typeof meta>;

/** No tokens yet: the name field and Create. */
export const Empty: Story = {
  decorators: [withApiMocks(() => installApiTokenHandlers(apiTokenHandlers.empty()))],
};

/** Two tokens, one used and one never used. A revoked third is returned by the API and not listed. */
export const WithTokens: Story = {
  decorators: [withApiMocks(() => installApiTokenHandlers(apiTokenHandlers.withTokens()))],
};

/** Straight after Create: the token in a read-only field with Copy and the one-time warning. */
export const JustCreated: Story = {
  args: { justCreated: createdTokenFixture },
  decorators: [withApiMocks(() => installApiTokenHandlers(apiTokenHandlers.withTokens()))],
};

/** Ten active tokens: creating is closed until one is revoked. */
export const AtLimit: Story = {
  decorators: [withApiMocks(() => installApiTokenHandlers(apiTokenHandlers.atCap()))],
};

export const LoadError: Story = {
  decorators: [withApiMocks(() => installApiTokenHandlers(apiTokenHandlers.error()))],
};
