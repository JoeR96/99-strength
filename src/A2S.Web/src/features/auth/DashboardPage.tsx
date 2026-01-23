import { useUser } from '@clerk/clerk-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { useNavigate } from 'react-router-dom';
import { Navbar } from '@/components/layout/Navbar';
import { useCurrentWorkout } from '@/hooks/useWorkouts';
import { WeightUnit, type ExerciseDto, type LinearProgressionDto, type RepsPerSetProgressionDto } from '@/types/workout';

/**
 * Modern dashboard with mosaic-style layout using Golden Twilight theme.
 * Shows active program details and this week's training schedule.
 */
export function DashboardPage() {
  const { user } = useUser();
  const navigate = useNavigate();
  const { data: workout, isLoading } = useCurrentWorkout();

  // Group exercises by day for the week view
  const exercisesByDay = workout?.exercises?.reduce((acc, exercise) => {
    const day = exercise.assignedDay;
    if (!acc[day]) {
      acc[day] = [];
    }
    acc[day].push(exercise);
    return acc;
  }, {} as Record<number, ExerciseDto[]>) || {};

  const days = Object.keys(exercisesByDay).map(Number).sort((a, b) => a - b);
  const dayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

  return (
    <div className="min-h-screen bg-background">
      <Navbar />

      {/* Main Content - Mosaic Layout */}
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        {/* Welcome Header */}
        <div className="mb-10">
          <h2 className="text-4xl font-bold text-foreground tracking-tight">
            Welcome back, <span className="text-primary">{user?.firstName || 'User'}</span>
          </h2>
          <p className="mt-3 text-lg text-muted-foreground">
            Track your strength training progress and crush your goals.
          </p>
        </div>

        {/* Mosaic Grid Layout */}
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
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
              <div className="grid gap-4 sm:grid-cols-3">
                <div className="group relative overflow-hidden rounded-xl border border-border/50 bg-gradient-to-br from-muted/30 to-muted/10 p-5 transition-all hover:border-primary/30 hover:shadow-md">
                  <div className="text-3xl font-black text-primary">0</div>
                  <p className="text-sm font-medium text-muted-foreground mt-1">Total Workouts</p>
                  <div className="absolute -right-4 -top-4 h-16 w-16 rounded-full bg-primary/5 transition-transform group-hover:scale-150" />
                </div>
                <div className="group relative overflow-hidden rounded-xl border border-border/50 bg-gradient-to-br from-muted/30 to-muted/10 p-5 transition-all hover:border-primary/30 hover:shadow-md">
                  <div className="text-3xl font-black text-primary">0</div>
                  <p className="text-sm font-medium text-muted-foreground mt-1">This Week</p>
                  <div className="absolute -right-4 -top-4 h-16 w-16 rounded-full bg-primary/5 transition-transform group-hover:scale-150" />
                </div>
                <div className="group relative overflow-hidden rounded-xl border border-border/50 bg-gradient-to-br from-muted/30 to-muted/10 p-5 transition-all hover:border-primary/30 hover:shadow-md">
                  <div className="text-3xl font-black text-primary">0</div>
                  <p className="text-sm font-medium text-muted-foreground mt-1">Current Streak</p>
                  <div className="absolute -right-4 -top-4 h-16 w-16 rounded-full bg-primary/5 transition-transform group-hover:scale-150" />
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
                <div className="space-y-4">
                  <div className="rounded-xl border border-primary/30 bg-primary/5 p-4">
                    <h3 className="font-bold text-lg text-foreground">{workout.name}</h3>
                    <p className="text-sm text-muted-foreground mt-1">
                      {workout.variant}-Day Split | Week {workout.currentWeek}/{workout.totalWeeks}
                    </p>
                    <div className="mt-3">
                      <div className="flex justify-between text-xs text-muted-foreground mb-1">
                        <span>Progress</span>
                        <span>{Math.round((workout.currentWeek / workout.totalWeeks) * 100)}%</span>
                      </div>
                      <div className="h-2 bg-muted rounded-full overflow-hidden">
                        <div
                          className="h-full bg-primary transition-all duration-300"
                          style={{ width: `${(workout.currentWeek / workout.totalWeeks) * 100}%` }}
                        />
                      </div>
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Button variant="glow" className="flex-1" onClick={() => navigate('/workout')}>
                      View Workout
                    </Button>
                    <Button variant="outline" className="flex-1" onClick={() => navigate('/programs')}>
                      Manage
                    </Button>
                  </div>
                </div>
              ) : (
                <>
                  <div className="mb-5 flex items-center justify-center rounded-xl border-2 border-dashed border-border/50 bg-muted/20 py-8 transition-colors group-hover:border-primary/30">
                    <p className="text-sm text-muted-foreground">No program selected</p>
                  </div>
                  <Button variant="glow" className="w-full" onClick={() => navigate('/setup')}>
                    Start A2S Program
                  </Button>
                </>
              )}
            </CardContent>
          </Card>

          {/* This Week's Training - Full Width */}
          <Card className="md:col-span-2 lg:col-span-3 overflow-hidden">
            <CardHeader className="pb-4">
              <CardTitle className="flex items-center gap-2">
                <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                </svg>
                This Week's Training
              </CardTitle>
              <CardDescription>
                {workout ? `Week ${workout.currentWeek} - Block ${Math.ceil(workout.currentWeek / 7)}` : 'Your weekly schedule'}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {workout && days.length > 0 ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4">
                  {days.map((day) => (
                    <div
                      key={day}
                      className="p-4 border rounded-lg border-border bg-card hover:border-primary/30 transition-colors"
                    >
                      <h3 className="font-semibold text-foreground mb-3">{dayNames[day - 1] || `Day ${day}`}</h3>
                      <div className="space-y-3">
                        {exercisesByDay[day]
                          .sort((a, b) => a.orderInDay - b.orderInDay)
                          .map((exercise) => (
                            <ExerciseDetail key={exercise.id} exercise={exercise} />
                          ))}
                      </div>
                      <Button variant="outline" size="sm" className="w-full mt-4">
                        Start Workout
                      </Button>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-border/50 bg-muted/10 py-12">
                  <svg className="h-12 w-12 text-muted-foreground/30 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                  </svg>
                  <p className="text-sm font-medium text-muted-foreground">No scheduled workouts</p>
                  <p className="text-xs text-muted-foreground/70 mt-1">Create a program to see your weekly schedule</p>
                </div>
              )}
            </CardContent>
          </Card>

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

/**
 * Displays full exercise details including all progression parameters
 */
function ExerciseDetail({ exercise }: { exercise: ExerciseDto }) {
  const isLinear = exercise.progression.type === 'Linear';
  const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
  const repsPerSetProg = !isLinear ? (exercise.progression as RepsPerSetProgressionDto) : null;

  const weightUnit = linearProg?.trainingMax?.unit === WeightUnit.Pounds ? 'lbs' : 'kg';
  const repsWeightUnit = repsPerSetProg?.weightUnit?.toLowerCase() === 'pounds' ? 'lbs' : 'kg';

  return (
    <div className="border-l-2 border-primary/30 pl-3 py-1">
      <div className="font-medium text-sm text-foreground">{exercise.name}</div>

      {linearProg && (
        <div className="text-xs text-muted-foreground mt-1 space-y-0.5">
          <div className="flex justify-between">
            <span>Training Max:</span>
            <span className="font-medium text-foreground">{linearProg.trainingMax.value} {weightUnit}</span>
          </div>
          <div className="flex justify-between">
            <span>Sets:</span>
            <span className="font-medium text-foreground">{linearProg.baseSetsPerExercise} sets{linearProg.useAmrap ? ' + AMRAP' : ''}</span>
          </div>
          {linearProg.useAmrap && (
            <div className="text-primary/80 text-[10px] font-medium mt-1">
              AMRAP on last set
            </div>
          )}
        </div>
      )}

      {repsPerSetProg && (
        <div className="text-xs text-muted-foreground mt-1 space-y-0.5">
          <div className="flex justify-between">
            <span>Weight:</span>
            <span className="font-medium text-foreground">{repsPerSetProg.currentWeight} {repsWeightUnit}</span>
          </div>
          <div className="flex justify-between">
            <span>Sets:</span>
            <span className="font-medium text-foreground">{repsPerSetProg.currentSetCount} / {repsPerSetProg.targetSets} target</span>
          </div>
          <div className="flex justify-between">
            <span>Reps:</span>
            <span className="font-medium text-foreground">
              {repsPerSetProg.repRange.minimum}-{repsPerSetProg.repRange.target}-{repsPerSetProg.repRange.maximum}
            </span>
          </div>
        </div>
      )}
    </div>
  );
}
