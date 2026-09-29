/* eslint-disable react-refresh/only-export-components -- module stand-in, not a component file */
/**
 * Storybook stand-in for `@clerk/clerk-react` (wired by a Vite alias in
 * `.storybook/main.ts`). App pages see a signed-in user without a network round trip;
 * the real `ClerkProvider`, `SignIn` and `SignUp` are passed through so the sign-in and
 * sign-up stories render Clerk's actual widget.
 */
import type { ReactNode } from 'react';
// Relative path on purpose: the bare specifier is aliased to this file.
import {
  ClerkProvider as RealClerkProvider,
  SignIn as RealSignIn,
  SignUp as RealSignUp,
} from '../../node_modules/@clerk/clerk-react/dist/index.mjs';

export const ClerkProvider = RealClerkProvider;
export const SignIn = RealSignIn;
export const SignUp = RealSignUp;

export const mockUser = {
  id: 'user_2m9Qk',
  firstName: 'Joe',
  lastName: 'Richardson',
  fullName: 'Joe Richardson',
  primaryEmailAddress: { emailAddress: 'joe@example.com' },
  imageUrl: '',
};

export function useUser() {
  return { isLoaded: true, isSignedIn: true, user: mockUser };
}

export function useAuth() {
  return {
    isLoaded: true,
    isSignedIn: true,
    userId: mockUser.id,
    sessionId: 'sess_storybook',
    getToken: async () => 'storybook-token',
    signOut: async () => {},
  };
}

export function SignedIn({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

export function SignedOut() {
  return null;
}

/** Looks like Clerk's avatar button (initials on the muted surface). */
export function UserButton() {
  return (
    <button
      type="button"
      aria-label="Open user menu"
      className="flex h-11 w-11 items-center justify-center rounded-md border border-border bg-muted text-sm font-semibold text-foreground"
    >
      JR
    </button>
  );
}
