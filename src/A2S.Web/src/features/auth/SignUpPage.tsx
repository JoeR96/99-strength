import { SignUp } from '@clerk/clerk-react';
import { clerkAppearance } from './clerkAppearance';

/**
 * Sign-up page component that displays Clerk's pre-built sign-up UI.
 */
export function SignUpPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background text-foreground">
      <div className="w-full max-w-md px-4">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold text-foreground">99 Strength</h1>
          <p className="mt-2 text-muted-foreground">Create your account to get started</p>
        </div>
        <div className="rounded-2xl border border-primary/10 bg-card/80 backdrop-blur-xl p-1 shadow-2xl shadow-black/20">
          <SignUp
            routing="path"
            path="/sign-up"
            signInUrl="/sign-in"
            fallbackRedirectUrl="/dashboard"
            appearance={clerkAppearance}
          />
        </div>
      </div>
    </div>
  );
}
