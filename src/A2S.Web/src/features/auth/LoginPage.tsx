import { SignIn } from '@clerk/clerk-react';

/**
 * Login page component that displays Clerk's pre-built sign-in UI with SSO options.
 */
export function LoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-gray-50 to-gray-100">
      <div className="w-full max-w-md px-4">
        <div className="mb-8 text-center">
          <h1 className="text-4xl font-bold text-gray-900">A2S Workout Tracker</h1>
          <p className="mt-2 text-gray-600">Sign in to track your progress</p>
        </div>
        <SignIn
          routing="path"
          path="/sign-in"
          signUpUrl="/sign-up"
          fallbackRedirectUrl="/dashboard"
        />
      </div>
    </div>
  );
}
