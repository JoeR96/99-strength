import type { Preview } from '@storybook/react-vite';
import { setupWorker } from 'msw/browser';
import { mswLoader } from 'msw-storybook-addon/csf3';
import { handlers } from '../src/mocks/handlers';
import { AppProviders, resetBrowserState, type RouteParameter } from '../src/mocks/storybook';
import '../src/index.css'; // Tailwind + Arcade Minimal tokens

// Mock Service Worker: every story talks to the handlers in src/mocks, never a real API.
// Unhandled requests (Clerk's own traffic on the auth stories) go to the network.
const startWorker = async () => {
  const worker = setupWorker();
  await worker.start({
    onUnhandledRequest: 'bypass',
    quiet: true,
    serviceWorker: { url: './mockServiceWorker.js' },
  });
  return worker;
};

const preview: Preview = {
  parameters: {
    layout: 'fullscreen',
    backgrounds: { disable: true },
    msw: { handlers },
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    a11y: {
      // WCAG 2.x A/AA, the same tags the app's audits use.
      options: { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } },
    },
  },
  loaders: [
    async () => {
      resetBrowserState();
      return {};
    },
    mswLoader(startWorker),
  ],
  decorators: [
    (Story, { parameters }) => (
      <AppProviders route={parameters.route as RouteParameter | undefined} clerk={parameters.clerk}>
        <Story />
      </AppProviders>
    ),
  ],
};

export default preview;
