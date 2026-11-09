import { fileURLToPath } from 'node:url';
import type { StorybookConfig } from '@storybook/react-vite';

const config: StorybookConfig = {
  "stories": [
    "../src/**/*.mdx",
    "../src/**/*.stories.@(js|jsx|mjs|ts|tsx)"
  ],
  "addons": [
    "@chromatic-com/storybook",
    "@storybook/addon-vitest",
    "@storybook/addon-a11y",
    "@storybook/addon-docs",
    "@storybook/addon-onboarding"
  ],
  "framework": "@storybook/react-vite",
  // 1. The app's vite.config.ts registers vite-plugin-pwa. Storybook has no use for a service
  //    worker, and Workbox refuses to precache the 3 MB manager bundle, which fails the build.
  // 2. Clerk is replaced by an offline stub: a real publishable key is not available in stories
  //    and Clerk throws on a placeholder, so no page with the Navbar could render.
  async viteFinal(viteConfig) {
    const isPwaPlugin = (plugin: unknown) =>
      typeof plugin === 'object' && plugin !== null && 'name' in plugin &&
      String((plugin as { name: unknown }).name).startsWith('vite-plugin-pwa');
    const strip = (plugins: unknown[]): unknown[] =>
      plugins.flatMap((plugin) => (Array.isArray(plugin) ? [strip(plugin)] : isPwaPlugin(plugin) ? [] : [plugin]));
    const existingAlias = viteConfig.resolve?.alias;
    const aliasEntries = Array.isArray(existingAlias)
      ? existingAlias
      : Object.entries(existingAlias ?? {}).map(([find, replacement]) => ({ find, replacement }));
    return {
      ...viteConfig,
      plugins: strip((viteConfig.plugins ?? []) as unknown[]) as typeof viteConfig.plugins,
      resolve: {
        ...viteConfig.resolve,
        alias: [
          ...aliasEntries,
          { find: '@clerk/clerk-react', replacement: fileURLToPath(new URL('./clerkStub.tsx', import.meta.url)) },
        ],
      },
    };
  },
};
export default config;
