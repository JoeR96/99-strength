import { UserButton, useUser } from '@clerk/clerk-react';

/**
 * Simple dashboard page to verify authentication works.
 * This will be replaced with actual workout dashboard later.
 */
export function DashboardPage() {
  const { user } = useUser();

  return (
    <div className="min-h-screen bg-gray-50">
      <nav className="bg-white shadow">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="flex h-16 justify-between">
            <div className="flex items-center">
              <h1 className="text-xl font-bold text-gray-900">A2S Workout Tracker</h1>
            </div>
            <div className="flex items-center">
              <UserButton afterSignOutUrl="/sign-in" />
            </div>
          </div>
        </div>
      </nav>

      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="rounded-lg bg-white p-6 shadow">
          <h2 className="text-2xl font-bold text-gray-900">
            Welcome, {user?.firstName || 'User'}!
          </h2>
          <p className="mt-4 text-gray-600">
            You are successfully authenticated. Dashboard features will be added in later phases.
          </p>
          <div className="mt-6">
            <h3 className="text-lg font-semibold text-gray-900">User Info:</h3>
            <dl className="mt-2 space-y-2">
              <div>
                <dt className="inline font-medium text-gray-700">Email:</dt>
                <dd className="inline ml-2 text-gray-600">
                  {user?.primaryEmailAddress?.emailAddress}
                </dd>
              </div>
              <div>
                <dt className="inline font-medium text-gray-700">User ID:</dt>
                <dd className="inline ml-2 text-gray-600">{user?.id}</dd>
              </div>
            </dl>
          </div>
        </div>
      </main>
    </div>
  );
}
