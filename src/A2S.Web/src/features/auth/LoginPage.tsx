import { SignIn } from '@clerk/clerk-react';
import { AuthShell } from './AuthShell';
import { clerkAppearance } from './clerkAppearance';

/**
 * Sign-in page: Clerk's pre-built sign-in (email + SSO) inside the shared auth shell.
 */
export function LoginPage() {
  return (
    <AuthShell subtitle="Sign in to pick up this week's training.">
      <SignIn
        routing="path"
        path="/sign-in"
        signUpUrl="/sign-up"
        fallbackRedirectUrl="/dashboard"
        appearance={clerkAppearance}
      />
    </AuthShell>
  );
}
