/**
 * Simulator projection charts (extracted from SimulationPage so they can be
 * shown on their own in Storybook). One line per exercise; colours come from the
 * shared chart palette so every series stays on-theme. See lib/chartTheme.ts.
 */
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import type { ExerciseSimulationSeries } from './simulationTypes';

const CHART_COLORS = chartSeriesPalette;

/**
 * One row per session with a column per exercise. Recharts shares a single category
 * axis, so giving each <Line> its own data array repeated the session ticks once per
 * series (1…43, 1…43, …); pivoting into one table keeps a single 1…N axis.
 */
function bySession(series: ExerciseSimulationSeries[], field: 'trainingMax' | 'currentWeight') {
  const rows = new Map<number, Record<string, number | null>>();
  for (const s of series) {
    for (const p of s.dataPoints) {
      const row = rows.get(p.session) ?? { session: p.session };
      row[s.exerciseId] = p[field];
      rows.set(p.session, row);
    }
  }
  return [...rows.values()].sort((a, b) => (a.session ?? 0) - (b.session ?? 0));
}

/** Training max per session for Linear (AMRAP-driven) lifts. */
export function TrainingMaxProjectionChart({ series }: { series: ExerciseSimulationSeries[] }) {
  if (series.length === 0) return null;
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-lg">Training Max Progression</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-72">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={bySession(series, 'trainingMax')}>
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
                formatter={(value, name) => [`${Math.round(Number(value) * 100) / 100}kg`, name]}
                labelFormatter={(label) => `Session ${label}`}
              />
              <Legend />
              {series.map((series, idx) => (
                <Line
                  key={series.exerciseId}
                  type="monotone"
                  dataKey={series.exerciseId}
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
  );
}

/** Working weight per session for RepsPerSet accessories. */
export function AccessoryWeightProjectionChart({ series }: { series: ExerciseSimulationSeries[] }) {
  if (series.length === 0) return null;
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-lg">Accessory Weight Progression</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-72">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={bySession(series, 'currentWeight')}>
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
                formatter={(value, name) => [value != null ? `${value}kg` : 'Pending', name]}
                labelFormatter={(label) => `Session ${label}`}
              />
              <Legend />
              {series.map((series, idx) => (
                <Line
                  key={series.exerciseId}
                  type="monotone"
                  dataKey={series.exerciseId}
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
  );
}
