import { useUser } from '@clerk/clerk-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Link, useNavigate } from 'react-router-dom';
import { Navbar } from '@/components/layout/Navbar';
import { useCurrentWorkout } from '@/hooks/useWorkouts';
import { WeightUnit, type ExerciseDto, type LinearProgressionDto, type RepsPerSetProgressionDto, type MinimalSetsProgressionDto, type WorkoutDto } from '@/types/workout';

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

  const daysPerWeek = workout?.daysPerWeek || 4;
  const days = Array.from({ length: daysPerWeek }, (_, i) => i + 1);
  const dayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

  // Track completed days and current day
  const completedDays = new Set(workout?.completedDaysInCurrentWeek || []);
  const currentDay = workout?.currentDay || 1;

  // Calculate real stats
  const totalWorkoutsCompleted = workout
    ? ((workout.currentWeek - 1) * daysPerWeek) + completedDays.size
    : 0;
  const thisWeekCompleted = completedDays.size;

  // Check if a day is unlocked (only current day and completed days are accessible)
  const isDayUnlocked = (day: number) => {
    return completedDays.has(day) || day === currentDay;
  };

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
                  <Link to={`/workout/session/${currentDay}`}>
                    <Button className="w-full" data-testid="start-current-workout">
                      Start {dayNames[currentDay - 1]}'s Workout
                    </Button>
                  </Link>
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
                {workout ? (
                  <>
                    Week {workout.currentWeek} - Block {workout.currentBlock}
                    {workout.isWeekComplete && (
                      <span className="ml-2 text-green-500 font-medium">Week Complete!</span>
                    )}
                  </>
                ) : 'Your weekly schedule'}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {workout && days.length > 0 ? (
                <>
                  {/* Progress indicator */}
                  <div className="mb-4">
                    <div className="flex items-center gap-2 mb-2">
                      <span className="text-sm text-muted-foreground">Week Progress:</span>
                      <span className="text-sm font-medium">
                        {completedDays.size} / {daysPerWeek} days completed
                      </span>
                    </div>
                    <div className="flex gap-1">
                      {days.map((day) => (
                        <div
                          key={day}
                          className={`h-2 flex-1 rounded ${
                            completedDays.has(day)
                              ? "bg-green-500"
                              : day === currentDay
                              ? "bg-primary"
                              : "bg-muted"
                          }`}
                          title={`Day ${day}: ${completedDays.has(day) ? "Completed" : day === currentDay ? "Current" : "Locked"}`}
                        />
                      ))}
                    </div>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                    {days.map((day) => {
                      const isCompleted = completedDays.has(day);
                      const isCurrent = day === currentDay && !isCompleted;
                      const isLocked = !isDayUnlocked(day);

                      return (
                        <div
                          key={day}
                          className={`p-4 border rounded-lg transition-all ${
                            isCompleted
                              ? "border-green-500 bg-green-50 dark:bg-green-950"
                              : isCurrent
                              ? "border-primary bg-primary/5 ring-2 ring-primary/20"
                              : isLocked
                              ? "border-border bg-muted/30 opacity-60"
                              : "border-border bg-card hover:border-primary/30"
                          }`}
                          data-testid={`day-card-${day}`}
                        >
                          <div className="flex items-center justify-between mb-3">
                            <div>
                              <h3 className="font-semibold text-foreground">{dayNames[day - 1] || `Day ${day}`}</h3>
                              {isCurrent && (
                                <span className="text-xs text-primary font-medium">Current</span>
                              )}
                              {isLocked && (
                                <span className="text-xs text-muted-foreground">Locked</span>
                              )}
                            </div>
                            {isCompleted && (
                              <svg
                                className="w-5 h-5 text-green-500"
                                fill="currentColor"
                                viewBox="0 0 20 20"
                                data-testid={`day-${day}-completed-icon`}
                              >
                                <path
                                  fillRule="evenodd"
                                  d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                                  clipRule="evenodd"
                                />
                              </svg>
                            )}
                            {isLocked && (
                              <svg
                                className="w-5 h-5 text-muted-foreground"
                                fill="currentColor"
                                viewBox="0 0 20 20"
                              >
                                <path
                                  fillRule="evenodd"
                                  d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z"
                                  clipRule="evenodd"
                                />
                              </svg>
                            )}
                          </div>
                          <div className="space-y-3 mb-4">
                            {(exercisesByDay[day] || [])
                              .sort((a, b) => a.orderInDay - b.orderInDay)
                              .map((exercise) => (
                                <ExerciseDetail key={exercise.id} exercise={exercise} />
                              ))}
                          </div>
                          {isCompleted ? (
                            <Button
                              variant="outline"
                              size="sm"
                              className="w-full"
                              disabled
                              data-testid={`start-workout-day-${day}`}
                            >
                              Completed
                            </Button>
                          ) : isLocked ? (
                            <Button
                              variant="outline"
                              size="sm"
                              className="w-full"
                              disabled
                              data-testid={`start-workout-day-${day}`}
                            >
                              Locked
                            </Button>
                          ) : (
                            <Link to={`/workout/session/${day}`}>
                              <Button
                                variant={isCurrent ? "default" : "outline"}
                                size="sm"
                                className="w-full"
                                data-testid={`start-workout-day-${day}`}
                              >
                                {isCurrent ? "Start Workout" : "Start"}
                              </Button>
                            </Link>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </>
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

          {/* Next Week Preview - Shows after completing at least one day */}
          {workout && workout.currentWeek < workout.totalWeeks && (
            <NextWeekPreview workout={workout} />
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

/**
 * Displays full exercise details including all progression parameters
 */
function ExerciseDetail({ exercise }: { exercise: ExerciseDto }) {
  const isLinear = exercise.progression.type === 'Linear';
  const isRepsPerSet = exercise.progression.type === 'RepsPerSet';
  const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
  const repsPerSetProg = isRepsPerSet ? (exercise.progression as RepsPerSetProgressionDto) : null;
  const minimalSetsProg = exercise.progression.type === 'MinimalSets' ? (exercise.progression as MinimalSetsProgressionDto) : null;

  const weightUnit = linearProg?.trainingMax?.unit === WeightUnit.Pounds ? 'lbs' : 'kg';
  const repsWeightUnit = repsPerSetProg?.weightUnit?.toLowerCase() === 'pounds' ? 'lbs' : 'kg';
  const minimalWeightUnit = minimalSetsProg?.weightUnit?.toLowerCase() === 'pounds' ? 'lbs' : 'kg';

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

      {minimalSetsProg && (
        <div className="text-xs text-muted-foreground mt-1 space-y-0.5">
          <div className="flex justify-between">
            <span>Weight:</span>
            <span className="font-medium text-foreground">{minimalSetsProg.currentWeight} {minimalWeightUnit}</span>
          </div>
          <div className="flex justify-between">
            <span>Sets:</span>
            <span className="font-medium text-foreground">{minimalSetsProg.currentSetCount} ({minimalSetsProg.minimumSets}-{minimalSetsProg.maximumSets})</span>
          </div>
          <div className="flex justify-between">
            <span>Target Reps:</span>
            <span className="font-medium text-foreground">{minimalSetsProg.targetTotalReps}</span>
          </div>
        </div>
      )}
    </div>
  );
}

/**
 * Next Week Preview - Shows what's coming up next week
 */
function NextWeekPreview({ workout }: { workout: WorkoutDto }) {
  const nextWeek = workout.currentWeek + 1;
  const nextBlock = Math.ceil(nextWeek / 7);
  const isDeloadWeek = nextWeek % 7 === 0;
  const dayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const daysPerWeek = workout.daysPerWeek || 4;
  const days = Array.from({ length: daysPerWeek }, (_, i) => i + 1);

  // Group exercises by day
  const exercisesByDay = workout.exercises.reduce((acc, exercise) => {
    const day = exercise.assignedDay;
    if (!acc[day]) {
      acc[day] = [];
    }
    acc[day].push(exercise);
    return acc;
  }, {} as Record<number, ExerciseDto[]>);

  return (
    <Card className="md:col-span-2 lg:col-span-3 overflow-hidden">
      <CardHeader className="pb-4">
        <CardTitle className="flex items-center gap-2">
          <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7l5 5m0 0l-5 5m5-5H6" />
          </svg>
          Next Week Preview
          {isDeloadWeek && (
            <span className="ml-2 text-xs bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200 px-2 py-0.5 rounded">
              Deload Week
            </span>
          )}
        </CardTitle>
        <CardDescription>
          Week {nextWeek} - Block {nextBlock}
          {isDeloadWeek && " (Reduced volume for recovery)"}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {days.map((day) => (
            <div
              key={day}
              className="p-4 border rounded-lg border-border/50 bg-muted/10"
            >
              <h3 className="font-semibold text-foreground mb-3">{dayNames[day - 1] || `Day ${day}`}</h3>
              <div className="space-y-2">
                {(exercisesByDay[day] || [])
                  .sort((a, b) => a.orderInDay - b.orderInDay)
                  .map((exercise) => (
                    <div key={exercise.id} className="text-sm">
                      <div className="font-medium text-foreground/80">{exercise.name}</div>
                      <div className="text-xs text-muted-foreground">
                        {exercise.progression.type === 'Linear' && 'Linear Progression'}
                        {exercise.progression.type === 'RepsPerSet' && 'Reps Per Set'}
                        {exercise.progression.type === 'MinimalSets' && 'Minimal Sets'}
                      </div>
                    </div>
                  ))}
              </div>
            </div>
          ))}
        </div>
        {isDeloadWeek && (
          <div className="mt-4 p-3 bg-yellow-50 dark:bg-yellow-950 border border-yellow-200 dark:border-yellow-800 rounded-lg">
            <p className="text-sm text-yellow-800 dark:text-yellow-200">
              Week {nextWeek} is a deload week. Training volume will be reduced to allow for recovery and supercompensation.
            </p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
