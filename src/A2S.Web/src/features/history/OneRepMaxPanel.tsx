import { useMemo, useState } from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Card } from '@/components/ui/card';
import { ToggleButton } from '@/components/ui/toggle-button';
import { chartAxisProps, chartColors, chartTooltipContentStyle } from '@/lib/chartTheme';
import { oneRepMaxSeries, unitLabel, type OneRepMaxPoint } from './historyStats';
import type { ExerciseHistoryDto } from './historyTypes';

/** "Squat (Barbell)" reads better as "Squat" on a small button. */
const shortName = (name: string) => name.replace(/\s*\(.*\)$/, '');

function PointTooltip({
  active,
  payload,
  unit,
}: {
  active?: boolean;
  payload?: Array<{ payload: OneRepMaxPoint }>;
  unit: string;
}) {
  const point = active ? payload?.[0]?.payload : undefined;
  if (!point) return null;
  return (
    <div style={chartTooltipContentStyle} className="px-3 py-2">
      <p className="text-xs text-muted-foreground">
        Week {point.weekNumber} · Block {point.blockNumber}
      </p>
      <p className="font-semibold">
        {point.estimate.toFixed(1)} {unit}
      </p>
      <p className="font-mono text-xs text-muted-foreground">
        from {point.weight} {unit} × {point.reps}
      </p>
    </div>
  );
}

/**
 * Estimated one-rep max for each main lift, week by week.
 * Each point comes from the week's AMRAP set, so the line is what the lifter could do, not what was programmed.
 */
export function OneRepMaxPanel({ exercises }: { exercises: ExerciseHistoryDto[] }) {
  const lifts = useMemo(
    () =>
      exercises
        .map((exercise) => ({ exercise, series: oneRepMaxSeries(exercise) }))
        .filter((lift) => lift.series.length > 0),
    [exercises]
  );
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const lift = lifts.find((l) => l.exercise.exerciseId === selectedId) ?? lifts[0];

  if (!lift) return null;

  const { exercise, series } = lift;
  const unit = unitLabel(exercise.weightUnit);
  const first = series[0];
  const latest = series[series.length - 1];
  const best = series.reduce((a, b) => (b.estimate > a.estimate ? b : a));
  const change = latest.estimate - first.estimate;

  const values = series.map((p) => p.estimate).concat(exercise.trainingMax ?? []);
  const domain: [number, number] = [
    Math.floor((Math.min(...values) - 5) / 10) * 10,
    Math.ceil((Math.max(...values) + 5) / 10) * 10,
  ];

  const stats: [string, string, string?][] = [
    ['Now', `${latest.estimate.toFixed(1)} ${unit}`, `week ${latest.weekNumber}`],
    ['Since week ' + first.weekNumber, `${change >= 0 ? '+' : ''}${change.toFixed(1)} ${unit}`],
    ['Best set', `${best.weight} ${unit} × ${best.reps}`, `week ${best.weekNumber}`],
  ];

  return (
    <Card className="p-6">
      <div className="mb-4 flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h2 className="text-xl font-semibold text-foreground">Estimated one-rep max</h2>
          <p className="text-caption mt-1">
            From each week&apos;s last set, taken for as many reps as possible (Epley formula).
          </p>
        </div>
        <div className="flex flex-wrap gap-2" role="group" aria-label="Lift">
          {lifts.map((l) => (
            <ToggleButton
              key={l.exercise.exerciseId}
              pressed={l === lift}
              onClick={() => setSelectedId(l.exercise.exerciseId)}
            >
              {shortName(l.exercise.name)}
            </ToggleButton>
          ))}
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_12rem]">
        <div className="h-64 lg:h-auto lg:min-h-64" role="img" aria-label={`${exercise.name}: estimated one-rep max by week`}>
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={series} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke={chartColors.border} vertical={false} />
              <XAxis dataKey="label" {...chartAxisProps} tickLine={false} />
              <YAxis {...chartAxisProps} domain={domain} tickLine={false} axisLine={false} width={40} />
              <Tooltip content={<PointTooltip unit={unit} />} cursor={{ stroke: chartColors.border }} />
              {exercise.trainingMax && (
                <ReferenceLine
                  y={exercise.trainingMax}
                  stroke={chartColors.mutedForeground}
                  strokeDasharray="6 4"
                  label={{ value: `Training max ${exercise.trainingMax} ${unit}`, position: 'insideTopLeft', fill: chartColors.mutedForeground, fontSize: 12 }}
                />
              )}
              <Line
                type="monotone"
                dataKey="estimate"
                stroke={chartColors.primary}
                strokeWidth={2}
                dot={{ fill: chartColors.primary, strokeWidth: 0, r: 4 }}
                activeDot={{ r: 6 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>

        <dl className="grid grid-cols-3 gap-3 lg:grid-cols-1">
          {stats.map(([label, value, note]) => (
            <div key={label} className="rounded-md bg-muted/30 p-3">
              <dt className="text-xs text-muted-foreground">{label}</dt>
              <dd className="text-lg font-semibold text-foreground">
                {value}
                {note && <span className="ml-2 text-xs font-normal text-muted-foreground">{note}</span>}
              </dd>
            </div>
          ))}
        </dl>
      </div>
    </Card>
  );
}
