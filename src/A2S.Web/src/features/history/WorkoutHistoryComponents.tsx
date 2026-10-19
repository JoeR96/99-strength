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
import { chartColors, chartTooltipContentStyle } from '@/lib/chartTheme';
import { getBlockColor } from '@/lib/blockColors';
import { unitLabel } from './historyStats';
import type { ExerciseHistoryDto } from './historyTypes';

export type {
  CompletedSetDto,
  WeeklyPerformanceDto,
  ExerciseHistoryDto,
  ExercisePerformanceHistoryDto,
  WorkoutActivityDto,
  WorkoutHistoryDto,
} from './historyTypes';

const PROGRESSION_LABELS: Record<string, string> = {
  Linear: 'Linear',
  RepsPerSet: 'Reps per set',
  MinimalSets: 'Minimal sets',
};
const progressionLabel = (type: string) => PROGRESSION_LABELS[type] ?? type;

/** "MainLift" -> "Main lift" */
const categoryLabel = (category: string) =>
  category.replace(/([a-z])([A-Z])/g, (_, a: string, b: string) => `${a} ${b.toLowerCase()}`);

/** A Linear lift has no fixed weight (it is a percentage of the training max), so show that instead. */
const loadLabel = (exercise: ExerciseHistoryDto) =>
  exercise.progressionType === 'Linear' && exercise.trainingMax
    ? `training max ${exercise.trainingMax} ${unitLabel(exercise.weightUnit)}`
    : `${exercise.currentWeight} ${unitLabel(exercise.weightUnit)}`;

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
      <div className="lg:col-span-1 rounded-lg border border-border bg-card p-4">
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
                    <div className={`text-xs ${selectedExercise?.exerciseId === exercise.exerciseId ? 'text-primary-foreground' : 'text-muted-foreground'}`}>
                      {progressionLabel(exercise.progressionType)} · {loadLabel(exercise)}
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
          <div className="rounded-lg border border-border bg-card p-8 text-center h-full flex items-center justify-center">
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
      <div className="rounded-lg border border-border bg-card p-6">
        <div className="flex items-start justify-between">
          <div>
            <h3 className="text-xl font-semibold text-foreground">{exercise.name}</h3>
            <p className="text-muted-foreground mt-1">Day {exercise.assignedDay} · {categoryLabel(exercise.category)} · {exercise.equipment}</p>
          </div>
          <span className={`px-3 py-1 rounded-full text-sm font-medium ${
            exercise.progressionType === 'Linear' ? 'bg-neon-blue/15 text-[color-mix(in_srgb,var(--color-neon-blue)_60%,var(--color-foreground))]'
            : exercise.progressionType === 'RepsPerSet' ? 'bg-neon-purple/15 text-[color-mix(in_srgb,var(--color-neon-purple)_60%,var(--color-foreground))]'
            : 'bg-warning/10 text-warning'
          }`}>{progressionLabel(exercise.progressionType)}</span>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mt-6">
          {exercise.currentWeight > 0 && (
            <div className="p-3 rounded-lg bg-muted/30">
              <p className="text-xs text-muted-foreground">Current Weight</p>
              <p className="text-lg font-semibold text-foreground">{exercise.currentWeight} {unitLabel(exercise.weightUnit)}</p>
            </div>
          )}
          {exercise.trainingMax && (
            <div className="p-3 rounded-lg bg-muted/30">
              <p className="text-xs text-muted-foreground">Training Max</p>
              <p className="text-lg font-semibold text-foreground">{exercise.trainingMax} {unitLabel(exercise.weightUnit)}</p>
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
          <div className="rounded-lg border border-border bg-card p-6">
            <h4 className="text-lg font-semibold text-foreground mb-4">
              {exercise.progressionType === 'Linear' ? 'Training Volume Over Time' : 'Set Volume Over Time'}
            </h4>
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={volumeChartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
                  <XAxis dataKey="week" stroke={chartColors.mutedForeground} tick={{ fill: chartColors.mutedForeground }} fontSize={12} />
                  <YAxis stroke={chartColors.mutedForeground} tick={{ fill: chartColors.mutedForeground }} fontSize={12} />
                  <Tooltip contentStyle={chartTooltipContentStyle}
                    formatter={(value) => typeof value === 'number' ? [`${value} kg·reps`, 'Volume'] : [value, 'Volume']} />
                  <Area type="monotone" dataKey="volume" stroke={chartColors.primary} fill={chartColors.primaryTranslucent} strokeWidth={2} />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="rounded-lg border border-border bg-card p-6">
            <h4 className="text-lg font-semibold text-foreground mb-4">Weight Progression</h4>
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={weightChartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
                  <XAxis dataKey="week" stroke={chartColors.mutedForeground} tick={{ fill: chartColors.mutedForeground }} fontSize={12} />
                  <YAxis stroke={chartColors.mutedForeground} tick={{ fill: chartColors.mutedForeground }} fontSize={12} domain={['auto', 'auto']} />
                  <Tooltip contentStyle={chartTooltipContentStyle}
                    formatter={(value) => [`${value} ${unitLabel(exercise.weightUnit)}`, 'Weight']} />
                  <Line type="monotone" dataKey="weight" stroke={chartColors.primary} strokeWidth={2} dot={{ fill: chartColors.primary, strokeWidth: 2, r: 4 }} activeDot={{ r: 6 }} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="rounded-lg border border-border bg-card p-6">
            <h4 className="text-lg font-semibold text-foreground mb-4">Week-by-Week History</h4>
            <div className="overflow-x-auto" tabIndex={0} role="region" aria-label={`${exercise.name} weekly history`}>
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
                        <span className="inline-flex w-6 h-6 items-center justify-center rounded-full text-xs font-medium text-background"
                          style={{ backgroundColor: getBlockColor(week.blockNumber) }}>{week.blockNumber}</span>
                      </td>
                      <td className="py-3 px-4 text-muted-foreground">{week.completedAt ? new Date(week.completedAt).toLocaleDateString() : '-'}</td>
                      <td className="py-3 px-4 text-right font-mono text-foreground">{week.setsCompleted}</td>
                      <td className="py-3 px-4 text-right font-mono text-foreground">{Math.round(week.averageWeight * 10) / 10} {unitLabel(exercise.weightUnit)}</td>
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

          <div className="rounded-lg border border-border bg-card p-6">
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
        <div className="rounded-lg border border-border bg-card p-8 text-center">
          <p className="text-muted-foreground">No history data available yet. Complete workouts to see progress tracking.</p>
          <p className="text-sm text-muted-foreground mt-2">Note: If you seeded data before the history tracking update, you may need to re-seed to see detailed history.</p>
        </div>
      )}
    </div>
  );
}
