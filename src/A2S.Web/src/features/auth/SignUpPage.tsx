import { SignUp } from '@clerk/clerk-react';
import { AuthShell } from './AuthShell';
import { clerkAppearance } from './clerkAppearance';

/**
 * Sign-up page: Clerk's pre-built sign-up inside the shared auth shell.
 */
export function SignUpPage() {
  return (
    <AuthShell subtitle="Create an account and set up your first program.">
      <SignUp
        routing="path"
        path="/sign-up"
        signInUrl="/sign-in"
        fallbackRedirectUrl="/dashboard"
        appearance={clerkAppearance}
      />
    </AuthShell>
  );
}
