/**
 * DashboardExerciseTracking — Per-exercise progression charts for the dashboard.
 * Linear exercises: TM progression over weeks.
 * RPS exercises: Volume progression over weeks.
 * Hover tooltips with session details.
 */

import { useMemo } from 'react';
import { useWorkoutHistory } from '@/hooks/useWorkouts';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import type {
  WorkoutDto,
  ExerciseHistoryDto,
} from '@/types/workout';

interface DashboardExerciseTrackingProps {
  workout: WorkoutDto;
}

interface ChartDataPoint {
  week: string;
  weekNumber: number;
  value: number;
  reps?: number;
  sets?: number;
  weight?: number;
  amrapReps?: number;
  isDeload: boolean;
}

function buildLinearChartData(history: ExerciseHistoryDto): ChartDataPoint[] {
  return history.weeklyHistory
    .filter((w) => w.completedAt)
    .map((w) => ({
      week: `W${w.weekNumber}`,
      weekNumber: w.weekNumber,
      value: w.trainingMaxAtWeek ?? w.averageWeight,
      reps: w.totalReps,
      sets: w.setsCompleted,
      weight: w.averageWeight,
      amrapReps: w.amrapReps,
      isDeload: w.isDeloadWeek,
    }));
}

function buildVolumeChartData(history: ExerciseHistoryDto): ChartDataPoint[] {
  return history.weeklyHistory
    .filter((w) => w.completedAt)
    .map((w) => ({
      week: `W${w.weekNumber}`,
      weekNumber: w.weekNumber,
      value: Math.round(w.totalVolume),
      reps: w.totalReps,
      sets: w.setsCompleted,
      weight: w.averageWeight,
      amrapReps: w.amrapReps,
      isDeload: w.isDeloadWeek,
    }));
}

function ExerciseProgressionChart({
  history,
  type,
}: {
  history: ExerciseHistoryDto;
  type: 'linear' | 'volume';
}) {
  const chartData = useMemo(
    () => (type === 'linear' ? buildLinearChartData(history) : buildVolumeChartData(history)),
    [history, type]
  );

  if (chartData.length < 2) {
    return (
      <div className="flex items-center justify-center h-32 text-sm text-muted-foreground">
        Not enough data yet (need 2+ sessions)
      </div>
    );
  }

  const lineColor = type === 'linear' ? 'hsl(var(--primary))' : 'hsl(210, 100%, 50%)';
  const label = type === 'linear' ? 'Training Max' : 'Volume';
  const unitLabel = type === 'linear' ? history.weightUnit === 'Kilograms' ? 'kg' : 'lbs' : '';

  return (
    <div className="h-40">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={chartData}>
          <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
          <XAxis
            dataKey="week"
            stroke="hsl(var(--muted-foreground))"
            fontSize={10}
            tick={{ fill: 'hsl(var(--muted-foreground))' }}
          />
          <YAxis
            stroke="hsl(var(--muted-foreground))"
            fontSize={10}
            tick={{ fill: 'hsl(var(--muted-foreground))' }}
            width={45}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: 'hsl(var(--card))',
              border: '1px solid hsl(var(--border))',
              borderRadius: '8px',
              color: 'hsl(var(--foreground))',
              fontSize: '12px',
            }}
            formatter={(_value, _name, props) => {
              const d = props.payload as unknown as ChartDataPoint;
              const lines: string[] = [];
              lines.push(`${label}: ${d.value}${unitLabel ? ` ${unitLabel}` : ''}`);
              if (d.sets !== undefined) lines.push(`Sets: ${d.sets}`);
              if (d.reps !== undefined) lines.push(`Reps: ${d.reps}`);
              if (d.weight !== undefined) lines.push(`Avg Weight: ${Math.round(d.weight)}${unitLabel ? ` ${unitLabel}` : ''}`);
              if (d.amrapReps !== undefined && d.amrapReps > 0) lines.push(`AMRAP: ${d.amrapReps} reps`);
              if (d.isDeload) lines.push('(Deload week)');
              return [<span style={{ whiteSpace: 'pre-line' }}>{lines.join('\n')}</span>, ''];
            }}
            labelFormatter={(l) => String(l)}
          />
          <Line
            type="monotone"
            dataKey="value"
            name={label}
            stroke={lineColor}
            strokeWidth={2}
            dot={{ fill: lineColor, r: 2.5 }}
            activeDot={{ r: 4 }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

export function DashboardExerciseTracking({ workout }: DashboardExerciseTrackingProps) {
  const { data: history, isLoading } = useWorkoutHistory(workout.id, true);

  const { linearExercises, volumeExercises } = useMemo(() => {
    if (!history?.exerciseHistories) {
      return { linearExercises: [], volumeExercises: [] };
    }

    const linear = history.exerciseHistories.filter(
      (e) => e.progressionType === 'Linear'
    );
    const volume = history.exerciseHistories.filter(
      (e) => e.progressionType === 'RepsPerSet' || e.progressionType === 'MinimalSets'
    );

    return { linearExercises: linear, volumeExercises: volume };
  }, [history]);

  if (isLoading) {
    return (
      <Card className="md:col-span-2 lg:col-span-3 overflow-hidden">
        <CardHeader className="pb-4">
          <CardTitle>Exercise Progression</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (linearExercises.length === 0 && volumeExercises.length === 0) {
    return null;
  }

  return (
    <Card className="md:col-span-2 lg:col-span-3 overflow-hidden">
      <CardHeader className="pb-4">
        <CardTitle className="flex items-center gap-2">
          <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
          </svg>
          Exercise Progression
        </CardTitle>
        <CardDescription>Training Max and volume trends from program start</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="space-y-6">
          {/* Linear exercises — TM progression */}
          {linearExercises.length > 0 && (
            <div>
              <h3 className="text-sm font-medium text-muted-foreground mb-3">
                Training Max Progression
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {linearExercises.map((exercise) => (
                  <div key={exercise.exerciseId} className="rounded-xl border border-border p-4">
                    <div className="flex items-center justify-between mb-2">
                      <h4 className="text-sm font-semibold text-foreground">{exercise.name}</h4>
                      {exercise.trainingMax && (
                        <span className="text-xs text-muted-foreground">
                          Current TM: {exercise.trainingMax} {exercise.weightUnit === 'Kilograms' ? 'kg' : 'lbs'}
                        </span>
                      )}
                    </div>
                    <ExerciseProgressionChart history={exercise} type="linear" />
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* RPS / MinimalSets exercises — Volume progression */}
          {volumeExercises.length > 0 && (
            <div>
              <h3 className="text-sm font-medium text-muted-foreground mb-3">
                Volume Progression
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {volumeExercises.map((exercise) => (
                  <div key={exercise.exerciseId} className="rounded-xl border border-border p-4">
                    <div className="flex items-center justify-between mb-2">
                      <h4 className="text-sm font-semibold text-foreground">{exercise.name}</h4>
                      <span className="text-xs text-muted-foreground">
                        {exercise.currentSets}/{exercise.targetSets} sets @ {exercise.currentWeight} {exercise.weightUnit === 'Kilograms' ? 'kg' : 'lbs'}
                      </span>
                    </div>
                    <ExerciseProgressionChart history={exercise} type="volume" />
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
