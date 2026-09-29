/* eslint-disable react-refresh/only-export-components -- Storybook decorator module */
/**
 * App providers for Storybook, applied globally in `.storybook/preview.tsx`:
 * a fresh QueryClient per story, HevyProvider, a MemoryRouter at the story's route,
 * the toast host, and (for the auth stories) the real ClerkProvider.
 *
 * Story parameters:
 *   route: { path: '/workout/session/:day', url: '/workout/session/2', state?: unknown }
 *   clerk: 'real'   // wrap in Clerk's provider so <SignIn>/<SignUp> render
 */
import { useState, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { ClerkProvider } from '@clerk/clerk-react';
import { HevyProvider } from '@/contexts/HevyContext';

/**
 * Publishable key for the Clerk development instance the API trusts in development
 * (`Clerk:Domain` = cosmic-treefrog-30.clerk.accounts.dev in appsettings.Development.json).
 * A publishable key is base64("<frontend api host>$") and is public by design.
 * Override with VITE_CLERK_PUBLISHABLE_KEY.
 */
export const STORYBOOK_CLERK_KEY =
  import.meta.env.VITE_CLERK_PUBLISHABLE_KEY ||
  'pk_test_Y29zbWljLXRyZWVmcm9nLTMwLmNsZXJrLmFjY291bnRzLmRldiQ';

export interface RouteParameter {
  /** Route pattern the page expects (for useParams). */
  path: string;
  /** Concrete URL to start at; defaults to `path`. */
  url?: string;
  /** Optional history state (e.g. data handed over by navigate()). */
  state?: unknown;
}

function freshQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  });
}

export function AppProviders({
  route,
  clerk,
  children,
}: {
  route?: RouteParameter;
  clerk?: 'real';
  children: ReactNode;
}) {
  const [client] = useState(freshQueryClient);
  const path = route?.path ?? '/';
  const url = route?.url ?? path;

  const tree = (
    <QueryClientProvider client={client}>
      <HevyProvider>
        <MemoryRouter initialEntries={[{ pathname: url, state: route?.state ?? null }]}>
          <Routes>
            <Route path={path} element={children} />
            <Route path="*" element={children} />
          </Routes>
        </MemoryRouter>
        <Toaster
          position="bottom-right"
          toastOptions={{
            className: 'bg-background text-foreground border border-border',
            duration: 4000,
          }}
        />
      </HevyProvider>
    </QueryClientProvider>
  );

  return clerk === 'real' ? (
    <ClerkProvider publishableKey={STORYBOOK_CLERK_KEY} afterSignOutUrl="/sign-in">
      {tree}
    </ClerkProvider>
  ) : (
    tree
  );
}

/** Forget any half-logged session so the recovery modal never pops up uninvited. */
export function resetBrowserState() {
  try {
    localStorage.removeItem('workout_progress');
  } catch {
    // storage blocked — nothing to reset
  }
}
