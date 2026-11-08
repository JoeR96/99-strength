/**
 * Shared Storybook decorators for signed-in pages: a fresh QueryClient per story,
 * a MemoryRouter and a placeholder ClerkProvider (the Navbar reads `useUser`).
 * `withApiMocks(setup)` runs the handler installers before the story renders.
 */

import type { Decorator } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ClerkProvider } from '@clerk/clerk-react';
import { Toaster } from 'react-hot-toast';

export function createStoryQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
      mutations: { retry: false },
    },
  });
}

export function withAppProviders(initialPath = '/today'): Decorator {
  return function AppProvidersDecorator(Story) {
    const queryClient = createStoryQueryClient();
    return (
      <ClerkProvider publishableKey="pk_test_placeholder">
        <MemoryRouter initialEntries={[initialPath]}>
          <QueryClientProvider client={queryClient}>
            <Story />
            <Toaster position="bottom-center" />
          </QueryClientProvider>
        </MemoryRouter>
      </ClerkProvider>
    );
  };
}

/** Installs API stubs synchronously before the story tree mounts. */
export function withApiMocks(setup: () => void): Decorator {
  return function ApiMocksDecorator(Story) {
    setup();
    return <Story />;
  };
}

/** Phone-first viewport parameters for page stories. */
export const phoneParameters = {
  layout: 'fullscreen',
  viewport: { defaultViewport: 'mobile2' },
} as const;
