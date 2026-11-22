import { SignUp } from '@clerk/clerk-react';
import { clerkAppearance } from './clerkAppearance';
import { AuthLayout } from './AuthLayout';

/** Sign-up screen: Clerk's sign-up form on our own origin, inside the shared phone-first frame. */
export function SignUpPage() {
  return (
    <AuthLayout title="99 Strength" subtitle="Create your account to get started">
      <SignUp routing="path" path="/sign-up" signInUrl="/sign-in" fallbackRedirectUrl="/today" appearance={clerkAppearance} />
    </AuthLayout>
  );
}
