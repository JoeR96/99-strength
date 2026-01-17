import { UserButton, useUser } from '@clerk/clerk-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

/**
 * Modern dashboard with mosaic-style layout using Golden Twilight theme.
 * Layout is designed to be built out gradually with workout tracking features.
 */
export function DashboardPage() {
  const { user } = useUser();

  return (
    <div className="min-h-screen bg-background">
      {/* Navigation Bar */}
      <nav className="border-b border-border bg-card shadow-sm">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="flex h-16 justify-between items-center">
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-bold text-primary">A2S Workout Tracker</h1>
            </div>
            <div className="flex items-center gap-4">
              <span className="text-sm text-muted-foreground hidden sm:inline">
                {user?.firstName || 'User'}
              </span>
              <UserButton afterSignOutUrl="/sign-in" />
            </div>
          </div>
        </div>
      </nav>

      {/* Main Content - Mosaic Layout */}
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        {/* Welcome Header */}
        <div className="mb-8">
          <h2 className="text-3xl font-bold text-foreground">
            Welcome back, {user?.firstName || 'User'}!
          </h2>
          <p className="mt-2 text-muted-foreground">
            Track your strength training progress and crush your goals.
          </p>
        </div>

        {/* Mosaic Grid Layout */}
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {/* Quick Stats Card - Spans 2 columns on larger screens */}
          <Card className="md:col-span-2 lg:col-span-2">
            <CardHeader>
              <CardTitle>Quick Stats</CardTitle>
              <CardDescription>Your workout summary at a glance</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid gap-4 sm:grid-cols-3">
                <div className="rounded-lg border border-border bg-muted/20 p-4">
                  <div className="text-2xl font-bold text-primary">0</div>
                  <p className="text-sm text-muted-foreground">Total Workouts</p>
                </div>
                <div className="rounded-lg border border-border bg-muted/20 p-4">
                  <div className="text-2xl font-bold text-primary">0</div>
                  <p className="text-sm text-muted-foreground">This Week</p>
                </div>
                <div className="rounded-lg border border-border bg-muted/20 p-4">
                  <div className="text-2xl font-bold text-primary">0</div>
                  <p className="text-sm text-muted-foreground">Current Streak</p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Current Program Card */}
          <Card>
            <CardHeader>
              <CardTitle>Current Program</CardTitle>
              <CardDescription>Your active training plan</CardDescription>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground mb-4">
                No program selected yet
              </p>
              <Button className="w-full">
                Start A2S Program
              </Button>
            </CardContent>
          </Card>

          {/* Recent Activity Card */}
          <Card className="lg:col-span-2">
            <CardHeader>
              <CardTitle>Recent Activity</CardTitle>
              <CardDescription>Your latest workouts and progress</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="text-center py-8 text-muted-foreground">
                <p className="text-sm">No workouts logged yet</p>
                <p className="text-xs mt-2">Start your first workout to see your progress here</p>
              </div>
            </CardContent>
          </Card>

          {/* Next Workout Card */}
          <Card>
            <CardHeader>
              <CardTitle>Next Workout</CardTitle>
              <CardDescription>Upcoming session</CardDescription>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground mb-4">
                No upcoming workout scheduled
              </p>
              <Button variant="secondary" className="w-full">
                Log Workout
              </Button>
            </CardContent>
          </Card>

          {/* Personal Records Card */}
          <Card className="md:col-span-2 lg:col-span-3">
            <CardHeader>
              <CardTitle>Personal Records</CardTitle>
              <CardDescription>Your best lifts across all exercises</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="text-center py-8 text-muted-foreground">
                <p className="text-sm">No personal records yet</p>
                <p className="text-xs mt-2">Complete workouts to track your PRs</p>
              </div>
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}
