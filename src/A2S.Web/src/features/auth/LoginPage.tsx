import { SignIn } from '@clerk/clerk-react';
import { AuthShell } from './AuthShell';
import { clerkAppearance } from './clerkAppearance';

/**
 * Sign-in page: Clerk's pre-built sign-in (email + SSO) inside the shared auth shell.
 *
 * `routing="hash"` lets the widget render outside /sign-in (Storybook's iframe).
 */
export function LoginPage({ routing = 'path' }: { routing?: 'path' | 'hash' } = {}) {
  return (
    <AuthShell subtitle="Sign in to pick up this week's training.">
      <SignIn
        {...(routing === 'path' ? { routing: 'path' as const, path: '/sign-in' } : { routing: 'hash' as const })}
        signUpUrl="/sign-up"
        fallbackRedirectUrl="/dashboard"
        appearance={clerkAppearance}
      />
    </AuthShell>
  );
}
