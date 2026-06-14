/**
 * Workout Simulation Page
 * Simulate workout progression over time using real progression logic
 */

import { useState, useMemo, useEffect, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Navbar } from '@/components/layout/Navbar';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { apiClient, getAuthToken } from '@/api/apiClient';
import { useAllWorkouts } from '@/hooks/useWorkouts';
import { readNdjsonStream } from '@/lib/ndjson';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from 'recharts';
import { chartColors, chartSeriesPalette, chartTooltipContentStyle } from '@/lib/chartTheme';

interface SimulationDataPoint {
  session: number;
  week: number;
  block: number;
  trainingMax: number | null;
  trainingMaxUnit: string | null;
  currentWeight: number | null;
  currentWeightUnit: string | null;
  summary: {
    type: string;
    details: Record<string, string>;
  };
}

interface ExerciseSimulationSeries {
  exerciseId: string;
  exerciseName: string;
  progressionType: string;
  dataPoints: SimulationDataPoint[];
}

interface SimulationResult {
  workoutName: string;
  variant: string;
  startWeek: number;
  endWeek: number;
  totalWeeks: number;
  exerciseTimeSeries: ExerciseSimulationSeries[];
}

function useSimulation(workoutId: string | null, sessions: number, enabled: boolean) {
  return useQuery({
    queryKey: ['simulate-workout', workoutId, sessions],
    queryFn: async () => {
      const response = await apiClient.get<SimulationResult>(
        `/workouts/${workoutId}/simulate?sessions=${sessions}`
      );
      return response.data;
    },
    enabled: enabled && !!workoutId,
    staleTime: 1000 * 60 * 5,
  });
}

// Multi-series simulation charts need distinct colours per exercise; use the shared
// theme-token palette so every series stays on-theme. See lib/chartTheme.ts.
const CHART_COLORS = chartSeriesPalette;

interface StreamDayEvent {
  type: 'day';
  outcome: string;
  day: number;
  weekNumber: number;
  blockNumber: number;
  exercisesCompleted: number;
  newCurrentWeek: number;
  newCurrentDay: number;
  weekProgressed: boolean;
  programComplete: boolean;
  isDeloadWeek: boolean;
  progressionChanges: Array<{ exerciseId: string; exerciseName: string; change: string }>;
}

type StreamEvent =
  | { type: 'started'; totalDays: number }
  | StreamDayEvent
  | { type: 'completed' }
  | { type: 'error'; message: string };

export function SimulationPage() {
  const { data: workouts, isLoading: loadingWorkouts } = useAllWorkouts();
  const [selectedWorkoutId, setSelectedWorkoutId] = useState<string | null>(null);
  const [sessionCount, setSessionCount] = useState(30);
  const [runSimulation, setRunSimulation] = useState(false);
  const [selectedExercise, setSelectedExercise] = useState<string | null>(null);

  // Persistent streaming run state
  const [runDays, setRunDays] = useState(84);
  const [runSuccessRate, setRunSuccessRate] = useState(0.65);
  const [runMaintainRate, setRunMaintainRate] = useState(0.25);
  const [streaming, setStreaming] = useState(false);
  const [streamEvents, setStreamEvents] = useState<StreamDayEvent[]>([]);
  const [streamError, setStreamError] = useState<string | null>(null);
  const [streamDone, setStreamDone] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  const handleRunStream = async () => {
    if (!selectedWorkoutId) return;
    setStreaming(true);
    setStreamEvents([]);
    setStreamError(null);
    setStreamDone(false);

    const controller = new AbortController();
    abortRef.current = controller;

    try {
      const token = await getAuthToken();
      const baseUrl = apiClient.defaults.baseURL ?? '';
      const url = `${baseUrl}/workouts/${selectedWorkoutId}/simulate/stream` +
        `?days=${runDays}&successRate=${runSuccessRate}&maintainRate=${runMaintainRate}`;
      const response = await fetch(url, {
        signal: controller.signal,
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (!response.ok) {
        setStreamError(`HTTP ${response.status}`);
        setStreaming(false);
        return;
      }
      for await (const evt of readNdjsonStream<StreamEvent>(response)) {
        if (evt.type === 'day') {
          setStreamEvents((prev) => [...prev, evt]);
        } else if (evt.type === 'error') {
          setStreamError(evt.message);
          break;
        } else if (evt.type === 'completed') {
          setStreamDone(true);
          break;
        }
      }
    } catch (err) {
      if ((err as Error).name !== 'AbortError') {
        setStreamError((err as Error).message);
      }
    } finally {
      setStreaming(false);
      abortRef.current = null;
    }
  };

  const handleCancelStream = () => {
    abortRef.current?.abort();
  };

  const { data: simulation, isLoading: loadingSimulation, error } = useSimulation(
    selectedWorkoutId,
    sessionCount,
    runSimulation
  );

  const activeWorkout = workouts?.find((w) => w.status === 'Active');

  // Auto-select active workout
  useEffect(() => {
    if (activeWorkout && !selectedWorkoutId) {
      setSelectedWorkoutId(activeWorkout.id);
    }
  }, [activeWorkout, selectedWorkoutId]);

  const handleRun = () => {
    if (!selectedWorkoutId && activeWorkout) {
      setSelectedWorkoutId(activeWorkout.id);
    }
    setRunSimulation(true);
  };

  const linearExercises = useMemo(() => {
    return simulation?.exerciseTimeSeries.filter((s) => s.progressionType === 'Linear') ?? [];
  }, [simulation]);

  const rpsExercises = useMemo(() => {
    return simulation?.exerciseTimeSeries.filter((s) => s.progressionType === 'RepsPerSet') ?? [];
  }, [simulation]);

  const minimalSetsExercises = useMemo(() => {
    return simulation?.exerciseTimeSeries.filter((s) => s.progressionType === 'MinimalSets') ?? [];
  }, [simulation]);

  const selectedSeries = simulation?.exerciseTimeSeries.find(
    (s) => s.exerciseId === selectedExercise
  );

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="container mx-auto px-4 py-6 max-w-5xl">
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Workout Simulator</h1>
          <p className="text-muted-foreground">
            Project your progression using real training algorithms with simulated AMRAP results
          </p>
        </div>

        {/* Controls */}
        <Card className="mb-6">
          <CardContent className="pt-6">
            <div className="flex flex-wrap items-end gap-4">
              <div className="flex-1 min-w-[200px]">
                <label className="block text-sm font-medium text-foreground mb-1.5">
                  Workout
                </label>
                <select
                  className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm"
                  value={selectedWorkoutId ?? ''}
                  onChange={(e) => {
                    setSelectedWorkoutId(e.target.value || null);
                    setRunSimulation(false);
                  }}
                >
                  <option value="">Select a workout...</option>
                  {loadingWorkouts ? (
                    <option disabled>Loading...</option>
                  ) : (
                    workouts?.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.name} {w.status === 'Active' ? '(Active)' : `(${w.status})`}
                      </option>
                    ))
                  )}
                </select>
              </div>

              <div className="w-32">
                <label className="block text-sm font-medium text-foreground mb-1.5">
                  Sessions
                </label>
                <input
                  type="number"
                  min={1}
                  max={500}
                  value={sessionCount}
                  onChange={(e) => {
                    setSessionCount(Math.max(1, Math.min(500, parseInt(e.target.value) || 1)));
                    setRunSimulation(false);
                  }}
                  className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm"
                />
              </div>

              <Button
                onClick={handleRun}
                disabled={!selectedWorkoutId || loadingSimulation}
              >
                {loadingSimulation ? (
                  <>
                    <div className="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent mr-2" />
                    Simulating...
                  </>
                ) : (
                  'Run Simulation'
                )}
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Dev-only: persistent day-by-day simulation (streams via NDJSON) */}
        <Card className="mb-6 border-dashed">
          <CardHeader>
            <CardTitle className="text-base">Persistent Run (dev)</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex flex-wrap items-end gap-4 mb-4">
              <div className="w-24">
                <label className="block text-xs text-muted-foreground mb-1">Days</label>
                <input
                  type="number"
                  min={1}
                  max={200}
                  value={runDays}
                  onChange={(e) => setRunDays(Math.max(1, Math.min(200, parseInt(e.target.value) || 1)))}
                  className="w-full rounded border border-border bg-background px-2 py-1 text-sm"
                />
              </div>
              <div className="w-28">
                <label className="block text-xs text-muted-foreground mb-1">Success %</label>
                <input
                  type="number"
                  min={0}
                  max={100}
                  value={Math.round(runSuccessRate * 100)}
                  onChange={(e) => setRunSuccessRate(Math.max(0, Math.min(100, parseInt(e.target.value) || 0)) / 100)}
                  className="w-full rounded border border-border bg-background px-2 py-1 text-sm"
                />
              </div>
              <div className="w-28">
                <label className="block text-xs text-muted-foreground mb-1">Maintain %</label>
                <input
                  type="number"
                  min={0}
                  max={100}
                  value={Math.round(runMaintainRate * 100)}
                  onChange={(e) => setRunMaintainRate(Math.max(0, Math.min(100, parseInt(e.target.value) || 0)) / 100)}
                  className="w-full rounded border border-border bg-background px-2 py-1 text-sm"
                />
              </div>
              {streaming ? (
                <Button variant="destructive" onClick={handleCancelStream}>Cancel</Button>
              ) : (
                <Button onClick={handleRunStream} disabled={!selectedWorkoutId}>
                  Run Persistent
                </Button>
              )}
              <div className="text-xs text-muted-foreground ml-auto">
                {streamEvents.length} day(s) · {streamDone ? 'done' : streaming ? 'streaming…' : 'idle'}
              </div>
            </div>

            {streamError && (
              <div className="mb-3 rounded border border-destructive/50 bg-destructive/10 p-2 text-sm text-destructive">
                {streamError}
              </div>
            )}

            {streamEvents.length > 0 && (
              <div className="max-h-80 overflow-y-auto space-y-1 rounded border border-border bg-muted/20 p-2 text-xs font-mono">
                {streamEvents.map((e, idx) => (
                  <div key={idx} className="flex gap-3">
                    <span className="text-muted-foreground">#{idx + 1}</span>
                    <span>W{e.weekNumber} D{e.day} B{e.blockNumber}</span>
                    <span
                      className={
                        e.outcome === 'Success'
                          ? 'text-green-600'
                          : e.outcome === 'Fail'
                            ? 'text-destructive'
                            : 'text-yellow-600'
                      }
                    >
                      {e.outcome}
                    </span>
                    {e.isDeloadWeek && <span className="text-blue-500">deload</span>}
                    {e.programComplete && <span className="font-semibold">PROGRAM COMPLETE</span>}
                    <span className="text-muted-foreground">
                      → W{e.newCurrentWeek} D{e.newCurrentDay}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        {error && (
          <Card className="mb-6">
            <CardContent className="pt-6">
              <p className="text-destructive">
                Simulation failed: {error instanceof Error ? error.message : 'Unknown error'}
              </p>
            </CardContent>
          </Card>
        )}

        {/* Results */}
        {simulation && (
          <div className="space-y-6">
            {/* Overview */}
            <Card>
              <CardHeader>
                <CardTitle>{simulation.workoutName} — Projection</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  <div className="rounded-lg bg-muted/30 p-3">
                    <p className="text-xs text-muted-foreground">Start Week</p>
                    <p className="text-xl font-semibold">{simulation.startWeek}</p>
                  </div>
                  <div className="rounded-lg bg-muted/30 p-3">
                    <p className="text-xs text-muted-foreground">End Week</p>
                    <p className="text-xl font-semibold">{simulation.endWeek}</p>
                  </div>
                  <div className="rounded-lg bg-muted/30 p-3">
                    <p className="text-xs text-muted-foreground">Total Weeks</p>
                    <p className="text-xl font-semibold">{simulation.totalWeeks}</p>
                  </div>
                  <div className="rounded-lg bg-muted/30 p-3">
                    <p className="text-xs text-muted-foreground">Exercises</p>
                    <p className="text-xl font-semibold">{simulation.exerciseTimeSeries.length}</p>
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Training Max Chart (Linear exercises) */}
            {linearExercises.length > 0 && (
              <Card>
                <CardHeader>
                  <CardTitle className="text-lg">Training Max Progression</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="h-72">
                    <ResponsiveContainer width="100%" height="100%">
                      <LineChart data={linearExercises[0]?.dataPoints ?? []}>
                        <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
                        <XAxis
                          dataKey="session"
                          stroke={chartColors.mutedForeground}
                          tick={{ fill: chartColors.mutedForeground }}
                          fontSize={11}
                          label={{ value: 'Session', position: 'insideBottom', offset: -5 }}
                        />
                        <YAxis
                          stroke={chartColors.mutedForeground}
                          tick={{ fill: chartColors.mutedForeground }}
                          fontSize={11}
                          label={{
                            value: 'TM (kg)',
                            angle: -90,
                            position: 'insideLeft',
                            offset: 10,
                          }}
                        />
                        <Tooltip
                          contentStyle={chartTooltipContentStyle}
                          formatter={(value, name) => [
                            `${Math.round(Number(value) * 100) / 100}kg`,
                            name,
                          ]}
                          labelFormatter={(label) => `Session ${label}`}
                        />
                        <Legend />
                        {linearExercises.map((series, idx) => (
                          <Line
                            key={series.exerciseId}
                            data={series.dataPoints}
                            type="monotone"
                            dataKey="trainingMax"
                            name={series.exerciseName}
                            stroke={CHART_COLORS[idx % CHART_COLORS.length]}
                            strokeWidth={2}
                            dot={false}
                            activeDot={{ r: 4 }}
                          />
                        ))}
                      </LineChart>
                    </ResponsiveContainer>
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Weight progression for RPS exercises */}
            {rpsExercises.length > 0 && (
              <Card>
                <CardHeader>
                  <CardTitle className="text-lg">Accessory Weight Progression</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="h-72">
                    <ResponsiveContainer width="100%" height="100%">
                      <LineChart data={rpsExercises[0]?.dataPoints ?? []}>
                        <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
                        <XAxis
                          dataKey="session"
                          stroke={chartColors.mutedForeground}
                          tick={{ fill: chartColors.mutedForeground }}
                          fontSize={11}
                          label={{ value: 'Session', position: 'insideBottom', offset: -5 }}
                        />
                        <YAxis
                          stroke={chartColors.mutedForeground}
                          tick={{ fill: chartColors.mutedForeground }}
                          fontSize={11}
                          label={{
                            value: 'Weight (kg)',
                            angle: -90,
                            position: 'insideLeft',
                            offset: 10,
                          }}
                        />
                        <Tooltip
                          contentStyle={chartTooltipContentStyle}
                          formatter={(value, name) => [
                            value != null ? `${value}kg` : 'Pending',
                            name,
                          ]}
                          labelFormatter={(label) => `Session ${label}`}
                        />
                        <Legend />
                        {rpsExercises.map((series, idx) => (
                          <Line
                            key={series.exerciseId}
                            data={series.dataPoints}
                            type="monotone"
                            dataKey="currentWeight"
                            name={series.exerciseName}
                            stroke={CHART_COLORS[(idx + 3) % CHART_COLORS.length]}
                            strokeWidth={2}
                            dot={false}
                            activeDot={{ r: 4 }}
                            connectNulls
                          />
                        ))}
                      </LineChart>
                    </ResponsiveContainer>
                  </div>
                </CardContent>
              </Card>
            )}

            {/* MinimalSets exercises info */}
            {minimalSetsExercises.length > 0 && (
              <Card>
                <CardHeader>
                  <CardTitle className="text-lg">Bodyweight Exercise Projection</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="grid gap-3 sm:grid-cols-2">
                    {minimalSetsExercises.map((series) => {
                      const firstPoint = series.dataPoints[0];
                      const lastPoint = series.dataPoints[series.dataPoints.length - 1];
                      return (
                        <div
                          key={series.exerciseId}
                          className="rounded-lg border border-border p-4"
                        >
                          <p className="font-medium text-foreground">{series.exerciseName}</p>
                          <div className="mt-2 flex gap-4 text-sm text-muted-foreground">
                            <span>
                              Start: {firstPoint?.currentWeight ?? '—'}kg
                            </span>
                            <span>→</span>
                            <span>
                              End: {lastPoint?.currentWeight ?? '—'}kg
                            </span>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Per-exercise detail */}
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Exercise Detail</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex flex-wrap gap-2 mb-4">
                  {simulation.exerciseTimeSeries.map((series) => (
                    <button
                      key={series.exerciseId}
                      onClick={() =>
                        setSelectedExercise(
                          selectedExercise === series.exerciseId ? null : series.exerciseId
                        )
                      }
                      className={`rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors ${
                        selectedExercise === series.exerciseId
                          ? 'border-primary bg-primary text-primary-foreground'
                          : 'border-border text-muted-foreground hover:text-foreground hover:bg-muted/50'
                      }`}
                    >
                      {series.exerciseName}
                    </button>
                  ))}
                </div>

                {selectedSeries && (
                  <div className="space-y-4">
                    <div className="h-56">
                      <ResponsiveContainer width="100%" height="100%">
                        <LineChart data={selectedSeries.dataPoints}>
                          <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} />
                          <XAxis
                            dataKey="session"
                            stroke={chartColors.mutedForeground}
                            tick={{ fill: chartColors.mutedForeground }}
                            fontSize={11}
                          />
                          <YAxis
                            stroke={chartColors.mutedForeground}
                            tick={{ fill: chartColors.mutedForeground }}
                            fontSize={11}
                          />
                          <Tooltip
                            contentStyle={chartTooltipContentStyle}
                            formatter={(value, key) => {
                              if (key === 'trainingMax') return [`${Math.round(Number(value) * 100) / 100}kg`, 'Training Max'];
                              if (key === 'currentWeight') return [value != null ? `${value}kg` : 'Pending', 'Weight'];
                              return [value, key];
                            }}
                            labelFormatter={(label) => `Session ${label} · Week ${selectedSeries.dataPoints[label as number]?.week ?? '?'}`}
                          />
                          {selectedSeries.progressionType === 'Linear' && (
                            <Line
                              type="monotone"
                              dataKey="trainingMax"
                              name="Training Max"
                              stroke={chartColors.primary}
                              strokeWidth={2}
                              dot={false}
                            />
                          )}
                          {(selectedSeries.progressionType === 'RepsPerSet' ||
                            selectedSeries.progressionType === 'MinimalSets') && (
                            <Line
                              type="monotone"
                              dataKey="currentWeight"
                              name="Weight"
                              stroke={chartColors.primary}
                              strokeWidth={2}
                              dot={false}
                              connectNulls
                            />
                          )}
                        </LineChart>
                      </ResponsiveContainer>
                    </div>

                    {/* Data table */}
                    <div className="rounded-xl border border-border overflow-x-auto max-h-64 overflow-y-auto">
                      <table className="w-full">
                        <thead className="sticky top-0 bg-muted/50">
                          <tr className="border-b border-border">
                            <th className="px-3 py-2 text-left text-xs font-medium text-muted-foreground">Session</th>
                            <th className="px-3 py-2 text-left text-xs font-medium text-muted-foreground">Week</th>
                            <th className="px-3 py-2 text-left text-xs font-medium text-muted-foreground">Block</th>
                            <th className="px-3 py-2 text-right text-xs font-medium text-muted-foreground">
                              {selectedSeries.progressionType === 'Linear' ? 'TM' : 'Weight'}
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {selectedSeries.dataPoints.map((dp) => (
                            <tr
                              key={dp.session}
                              className="border-b border-border last:border-0 text-sm"
                            >
                              <td className="px-3 py-1.5">{dp.session}</td>
                              <td className="px-3 py-1.5">{dp.week}</td>
                              <td className="px-3 py-1.5">{dp.block}</td>
                              <td className="px-3 py-1.5 text-right font-medium">
                                {selectedSeries.progressionType === 'Linear'
                                  ? dp.trainingMax != null
                                    ? `${Math.round(dp.trainingMax * 100) / 100}kg`
                                    : '—'
                                  : dp.currentWeight != null
                                    ? `${dp.currentWeight}kg`
                                    : 'Pending'}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}

                {!selectedExercise && (
                  <p className="text-sm text-muted-foreground text-center py-4">
                    Select an exercise above to see detailed projection data
                  </p>
                )}
              </CardContent>
            </Card>
          </div>
        )}

        {!simulation && !loadingSimulation && !error && (
          <Card>
            <CardContent className="pt-6">
              <p className="text-center text-muted-foreground py-8">
                Select a workout and click "Run Simulation" to project your progression.
                <br />
                <span className="text-xs mt-1 block">
                  The simulator uses your actual progression algorithms with simulated AMRAP results.
                </span>
              </p>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}
