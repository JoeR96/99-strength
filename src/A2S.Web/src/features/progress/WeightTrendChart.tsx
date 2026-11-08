/**
 * Weight trend: daily weigh-ins as dots, the smoothed EWMA line, the straight
 * target line between checkpoints with a marker on each checkpoint, and the
 * pace label (On pace / Behind / Ahead with the delta to the line).
 */

import { useMemo } from 'react';
import {
  CartesianGrid,
  ComposedChart,
  Line,
  ReferenceDot,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Badge } from '@/components/ui/badge';
import { chartAxisProps, chartColors, chartTooltipContentStyle } from '@/lib/chartTheme';
import { formatShortDate, fromEpochDays, type LocalDate } from '@/lib/localDate';
import type { WeightTrendDto } from '@/types/bodyWeight';
import type { CheckpointDto } from '@/types/plan';
import { buildTrendSeries, paceLabel, targetAnchors, windowRows, yDomain } from './trend';

export interface WeightTrendChartProps {
  trend: WeightTrendDto | undefined;
  checkpoints?: CheckpointDto[];
  startDate?: string;
  startKg?: number;
  /** Today's local date; the chart runs from the first weigh-in to the next checkpoint after it. */
  today?: LocalDate;
}

const toEpochDaysOf = (date: LocalDate) => Math.round(Date.UTC(Number(date.slice(0, 4)), Number(date.slice(5, 7)) - 1, Number(date.slice(8, 10))) / 86_400_000);

const paceBadgeVariant = { success: 'success', warning: 'warning', primary: 'primary', muted: 'neutral' } as const;

export function WeightTrendChart({ trend, checkpoints = [], startDate, startKg, today }: WeightTrendChartProps) {
  const anchors = useMemo(() => targetAnchors(startDate, startKg, checkpoints), [startDate, startKg, checkpoints]);
  const allRows = useMemo(() => buildTrendSeries(trend?.points ?? [], anchors, checkpoints), [trend?.points, anchors, checkpoints]);
  const lastDate = trend?.points[trend.points.length - 1]?.date;
  const rows = useMemo(() => windowRows(allRows, today ?? lastDate ?? '', checkpoints), [allRows, today, lastDate, checkpoints]);
  const visibleCheckpoints = checkpoints.filter((c) => rows.some((r) => r.date === c.date));
  const formatTick = (t: number) => formatShortDate(fromEpochDays(t));
  const pace = paceLabel(trend?.pace);
  const latest = trend?.points[trend.points.length - 1];
  const hasMa7 = rows.some((r) => r.movingAverage7 !== null);

  return (
    <section aria-labelledby="weight-trend-heading" className="rounded-lg border border-border bg-card p-4" data-testid="weight-trend">
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h2 id="weight-trend-heading" className="text-sm font-medium text-muted-foreground">
            Weight trend
          </h2>
          <p className="text-2xl font-semibold tabular-nums text-foreground">
            {latest ? latest.ewma.toFixed(1) : '—'}
            <span className="text-base font-normal text-muted-foreground"> kg trend</span>
          </p>
          {latest && (
            <p className="text-xs text-muted-foreground">
              Last weigh-in {latest.kg.toFixed(1)} kg on {formatShortDate(latest.date)}
            </p>
          )}
        </div>
        <div className="flex flex-col items-end gap-1" data-testid="pace-label">
          <Badge variant={paceBadgeVariant[pace.tone]} className="h-7 px-3 text-sm" data-tone={pace.tone}>
            {pace.text}
          </Badge>
          {pace.delta && <span className="text-xs tabular-nums text-muted-foreground">{pace.delta} vs target</span>}
        </div>
      </div>

      {(trend?.points.length ?? 0) === 0 ? (
        <p className="rounded-md border border-dashed border-border p-6 text-center text-sm text-muted-foreground" data-testid="trend-empty">
          No weigh-ins yet. Log one on Today and the trend starts here.
        </p>
      ) : (
        <div className="h-56 w-full" data-testid="trend-chart">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={rows} margin={{ top: 8, right: 8, bottom: 0, left: -16 }}>
              <CartesianGrid stroke={chartColors.border} strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="t" type="number" domain={['dataMin', 'dataMax']} tickFormatter={formatTick} {...chartAxisProps} minTickGap={28} />
              <YAxis domain={yDomain(rows)} {...chartAxisProps} width={44} tickFormatter={(v: number) => v.toFixed(0)} />
              <Tooltip
                contentStyle={chartTooltipContentStyle}
                labelFormatter={(label) => formatTick(Number(label))}
                formatter={(value, name) => [typeof value === 'number' ? `${value.toFixed(1)} kg` : '—', String(name)]}
              />
              <Line type="monotone" dataKey="target" name="Target" stroke={chartColors.mutedForeground} strokeWidth={1.5} strokeDasharray="4 4" dot={false} connectNulls isAnimationActive={false} />
              <Line type="monotone" dataKey="kg" name="Weigh-in" stroke="transparent" dot={{ r: 2.5, fill: chartColors.accent, stroke: 'none' }} activeDot={{ r: 4 }} isAnimationActive={false} connectNulls={false} />
              {hasMa7 && (
                <Line type="monotone" dataKey="movingAverage7" name="7-day average" stroke={chartColors.success} strokeWidth={1} dot={false} connectNulls isAnimationActive={false} />
              )}
              <Line type="monotone" dataKey="ewma" name="Trend" stroke={chartColors.primary} strokeWidth={2.5} dot={false} connectNulls isAnimationActive={false} />
              {visibleCheckpoints.map((c) => (
                <ReferenceDot key={c.date} x={toEpochDaysOf(c.date)} y={c.targetKg} r={5} fill={chartColors.card} stroke={chartColors.mutedForeground} strokeWidth={2} />
              ))}
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      )}

      <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-[11px] text-muted-foreground">
        <li className="flex items-center gap-1">
          <span className="h-0.5 w-4" style={{ background: chartColors.primary }} aria-hidden="true" /> trend
        </li>
        <li className="flex items-center gap-1">
          <span className="h-2 w-2 rounded-full" style={{ background: chartColors.accent }} aria-hidden="true" /> weigh-in
        </li>
        {hasMa7 && (
          <li className="flex items-center gap-1">
            <span className="h-0.5 w-4" style={{ background: chartColors.success }} aria-hidden="true" /> 7-day average
          </li>
        )}
        <li className="flex items-center gap-1">
          <span className="h-0 w-4 border-t border-dashed" style={{ borderColor: chartColors.mutedForeground }} aria-hidden="true" /> target
        </li>
      </ul>
    </section>
  );
}
