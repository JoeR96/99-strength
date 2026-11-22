import { SignIn } from '@clerk/clerk-react';
import { clerkAppearance } from './clerkAppearance';
import { AuthLayout } from './AuthLayout';

/** Sign-in screen: Clerk's sign-in form on our own origin, inside the shared phone-first frame. */
export function LoginPage() {
  return (
    <AuthLayout
      title="99 Strength"
      subtitle="Your whole training plan in one place"
      hint="Sign in to see what's on today"
      footer="Lifts, cardio, mobility, habits and weight"
    >
      <SignIn routing="path" path="/sign-in" signUpUrl="/sign-up" fallbackRedirectUrl="/today" appearance={clerkAppearance} />
    </AuthLayout>
  );
}
