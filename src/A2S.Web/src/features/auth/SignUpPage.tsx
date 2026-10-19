import { SignUp } from '@clerk/clerk-react';
import { AuthShell } from './AuthShell';
import { clerkAppearance } from './clerkAppearance';

/**
 * Sign-up page: Clerk's pre-built sign-up inside the shared auth shell.
 *
 * `routing="hash"` lets the widget render outside /sign-up (Storybook's iframe).
 */
export function SignUpPage({ routing = 'path' }: { routing?: 'path' | 'hash' } = {}) {
  return (
    <AuthShell subtitle="Create an account and set up your first program.">
      <SignUp
        {...(routing === 'path' ? { routing: 'path' as const, path: '/sign-up' } : { routing: 'hash' as const })}
        signInUrl="/sign-in"
        fallbackRedirectUrl="/dashboard"
        appearance={clerkAppearance}
      />
    </AuthShell>
  );
}
