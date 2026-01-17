import { SignIn } from '@clerk/clerk-react';

/**
 * Login page component that displays Clerk's pre-built sign-in UI with SSO options.
 * Uses Golden Twilight theme with navy background and gold accents.
 */
export function LoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-ink-black via-prussian-blue to-oxford-navy">
      <div className="w-full max-w-md px-4">
        <div className="mb-8 text-center">
          <h1 className="text-4xl font-bold text-gold drop-shadow-lg">
            A2S Workout Tracker
          </h1>
          <p className="mt-2 text-school-bus-yellow/90">
            Sign in to track your progress
          </p>
        </div>
        <SignIn
          routing="path"
          path="/sign-in"
          signUpUrl="/sign-up"
          fallbackRedirectUrl="/dashboard"
          appearance={{
            elements: {
              rootBox: "mx-auto",
              card: "bg-card shadow-2xl border-oxford-navy/50",
            },
          }}
        />
      </div>
    </div>
  );
}
