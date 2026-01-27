import { useUser } from '@clerk/clerk-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Link, useNavigate } from 'react-router-dom';
import { Navbar } from '@/components/layout/Navbar';
import { useCurrentWorkout } from '@/hooks/useWorkouts';
import { WeekOverview } from '@/features/workout/WeekOverview';
import { NextWeekPreview } from '@/features/workout/NextWeekPreview';

/**
 * Modern dashboard with mosaic-style layout using Golden Twilight theme.
 * Shows active program details and this week's training schedule.
 */
export function DashboardPage() {
  const { user } = useUser();
  const navigate = useNavigate();
  const { data: workout, isLoading, refetch } = useCurrentWorkout();

  const daysPerWeek = workout?.daysPerWeek || 4;

  // Track completed days and current day
  const completedDays = new Set(workout?.completedDaysInCurrentWeek || []);
  const currentDay = workout?.currentDay || 1;

  // Calculate real stats
  const totalWorkoutsCompleted = workout
    ? ((workout.currentWeek - 1) * daysPerWeek) + completedDays.size
    : 0;
  const thisWeekCompleted = completedDays.size;

  return (
    <div className="min-h-screen bg-background theme-transition">
      <Navbar />

      {/* Main Content - Apple Layout with generous whitespace */}
      <main className="container-apple py-16">
        {/* Welcome Header - Apple hero style */}
        <div className="mb-16 text-center">
          <h2 className="text-hero text-foreground mb-4">
            Welcome back, {user?.firstName || 'User'}
          </h2>
          <p className="text-body text-muted-foreground max-w-2xl mx-auto">
            Track your strength training progress and achieve your fitness goals.
          </p>
        </div>

        {/* Apple Grid Layout - clean and spacious */}
        <div className="grid gap-8 md:grid-cols-2 lg:grid-cols-3">
          {/* Quick Stats Card - Spans 2 columns on larger screens */}
          <Card className="md:col-span-2 lg:col-span-2 overflow-hidden">
            <CardHeader className="pb-4">
              <CardTitle className="flex items-center gap-2">
                <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                </svg>
                Quick Stats
              </CardTitle>
              <CardDescription>Your workout summary at a glance</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid gap-6 sm:grid-cols-3">
                <div className="group relative overflow-hidden rounded-2xl bg-muted/30 p-6 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:bg-muted/50">
                  <div className="text-4xl font-semibold text-primary mb-2">{totalWorkoutsCompleted}</div>
                  <p className="text-caption text-muted-foreground">Total Workouts</p>
                </div>
                <div className="group relative overflow-hidden rounded-2xl bg-muted/30 p-6 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:bg-muted/50">
                  <div className="text-4xl font-semibold text-primary mb-2">{thisWeekCompleted}/{daysPerWeek}</div>
                  <p className="text-caption text-muted-foreground">This Week</p>
                </div>
                <div className="group relative overflow-hidden rounded-2xl bg-muted/30 p-6 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:bg-muted/50">
                  <div className="text-4xl font-semibold text-primary mb-2">{totalWorkoutsCompleted}</div>
                  <p className="text-caption text-muted-foreground">Workouts Done</p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Current Program Card */}
          <Card className="group overflow-hidden">
            <CardHeader className="pb-4">
              <CardTitle className="flex items-center gap-2">
                <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                </svg>
                Current Program
              </CardTitle>
              <CardDescription>Your active training plan</CardDescription>
            </CardHeader>
            <CardContent>
              {isLoading ? (
                <div className="flex items-center justify-center py-8">
                  <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
                </div>
              ) : workout ? (
                <div className="space-y-6">
                  <div className="rounded-2xl bg-muted/30 p-6">
                    <h3 className="font-semibold text-xl text-foreground mb-1">{workout.name}</h3>
                    <p className="text-caption text-muted-foreground">
                      {workout.daysPerWeek}-Day Split
                    </p>
                    {/* Prominent Week/Day Display */}
                    <div className="mt-5 p-5 bg-primary/5 rounded-xl">
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-3xl font-semibold text-primary">Week {workout.currentWeek}</div>
                          <div className="text-caption text-muted-foreground mt-1">Day {currentDay} of {daysPerWeek}</div>
                        </div>
                        <div className="text-right">
                          <div className="text-caption text-muted-foreground">Block {workout.currentBlock}</div>
                          <div className="text-caption text-muted-foreground">{workout.currentWeek}/{workout.totalWeeks} weeks</div>
                        </div>
                      </div>
                    </div>
                    <div className="mt-5">
                      <div className="flex justify-between text-caption text-muted-foreground mb-2">
                        <span>Overall Progress</span>
                        <span>{Math.round((workout.currentWeek / workout.totalWeeks) * 100)}%</span>
                      </div>
                      <div className="h-1.5 bg-muted rounded-full overflow-hidden">
                        <div
                          className="h-full bg-primary transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]"
                          style={{ width: `${(workout.currentWeek / workout.totalWeeks) * 100}%` }}
                        />
                      </div>
                    </div>
                  </div>
                  <div className="space-y-3">
                    <Link to={`/workout/session/${currentDay}`}>
                      <Button className="w-full" data-testid="start-current-workout">
                        Start W{workout.currentWeek} D{currentDay} Workout
                      </Button>
                    </Link>
                  </div>
                </div>
              ) : (
                <>
                  <div className="mb-6 flex items-center justify-center rounded-2xl border border-dashed border-border bg-muted/20 py-12 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]">
                    <p className="text-body text-muted-foreground">No program selected</p>
                  </div>
                  <Button className="w-full" onClick={() => navigate('/setup')}>
                    Start A2S Program
                  </Button>
                </>
              )}
            </CardContent>
          </Card>

          {/* This Week's Training - Uses shared WeekOverview component */}
          {workout && (
            <div className="md:col-span-2 lg:col-span-3">
              <WeekOverview workout={workout} onWorkoutUpdated={refetch} />
            </div>
          )}

          {/* No workout placeholder */}
          {!workout && !isLoading && (
            <Card className="md:col-span-2 lg:col-span-3 overflow-hidden">
              <CardHeader className="pb-4">
                <CardTitle className="flex items-center gap-2">
                  <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                  </svg>
                  This Week's Training
                </CardTitle>
                <CardDescription>Your weekly schedule</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-border/50 bg-muted/10 py-12">
                  <svg className="h-12 w-12 text-muted-foreground/30 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                  </svg>
                  <p className="text-sm font-medium text-muted-foreground">No scheduled workouts</p>
                  <p className="text-xs text-muted-foreground/70 mt-1">Create a program to see your weekly schedule</p>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Next Week Preview - Uses shared component with blur logic */}
          {workout && workout.currentWeek < workout.totalWeeks && (
            <div className="md:col-span-2 lg:col-span-3">
              <NextWeekPreview workout={workout} />
            </div>
          )}

          {/* Personal Records Card */}
          <Card className="md:col-span-2 lg:col-span-3 overflow-hidden">
            <CardHeader className="pb-4">
              <CardTitle className="flex items-center gap-2">
                <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z" />
                </svg>
                Personal Records
              </CardTitle>
              <CardDescription>Your best lifts across all exercises</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-border/50 bg-muted/10 py-12">
                <svg className="h-12 w-12 text-muted-foreground/30 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12l2 2 4-4M7.835 4.697a3.42 3.42 0 001.946-.806 3.42 3.42 0 014.438 0 3.42 3.42 0 001.946.806 3.42 3.42 0 013.138 3.138 3.42 3.42 0 00.806 1.946 3.42 3.42 0 010 4.438 3.42 3.42 0 00-.806 1.946 3.42 3.42 0 01-3.138 3.138 3.42 3.42 0 00-1.946.806 3.42 3.42 0 01-4.438 0 3.42 3.42 0 00-1.946-.806 3.42 3.42 0 01-3.138-3.138 3.42 3.42 0 00-.806-1.946 3.42 3.42 0 010-4.438 3.42 3.42 0 00.806-1.946 3.42 3.42 0 013.138-3.138z" />
                </svg>
                <p className="text-sm font-medium text-muted-foreground">No personal records yet</p>
                <p className="text-xs text-muted-foreground/70 mt-1">Complete workouts to track your PRs</p>
              </div>
            </CardContent>
          </Card>

        </div>
      </main>

      {/* Footer */}
      <footer className="mt-auto border-t border-border/50 bg-card/50">
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
          <div className="flex flex-col items-center justify-between gap-4 sm:flex-row">
            <p className="text-sm text-muted-foreground">
              A2S Workout Tracker - Built for strength athletes
            </p>
            <div className="flex items-center gap-4">
              <a href="#" className="text-sm text-muted-foreground hover:text-primary transition-colors">Help</a>
              <a href="#" className="text-sm text-muted-foreground hover:text-primary transition-colors">Privacy</a>
              <a href="#" className="text-sm text-muted-foreground hover:text-primary transition-colors">Terms</a>
            </div>
          </div>
        </div>
      </footer>
    </div>
  );
}
