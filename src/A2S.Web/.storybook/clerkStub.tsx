/* eslint-disable react-refresh/only-export-components -- mirrors a library module that mixes hooks and components */
/**
 * Storybook-only stand-in for `@clerk/clerk-react` (aliased in main.ts). Stories must render
 * offline with no real publishable key, so this gives pages a signed-in "Joe" and a plain
 * avatar in place of Clerk's UserButton. Only the members the app imports are provided.
 */

import type { ReactNode } from 'react';

const user = { id: 'user_story', firstName: 'Joe', lastName: 'Story', fullName: 'Joe Story', primaryEmailAddress: { emailAddress: 'joe@example.com' } };

export function ClerkProvider({ children }: { children: ReactNode; publishableKey?: string }) {
  return <>{children}</>;
}

export function useUser() {
  return { isLoaded: true, isSignedIn: true, user };
}

export function useAuth() {
  return {
    isLoaded: true,
    isSignedIn: true,
    userId: user.id,
    sessionId: 'sess_story',
    getToken: async () => 'story-token',
    signOut: async () => {},
  };
}

export function useClerk() {
  return { signOut: async () => {}, openUserProfile: () => {} };
}

export function SignedIn({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

export function SignedOut() {
  return null;
}

export function UserButton({ appearance }: { afterSignOutUrl?: string; appearance?: { elements?: { avatarBox?: string } } }) {
  return (
    <span
      className={appearance?.elements?.avatarBox ?? 'h-11 w-11 rounded-md border border-border'}
      style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', background: 'var(--color-muted)', color: 'var(--color-foreground)', fontWeight: 600 }}
      aria-label="Account"
    >
      J
    </span>
  );
}

export function SignIn() {
  return <div>Sign in (Storybook stub)</div>;
}

export function SignUp() {
  return <div>Sign up (Storybook stub)</div>;
}
