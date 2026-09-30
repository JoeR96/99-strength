import type { StorybookConfig } from '@storybook/react-vite';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { mergeConfig } from 'vite';

const here = path.dirname(fileURLToPath(import.meta.url));

const config: StorybookConfig = {
  stories: ['../src/**/*.mdx', '../src/**/*.stories.@(js|jsx|mjs|ts|tsx)'],
  addons: [
    '@chromatic-com/storybook',
    '@storybook/addon-vitest',
    '@storybook/addon-a11y',
    '@storybook/addon-docs',
    '@storybook/addon-onboarding',
  ],
  framework: '@storybook/react-vite',
  // `.storybook/public` holds MSW's service worker (npx msw init .storybook/public).
  staticDirs: ['../public', './public'],
  viteFinal: (viteConfig) =>
    mergeConfig(viteConfig, {
      resolve: {
        // Pages get a signed-in Clerk stand-in; see src/mocks/clerk.tsx.
        alias: [
          {
            find: /^@clerk\/clerk-react$/,
            replacement: path.resolve(here, '../src/mocks/clerk.tsx'),
          },
        ],
      },
    }),
};
export default config;
