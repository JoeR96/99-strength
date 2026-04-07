/**
 * Hevy Exercise History Modal
 * Shows historical weight/reps/sets/volume charts for a specific exercise
 */

import { useState, useMemo, useEffect, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/apiClient';
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

interface SetDetail {
  weightKg: number;
  reps: number;
  type: string;
}

interface ExerciseSessionPoint {
  date: string;
  workoutTitle: string;
  sets: number;
  maxWeight: number;
  maxReps: number;
  totalVolume: number;
  avgWeight: number;
  avgReps: number;
  setDetails: SetDetail[];
}

interface HevyExerciseHistoryResponse {
  exerciseTemplateId: string;
  sessions: ExerciseSessionPoint[];
  totalSessions: number;
}

type TimePeriod = '1M' | '3M' | '6M' | '1Y' | 'ALL';

interface HevyExerciseModalProps {
  exerciseTemplateId: string;
  exerciseTitle: string;
  apiKey: string | null;
  onClose: () => void;
}

function useExerciseHistory(templateId: string, apiKey: string | null) {
  return useQuery({
    queryKey: ['hevy-exercise-history', templateId],
    queryFn: async () => {
      const response = await apiClient.get<HevyExerciseHistoryResponse>(
        `/hevy/data/exercises/${templateId}/history?maxPages=20`,
        { headers: { 'X-Hevy-Api-Key': apiKey ?? '' } }
      );
      return response.data;
    },
    enabled: !!apiKey && !!templateId,
    staleTime: 1000 * 60 * 10,
  });
}

export function HevyExerciseModal({
  exerciseTemplateId,
  exerciseTitle,
  apiKey,
  onClose,
}: HevyExerciseModalProps) {
  const { data, isLoading, error } = useExerciseHistory(exerciseTemplateId, apiKey);
  const [timePeriod, setTimePeriod] = useState<TimePeriod>('ALL');
  const [chartMetric, setChartMetric] = useState<'weight' | 'volume' | 'reps'>('weight');

  const filteredSessions = useMemo(() => {
    if (!data?.sessions) return [];

    const now = new Date();
    let cutoff: Date;

    switch (timePeriod) {
      case '1M':
        cutoff = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
        break;
      case '3M':
        cutoff = new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000);
        break;
      case '6M':
        cutoff = new Date(now.getTime() - 180 * 24 * 60 * 60 * 1000);
        break;
      case '1Y':
        cutoff = new Date(now.getTime() - 365 * 24 * 60 * 60 * 1000);
        break;
      default:
        return data.sessions;
    }

    return data.sessions.filter((s) => new Date(s.date) >= cutoff);
  }, [data?.sessions, timePeriod]);

  const chartData = useMemo(() => {
    return filteredSessions.map((s) => ({
      date: new Date(s.date).toLocaleDateString(undefined, {
        month: 'short',
        day: 'numeric',
      }),
      fullDate: new Date(s.date).toLocaleDateString(),
      maxWeight: s.maxWeight,
      totalVolume: Math.round(s.totalVolume),
      avgWeight: Math.round(s.avgWeight * 10) / 10,
      maxReps: s.maxReps,
      sets: s.sets,
    }));
  }, [filteredSessions]);

  const periods: TimePeriod[] = ['1M', '3M', '6M', '1Y', 'ALL'];
  const metrics = [
    { key: 'weight' as const, label: 'Weight' },
    { key: 'volume' as const, label: 'Volume' },
    { key: 'reps' as const, label: 'Reps' },
  ];

  const chartConfig = {
    weight: { dataKey: 'maxWeight', label: 'Max Weight (kg)', color: 'hsl(var(--primary))' },
    volume: { dataKey: 'totalVolume', label: 'Total Volume (kg)', color: 'hsl(210, 100%, 50%)' },
    reps: { dataKey: 'maxReps', label: 'Max Reps', color: 'hsl(150, 80%, 40%)' },
  };

  const activeConfig = chartConfig[chartMetric];
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKeyDown);
    dialogRef.current?.focus();
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-label={`${exerciseTitle} exercise history`}
      ref={dialogRef}
      tabIndex={-1}
    >
      <div
        className="w-full max-w-3xl max-h-[90vh] overflow-y-auto rounded-xl border border-border bg-card p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-xl font-bold text-foreground">{exerciseTitle}</h2>
          <button
            onClick={onClose}
            className="rounded-lg p-2 text-muted-foreground transition-colors hover:bg-muted"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M18 6 6 18" /><path d="m6 6 12 12" />
            </svg>
          </button>
        </div>

        {isLoading && (
          <div className="flex items-center justify-center py-12">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
            <span className="ml-3 text-muted-foreground">Loading history...</span>
          </div>
        )}

        {error && (
          <p className="text-destructive py-4">
            Failed to load exercise history: {error instanceof Error ? error.message : 'Unknown error'}
          </p>
        )}

        {data && data.sessions.length === 0 && (
          <p className="text-center text-muted-foreground py-8">
            No history found for this exercise.
          </p>
        )}

        {data && data.sessions.length > 0 && (
          <div className="space-y-6">
            {/* Stats Summary */}
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <div className="rounded-lg bg-muted/30 p-3">
                <p className="text-xs text-muted-foreground">Sessions</p>
                <p className="text-xl font-semibold">{filteredSessions.length}</p>
              </div>
              <div className="rounded-lg bg-muted/30 p-3">
                <p className="text-xs text-muted-foreground">PR Weight</p>
                <p className="text-xl font-semibold">
                  {Math.max(...filteredSessions.map((s) => s.maxWeight))}kg
                </p>
              </div>
              <div className="rounded-lg bg-muted/30 p-3">
                <p className="text-xs text-muted-foreground">Best Volume</p>
                <p className="text-xl font-semibold">
                  {Math.round(Math.max(...filteredSessions.map((s) => s.totalVolume))).toLocaleString()}kg
                </p>
              </div>
              <div className="rounded-lg bg-muted/30 p-3">
                <p className="text-xs text-muted-foreground">Best Reps</p>
                <p className="text-xl font-semibold">
                  {Math.max(...filteredSessions.map((s) => s.maxReps))}
                </p>
              </div>
            </div>

            {/* Controls */}
            <div className="flex flex-wrap items-center gap-4">
              {/* Time period filter */}
              <div className="flex gap-1 rounded-lg border border-border p-1">
                {periods.map((period) => (
                  <button
                    key={period}
                    onClick={() => setTimePeriod(period)}
                    className={`rounded-md px-3 py-1 text-sm font-medium transition-colors ${
                      timePeriod === period
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    {period}
                  </button>
                ))}
              </div>

              {/* Metric selector */}
              <div className="flex gap-1 rounded-lg border border-border p-1">
                {metrics.map((metric) => (
                  <button
                    key={metric.key}
                    onClick={() => setChartMetric(metric.key)}
                    className={`rounded-md px-3 py-1 text-sm font-medium transition-colors ${
                      chartMetric === metric.key
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    {metric.label}
                  </button>
                ))}
              </div>
            </div>

            {/* Chart */}
            {chartData.length > 0 && (
              <div className="rounded-xl border border-border bg-card p-4">
                <h3 className="text-sm font-medium text-muted-foreground mb-3">
                  {activeConfig.label}
                </h3>
                <div className="h-64">
                  <ResponsiveContainer width="100%" height="100%">
                    <LineChart data={chartData}>
                      <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                      <XAxis
                        dataKey="date"
                        stroke="hsl(var(--muted-foreground))"
                        fontSize={11}
                        interval="preserveStartEnd"
                      />
                      <YAxis
                        stroke="hsl(var(--muted-foreground))"
                        fontSize={11}
                        width={50}
                      />
                      <Tooltip
                        contentStyle={{
                          backgroundColor: 'hsl(var(--card))',
                          border: '1px solid hsl(var(--border))',
                          borderRadius: '8px',
                          fontSize: '13px',
                        }}
                        formatter={(value) => [
                          `${value}${chartMetric === 'reps' ? '' : 'kg'}`,
                          activeConfig.label,
                        ]}
                        labelFormatter={(label, payload) => {
                          const point = payload?.[0]?.payload;
                          return point?.fullDate ?? label;
                        }}
                      />
                      <Legend />
                      <Line
                        type="monotone"
                        dataKey={activeConfig.dataKey}
                        name={activeConfig.label}
                        stroke={activeConfig.color}
                        strokeWidth={2}
                        dot={{ fill: activeConfig.color, strokeWidth: 2, r: 3 }}
                        activeDot={{ r: 5 }}
                      />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              </div>
            )}

            {/* Session Table */}
            <div className="rounded-xl border border-border">
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead>
                    <tr className="border-b border-border bg-muted/30">
                      <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">Date</th>
                      <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">Workout</th>
                      <th className="px-4 py-3 text-right text-sm font-medium text-muted-foreground">Sets</th>
                      <th className="px-4 py-3 text-right text-sm font-medium text-muted-foreground">Max Weight</th>
                      <th className="px-4 py-3 text-right text-sm font-medium text-muted-foreground">Max Reps</th>
                      <th className="px-4 py-3 text-right text-sm font-medium text-muted-foreground">Volume</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredSessions
                      .slice()
                      .reverse()
                      .map((session, idx) => (
                        <tr
                          key={idx}
                          className="border-b border-border last:border-0 transition-colors hover:bg-muted/20"
                        >
                          <td className="px-4 py-3 text-sm">
                            {new Date(session.date).toLocaleDateString(undefined, {
                              month: 'short',
                              day: 'numeric',
                              year: '2-digit',
                            })}
                          </td>
                          <td className="px-4 py-3 text-sm text-muted-foreground">
                            {session.workoutTitle}
                          </td>
                          <td className="px-4 py-3 text-right text-sm">{session.sets}</td>
                          <td className="px-4 py-3 text-right text-sm font-medium">
                            {session.maxWeight}kg
                          </td>
                          <td className="px-4 py-3 text-right text-sm">{session.maxReps}</td>
                          <td className="px-4 py-3 text-right text-sm">
                            {Math.round(session.totalVolume).toLocaleString()}kg
                          </td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
