/**
 * ExerciseHistoryChart — Recharts-based performance chart for exercise drill-down.
 * Shows weight, volume, and estimated 1RM over time with time-period filtering.
 */

import { useState, useMemo } from 'react';
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

type TimePeriod = '1M' | '3M' | '6M' | '1Y' | 'ALL';
type Metric = 'weight' | 'volume' | 'e1rm';

interface SessionData {
  completedAt: string;
  weekNumber: number;
  sessionVolume: number;
  sets: {
    weight: number;
    weightUnit: string;
    actualReps: number;
    wasAmrap: boolean;
  }[];
}

interface ExerciseHistoryChartProps {
  sessions: SessionData[];
  weightUnit: string;
}

const PERIOD_MS: Record<Exclude<TimePeriod, 'ALL'>, number> = {
  '1M': 30 * 24 * 60 * 60 * 1000,
  '3M': 90 * 24 * 60 * 60 * 1000,
  '6M': 180 * 24 * 60 * 60 * 1000,
  '1Y': 365 * 24 * 60 * 60 * 1000,
};

const METRIC_CONFIG: Record<Metric, { label: string; color: string }> = {
  weight: { label: 'Max Weight', color: 'hsl(var(--primary))' },
  volume: { label: 'Volume', color: 'hsl(210, 100%, 50%)' },
  e1rm: { label: 'Est. 1RM (Epley)', color: 'hsl(30, 90%, 50%)' },
};

function estimateE1RM(weight: number, reps: number): number {
  if (reps <= 0 || weight <= 0) return 0;
  if (reps === 1) return weight;
  return Math.round(weight * (1 + reps / 30) * 10) / 10;
}

export function ExerciseHistoryChart({ sessions, weightUnit }: ExerciseHistoryChartProps) {
  const [timePeriod, setTimePeriod] = useState<TimePeriod>('ALL');
  const [activeMetric, setActiveMetric] = useState<Metric>('weight');

  const unitLabel = weightUnit === 'Kilograms' ? 'kg' : 'lbs';

  const filteredSessions = useMemo(() => {
    if (timePeriod === 'ALL') return sessions;

    const cutoff = new Date(Date.now() - PERIOD_MS[timePeriod]);
    return sessions.filter((s) => new Date(s.completedAt) >= cutoff);
  }, [sessions, timePeriod]);

  const chartData = useMemo(() => {
    return filteredSessions.map((s) => {
      const maxWeight =
        s.sets.length > 0 ? Math.max(...s.sets.map((set) => set.weight)) : 0;
      const bestE1RM =
        s.sets.length > 0
          ? Math.max(...s.sets.map((set) => estimateE1RM(set.weight, set.actualReps)))
          : 0;

      return {
        date: new Date(s.completedAt).toLocaleDateString(undefined, {
          month: 'short',
          day: 'numeric',
        }),
        fullDate: new Date(s.completedAt).toLocaleDateString(),
        week: `W${s.weekNumber}`,
        weight: maxWeight,
        volume: Math.round(s.sessionVolume),
        e1rm: bestE1RM,
      };
    });
  }, [filteredSessions]);

  const periods: TimePeriod[] = ['1M', '3M', '6M', '1Y', 'ALL'];
  const metrics: Metric[] = ['weight', 'volume', 'e1rm'];

  if (sessions.length === 0) {
    return (
      <div className="text-center py-8 text-muted-foreground">
        No session data to chart.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Controls */}
      <div className="flex flex-col sm:flex-row gap-3 items-start sm:items-center justify-between">
        {/* Time period filter */}
        <div className="flex gap-1.5">
          {periods.map((p) => (
            <button
              key={p}
              onClick={() => setTimePeriod(p)}
              className={`px-3 py-1.5 text-xs font-medium rounded-lg transition-colors ${
                timePeriod === p
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground hover:text-foreground'
              }`}
            >
              {p}
            </button>
          ))}
        </div>

        {/* Metric selector */}
        <div className="flex gap-1.5">
          {metrics.map((m) => (
            <button
              key={m}
              onClick={() => setActiveMetric(m)}
              className={`px-3 py-1.5 text-xs font-medium rounded-lg transition-colors ${
                activeMetric === m
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground hover:text-foreground'
              }`}
            >
              {METRIC_CONFIG[m].label}
            </button>
          ))}
        </div>
      </div>

      {/* Chart */}
      {chartData.length > 0 ? (
        <div className="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
              <XAxis
                dataKey="date"
                stroke="hsl(var(--muted-foreground))"
                fontSize={12}
                tick={{ fill: 'hsl(var(--muted-foreground))' }}
              />
              <YAxis
                stroke="hsl(var(--muted-foreground))"
                fontSize={12}
                tick={{ fill: 'hsl(var(--muted-foreground))' }}
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: 'hsl(var(--card))',
                  border: '1px solid hsl(var(--border))',
                  borderRadius: '8px',
                  color: 'hsl(var(--foreground))',
                }}
                formatter={(value) => {
                  const suffix = activeMetric === 'volume' ? '' : ` ${unitLabel}`;
                  return [`${value}${suffix}`, METRIC_CONFIG[activeMetric].label];
                }}
                labelFormatter={(label, payload) => {
                  const item = payload?.[0]?.payload;
                  return item ? `${item.fullDate} (${item.week})` : String(label);
                }}
              />
              <Legend />
              <Line
                type="monotone"
                dataKey={activeMetric}
                name={METRIC_CONFIG[activeMetric].label}
                stroke={METRIC_CONFIG[activeMetric].color}
                strokeWidth={2}
                dot={{ fill: METRIC_CONFIG[activeMetric].color, strokeWidth: 2, r: 3 }}
                activeDot={{ r: 5 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      ) : (
        <div className="flex items-center justify-center h-48 text-muted-foreground text-sm">
          No data for the selected time period.
        </div>
      )}

      {/* Summary stats for filtered data */}
      {chartData.length > 0 && (
        <div className="grid grid-cols-3 gap-3 text-center">
          <div className="rounded-lg bg-muted/30 p-3">
            <p className="text-xs text-muted-foreground">Sessions</p>
            <p className="text-lg font-semibold">{chartData.length}</p>
          </div>
          <div className="rounded-lg bg-muted/30 p-3">
            <p className="text-xs text-muted-foreground">Peak Weight</p>
            <p className="text-lg font-semibold">
              {Math.max(...chartData.map((d) => d.weight))} {unitLabel}
            </p>
          </div>
          <div className="rounded-lg bg-muted/30 p-3">
            <p className="text-xs text-muted-foreground">Peak Est. 1RM</p>
            <p className="text-lg font-semibold">
              {Math.max(...chartData.map((d) => d.e1rm))} {unitLabel}
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
