import { SignUp } from '@clerk/clerk-react';

/**
 * Sign-up page component that displays Clerk's pre-built sign-up UI.
 */
export function SignUpPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold text-gray-900">A2S Workout Tracker</h1>
          <p className="mt-2 text-gray-600">Create your account to get started</p>
        </div>
        <SignUp
          routing="path"
          path="/sign-up"
          signInUrl="/sign-in"
          fallbackRedirectUrl="/dashboard"
        />
      </div>
    </div>
  );
}
