import { useMemo } from 'react';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  AreaChart,
  Area,
} from 'recharts';

export interface CompletedSetDto {
  setNumber: number;
  weight: number;
  weightUnit: string;
  actualReps: number;
  wasAmrap: boolean;
}

export interface WeeklyPerformanceDto {
  weekNumber: number;
  blockNumber: number;
  completedAt: string | null;
  isDeloadWeek: boolean;
  totalVolume: number;
  averageWeight: number;
  totalReps: number;
  setsCompleted: number;
  amrapReps: number | null;
  sets: CompletedSetDto[];
}

export interface ExerciseHistoryDto {
  exerciseId: string;
  name: string;
  progressionType: string;
  assignedDay: number;
  category: string;
  equipment: string;
  currentWeight: number;
  weightUnit: string;
  currentSets: number;
  targetSets: number;
  trainingMax: number | null;
  weeklyHistory: WeeklyPerformanceDto[];
}

export interface ExercisePerformanceHistoryDto {
  exerciseId: string;
  completedAt: string;
  completedSets: CompletedSetDto[];
}

export interface WorkoutActivityDto {
  day: string;
  dayNumber: number;
  weekNumber: number;
  blockNumber: number;
  completedAt: string;
  isDeloadWeek: boolean;
  performances: ExercisePerformanceHistoryDto[];
}

export interface WorkoutHistoryDto {
  workoutId: string;
  workoutName: string;
  variant: string;
  totalWeeks: number;
  currentWeek: number;
  currentBlock: number;
  daysPerWeek: number;
  startedAt: string | null;
  totalWorkoutsCompleted: number;
  completedActivities: WorkoutActivityDto[];
  exerciseHistories: ExerciseHistoryDto[];
}

export const BLOCK_COLORS = {
  1: '#3b82f6',
  2: '#8b5cf6',
  3: '#ec4899',
};

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

export function GitHubStyleCalendar({
  months,
  onActivityClick,
  selectedDate,
}: {
  months: { month: number; year: number; days: { date: Date; activity: WorkoutActivityDto | null }[] }[];
  daysPerWeek: number;
  onActivityClick?: (activity: WorkoutActivityDto, date: Date) => void;
  selectedDate?: Date;
}) {
  return (
    <div className="space-y-8">
      {months.map((monthData, idx) => (
        <div key={idx} className="rounded-xl border border-border bg-card p-6">
          <h3 className="text-lg font-semibold text-foreground mb-4">
            {MONTHS[monthData.month]} {monthData.year}
          </h3>
          <div className="grid grid-cols-7 gap-1 mb-2">
            {DAYS.map(day => (
              <div key={day} className="text-center text-xs text-muted-foreground font-medium py-1">{day}</div>
            ))}
          </div>
          <div className="grid grid-cols-7 gap-1">
            {monthData.days.map((day, dayIdx) => {
              if (day.date.getTime() === 0) {
                return <div key={dayIdx} className="aspect-square" />;
              }
              const activity = day.activity;
              const isToday = day.date.toDateString() === new Date().toDateString();
              const isSelected = selectedDate && day.date.toDateString() === selectedDate.toDateString();
              const blockColor = activity ? BLOCK_COLORS[activity.blockNumber as keyof typeof BLOCK_COLORS] : undefined;

              return (
                <div
                  key={dayIdx}
                  onClick={() => activity && onActivityClick?.(activity, day.date)}
                  onKeyDown={(e) => {
                    if (activity && (e.key === 'Enter' || e.key === ' ')) {
                      e.preventDefault();
                      onActivityClick?.(activity, day.date);
                    }
                  }}
                  role="gridcell"
                  tabIndex={activity ? 0 : -1}
                  aria-label={activity
                    ? `Week ${activity.weekNumber}, Day ${activity.dayNumber}${activity.isDeloadWeek ? ' (Deload)' : ''}`
                    : day.date.toLocaleDateString()
                  }
                  className={`aspect-square rounded-md flex items-center justify-center text-xs font-medium transition-all ${
                    activity
                      ? 'text-white cursor-pointer hover:ring-2 hover:ring-white/50'
                      : isToday
                      ? 'bg-primary/20 text-primary ring-2 ring-primary cursor-default'
                      : 'bg-muted/30 text-muted-foreground cursor-default'
                  } ${isSelected ? 'ring-2 ring-white ring-offset-2 ring-offset-background' : ''}`}
                  style={activity ? { backgroundColor: blockColor } : undefined}
                  title={activity
                    ? `Week ${activity.weekNumber}, Day ${activity.dayNumber}${activity.isDeloadWeek ? ' (Deload)' : ''} - Click for details`
                    : day.date.toLocaleDateString()
                  }
                >
                  {day.date.getDate()}
                </div>
              );
            })}
          </div>
          <div className="flex gap-4 mt-4 text-xs text-muted-foreground">
            {[1, 2, 3].map(b => (
              <div key={b} className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: BLOCK_COLORS[b as keyof typeof BLOCK_COLORS] }} />
                <span>Block {b}</span>
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

export function WorkoutActivityDetail({
  activity,
  date,
  exerciseHistories,
  onClose,
}: {
  activity?: WorkoutActivityDto;
  date?: Date;
  exerciseHistories: ExerciseHistoryDto[];
  onClose: () => void;
}) {
  if (!activity || !date) {
    return (
      <div className="rounded-xl border border-border bg-card p-6 h-full flex items-center justify-center">
        <div className="text-center">
          <svg className="w-12 h-12 text-muted-foreground/30 mx-auto mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
          </svg>
          <p className="text-muted-foreground text-sm">Click on a workout day to see details</p>
        </div>
      </div>
    );
  }

  const getExerciseName = (exerciseId: string) => {
    const exercise = exerciseHistories.find(e => e.exerciseId === exerciseId);
    return exercise?.name ?? 'Unknown Exercise';
  };

  const totalVolume = activity.performances.reduce((sum, perf) => {
    return sum + perf.completedSets.reduce((setSum, set) => setSum + (set.weight * set.actualReps), 0);
  }, 0);

  const totalSets = activity.performances.reduce((sum, perf) => sum + perf.completedSets.length, 0);

  return (
    <div className="rounded-xl border border-border bg-card p-6 space-y-4">
      <div className="flex items-start justify-between">
        <div>
          <h3 className="text-lg font-semibold text-foreground">
            {date.toLocaleDateString('en-US', { weekday: 'long', month: 'short', day: 'numeric' })}
          </h3>
          <p className="text-sm text-muted-foreground mt-0.5">
            Week {activity.weekNumber}, Day {activity.dayNumber}
            {activity.isDeloadWeek && <span className="ml-2 text-amber-500">(Deload)</span>}
          </p>
        </div>
        <button onClick={onClose} className="p-1 rounded-md hover:bg-muted transition-colors" title="Close">
          <svg className="w-5 h-5 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
      <div className="grid grid-cols-3 gap-3">
        <div className="p-3 rounded-lg bg-muted/30 text-center">
          <p className="text-xs text-muted-foreground">Exercises</p>
          <p className="text-lg font-semibold text-foreground">{activity.performances.length}</p>
        </div>
        <div className="p-3 rounded-lg bg-muted/30 text-center">
          <p className="text-xs text-muted-foreground">Sets</p>
          <p className="text-lg font-semibold text-foreground">{totalSets}</p>
        </div>
        <div className="p-3 rounded-lg bg-muted/30 text-center">
          <p className="text-xs text-muted-foreground">Volume</p>
          <p className="text-lg font-semibold text-foreground">{Math.round(totalVolume).toLocaleString()}</p>
        </div>
      </div>
      <div className="space-y-3 max-h-[400px] overflow-y-auto">
        {activity.performances.map((perf) => (
          <div key={perf.exerciseId} className="border-b border-border/50 pb-3 last:border-0 last:pb-0">
            <h4 className="font-medium text-foreground text-sm mb-2">{getExerciseName(perf.exerciseId)}</h4>
            <div className="flex flex-wrap gap-1.5">
              {perf.completedSets.map((set) => (
                <div key={set.setNumber} className={`px-2 py-1 rounded text-xs ${set.wasAmrap ? 'bg-primary/10 text-primary border border-primary/30' : 'bg-muted/50 text-foreground'}`}>
                  <span className="font-mono">{set.weight}{set.weightUnit === 'Kilograms' ? 'kg' : 'lbs'} × {set.actualReps}</span>
                  {set.wasAmrap && <span className="ml-1 opacity-70">(AMRAP)</span>}
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

export function ExerciseProgressView({
  exercises,
  selectedExercise,
  onSelectExercise,
}: {
  exercises: ExerciseHistoryDto[];
  selectedExercise: ExerciseHistoryDto | null;
  onSelectExercise: (exercise: ExerciseHistoryDto | null) => void;
}) {
  const exercisesByDay = useMemo(() => {
    const grouped: Record<number, ExerciseHistoryDto[]> = {};
    exercises.forEach(ex => {
      if (!grouped[ex.assignedDay]) grouped[ex.assignedDay] = [];
      grouped[ex.assignedDay].push(ex);
    });
    return grouped;
  }, [exercises]);

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <div className="lg:col-span-1 rounded-xl border border-border bg-card p-4">
        <h3 className="text-lg font-semibold text-foreground mb-4">Exercises</h3>
        <div className="space-y-4">
          {Object.entries(exercisesByDay).map(([day, dayExercises]) => (
            <div key={day}>
              <h4 className="text-sm font-medium text-muted-foreground mb-2">Day {day}</h4>
              <div className="space-y-1">
                {dayExercises.map(exercise => (
                  <button
                    key={exercise.exerciseId}
                    onClick={() => onSelectExercise(exercise)}
                    className={`w-full text-left px-3 py-2 rounded-lg transition-colors ${
                      selectedExercise?.exerciseId === exercise.exerciseId
                        ? 'bg-primary text-primary-foreground'
                        : 'hover:bg-muted'
                    }`}
                  >
                    <div className="font-medium text-sm">{exercise.name}</div>
                    <div className={`text-xs ${selectedExercise?.exerciseId === exercise.exerciseId ? 'text-primary-foreground/70' : 'text-muted-foreground'}`}>
                      {exercise.progressionType} · {exercise.currentWeight} {exercise.weightUnit.toLowerCase()}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
      <div className="lg:col-span-2">
        {selectedExercise ? (
          <ExerciseDetailView exercise={selectedExercise} />
        ) : (
          <div className="rounded-xl border border-border bg-card p-8 text-center h-full flex items-center justify-center">
            <div>
              <svg className="w-16 h-16 text-muted-foreground/30 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
              <p className="text-muted-foreground">Select an exercise to view detailed progress</p>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export function ExerciseDetailView({ exercise }: { exercise: ExerciseHistoryDto }) {
  const hasHistory = exercise.weeklyHistory.length > 0;

  const volumeChartData = useMemo(() => {
    return exercise.weeklyHistory.map(week => ({
      week: `W${week.weekNumber}`,
      volume: Math.round(week.totalVolume),
      avgWeight: Math.round(week.averageWeight * 10) / 10,
      totalReps: week.totalReps,
      isDeload: week.isDeloadWeek,
    }));
  }, [exercise.weeklyHistory]);

  const weightChartData = useMemo(() => {
    return exercise.weeklyHistory.map(week => ({
      week: `W${week.weekNumber}`,
      weight: Math.round(week.averageWeight * 10) / 10,
      isDeload: week.isDeloadWeek,
    }));
  }, [exercise.weeklyHistory]);

  return (
    <div className="space-y-6">
      <div className="rounded-xl border border-border bg-card p-6">
        <div className="flex items-start justify-between">
          <div>
            <h3 className="text-xl font-semibold text-foreground">{exercise.name}</h3>
            <p className="text-muted-foreground mt-1">Day {exercise.assignedDay} · {exercise.category} · {exercise.equipment}</p>
          </div>
          <span className={`px-3 py-1 rounded-full text-sm font-medium ${
            exercise.progressionType === 'Linear' ? 'bg-blue-500/10 text-blue-500'
            : exercise.progressionType === 'RepsPerSet' ? 'bg-purple-500/10 text-purple-500'
            : 'bg-amber-500/10 text-amber-500'
          }`}>{exercise.progressionType}</span>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mt-6">
          <div className="p-3 rounded-lg bg-muted/30">
            <p className="text-xs text-muted-foreground">Current Weight</p>
            <p className="text-lg font-semibold text-foreground">{exercise.currentWeight} {exercise.weightUnit.toLowerCase()}</p>
          </div>
          {exercise.trainingMax && (
            <div className="p-3 rounded-lg bg-muted/30">
              <p className="text-xs text-muted-foreground">Training Max</p>
              <p className="text-lg font-semibold text-foreground">{exercise.trainingMax} {exercise.weightUnit.toLowerCase()}</p>
            </div>
          )}
          <div className="p-3 rounded-lg bg-muted/30">
            <p className="text-xs text-muted-foreground">Current Sets</p>
            <p className="text-lg font-semibold text-foreground">{exercise.currentSets}{exercise.progressionType === 'RepsPerSet' && ` / ${exercise.targetSets}`}</p>
          </div>
          <div className="p-3 rounded-lg bg-muted/30">
            <p className="text-xs text-muted-foreground">Weeks Tracked</p>
            <p className="text-lg font-semibold text-foreground">{exercise.weeklyHistory.length}</p>
          </div>
        </div>
      </div>

      {hasHistory ? (
        <>
          <div className="rounded-xl border border-border bg-card p-6">
            <h4 className="text-lg font-semibold text-foreground mb-4">
              {exercise.progressionType === 'Linear' ? 'Training Volume Over Time' : 'Set Volume Over Time'}
            </h4>
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={volumeChartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                  <XAxis dataKey="week" stroke="hsl(var(--muted-foreground))" fontSize={12} />
                  <YAxis stroke="hsl(var(--muted-foreground))" fontSize={12} />
                  <Tooltip contentStyle={{ backgroundColor: 'hsl(var(--card))', border: '1px solid hsl(var(--border))', borderRadius: '8px' }}
                    formatter={(value) => typeof value === 'number' ? [`${value} kg·reps`, 'Volume'] : [value, 'Volume']} />
                  <Area type="monotone" dataKey="volume" stroke="hsl(var(--primary))" fill="hsl(var(--primary) / 0.2)" strokeWidth={2} />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="rounded-xl border border-border bg-card p-6">
            <h4 className="text-lg font-semibold text-foreground mb-4">Weight Progression</h4>
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={weightChartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                  <XAxis dataKey="week" stroke="hsl(var(--muted-foreground))" fontSize={12} />
                  <YAxis stroke="hsl(var(--muted-foreground))" fontSize={12} domain={['auto', 'auto']} />
                  <Tooltip contentStyle={{ backgroundColor: 'hsl(var(--card))', border: '1px solid hsl(var(--border))', borderRadius: '8px' }}
                    formatter={(value) => [`${value} ${exercise.weightUnit.toLowerCase()}`, 'Weight']} />
                  <Line type="monotone" dataKey="weight" stroke="hsl(var(--primary))" strokeWidth={2} dot={{ fill: 'hsl(var(--primary))', strokeWidth: 2, r: 4 }} activeDot={{ r: 6 }} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="rounded-xl border border-border bg-card p-6">
            <h4 className="text-lg font-semibold text-foreground mb-4">Week-by-Week History</h4>
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-border">
                    <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Week</th>
                    <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Block</th>
                    <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Date</th>
                    <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Sets</th>
                    <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Avg Weight</th>
                    <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Total Reps</th>
                    <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Volume</th>
                    {exercise.progressionType === 'Linear' && (
                      <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">AMRAP</th>
                    )}
                  </tr>
                </thead>
                <tbody>
                  {exercise.weeklyHistory.map(week => (
                    <tr key={week.weekNumber} className={`border-b border-border/50 ${week.isDeloadWeek ? 'bg-muted/20' : ''}`}>
                      <td className="py-3 px-4">
                        <span className="font-medium text-foreground">Week {week.weekNumber}</span>
                        {week.isDeloadWeek && <span className="ml-2 text-xs px-2 py-0.5 rounded-full bg-muted text-muted-foreground">Deload</span>}
                      </td>
                      <td className="py-3 px-4">
                        <span className="inline-flex w-6 h-6 items-center justify-center rounded-full text-xs font-medium text-white"
                          style={{ backgroundColor: BLOCK_COLORS[week.blockNumber as keyof typeof BLOCK_COLORS] }}>{week.blockNumber}</span>
                      </td>
                      <td className="py-3 px-4 text-muted-foreground">{week.completedAt ? new Date(week.completedAt).toLocaleDateString() : '-'}</td>
                      <td className="py-3 px-4 text-right font-mono text-foreground">{week.setsCompleted}</td>
                      <td className="py-3 px-4 text-right font-mono text-foreground">{Math.round(week.averageWeight * 10) / 10} {exercise.weightUnit.toLowerCase()}</td>
                      <td className="py-3 px-4 text-right font-mono text-foreground">{week.totalReps}</td>
                      <td className="py-3 px-4 text-right font-mono text-foreground">{Math.round(week.totalVolume)}</td>
                      {exercise.progressionType === 'Linear' && (
                        <td className="py-3 px-4 text-right">
                          {week.amrapReps !== null ? <span className="font-mono text-foreground">{week.amrapReps}</span> : <span className="text-muted-foreground">-</span>}
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div className="rounded-xl border border-border bg-card p-6">
            <h4 className="text-lg font-semibold text-foreground mb-4">Set Details</h4>
            <div className="space-y-4">
              {exercise.weeklyHistory.map(week => (
                <div key={week.weekNumber} className="border-b border-border/50 pb-4 last:border-0 last:pb-0">
                  <div className="flex items-center gap-2 mb-2">
                    <span className="font-medium text-foreground">Week {week.weekNumber}</span>
                    {week.isDeloadWeek && <span className="text-xs px-2 py-0.5 rounded-full bg-muted text-muted-foreground">Deload</span>}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {week.sets.map(set => (
                      <div key={set.setNumber} className={`px-3 py-2 rounded-lg text-sm ${set.wasAmrap ? 'bg-primary/10 text-primary border border-primary/30' : 'bg-muted/50 text-foreground'}`}>
                        <span className="font-mono">{set.weight}{exercise.weightUnit === 'Kilograms' ? 'kg' : 'lbs'} × {set.actualReps}</span>
                        {set.wasAmrap && <span className="ml-1 text-xs">(AMRAP)</span>}
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </>
      ) : (
        <div className="rounded-xl border border-border bg-card p-8 text-center">
          <p className="text-muted-foreground">No history data available yet. Complete workouts to see progress tracking.</p>
          <p className="text-sm text-muted-foreground mt-2">Note: If you seeded data before the history tracking update, you may need to re-seed to see detailed history.</p>
        </div>
      )}
    </div>
  );
}
