/**
 * Progress for each cardio track: a step chart (one step per progression), the log of every step
 * and why, and the same data as a table for anyone who can't read the chart.
 */

import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { chartAxisProps, chartColors, chartTooltipContentStyle } from '@/lib/chartTheme';
import { formatShortDate } from '@/lib/localDate';
import { useCardioTrack, useCardioTracks } from '@/hooks/useCardioTracks';
import { outcomeLabel, type CardioTrackDetailDto } from '@/types/cardioTracks';

export function CardioTrackChart({ detail }: { detail: CardioTrackDetailDto }) {
  const { track, chart, audit, benchmarks } = detail;
  const headingId = `progress-${track.id}`;
  const steps = audit.filter((a) => a.before !== a.after || a.action !== 'Qualified');

  return (
    <section aria-labelledby={headingId} className="space-y-3 rounded-lg border border-border bg-card p-4" data-testid="cardio-track-progress">
      <div>
        <h3 id={headingId} className="text-sm font-medium text-muted-foreground">
          {track.name}
        </h3>
        <p className="text-xl font-semibold text-foreground">{track.prescription.title}</p>
        <p className="text-sm text-muted-foreground">
          {track.state.step} {track.state.step === 1 ? 'step' : 'steps'} up since {formatShortDate(track.startedOn)}
        </p>
      </div>

      {chart.length > 1 ? (
        <div className="h-48" aria-hidden="true">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={chart} margin={{ top: 8, right: 8, bottom: 0, left: -16 }}>
              <CartesianGrid stroke={chartColors.border} strokeDasharray="3 3" />
              <XAxis dataKey="date" tickFormatter={(d: string) => formatShortDate(d)} {...chartAxisProps} />
              <YAxis allowDecimals={false} {...chartAxisProps} />
              <Tooltip contentStyle={chartTooltipContentStyle} labelFormatter={(d) => formatShortDate(String(d))} />
              <Line type="stepAfter" dataKey="step" name="Step" stroke={chartColors.primary} strokeWidth={2} dot={{ fill: chartColors.primary }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">Log a couple of sessions to see the chart.</p>
      )}

      {steps.length > 0 && (
        <div>
          <h4 className="mb-1 text-sm font-medium text-foreground">Each change and why</h4>
          <ol className="space-y-2">
            {steps.map((entry) => (
              <li key={`${entry.atUtc}-${entry.action}`} className="text-sm">
                <span className="font-medium text-foreground">
                  {entry.date ? formatShortDate(entry.date) : formatShortDate(entry.atUtc.slice(0, 10))} · {outcomeLabel(entry.action)}
                </span>
                {entry.before !== entry.after && (
                  <span className="block text-foreground">
                    {entry.before} → {entry.after}
                  </span>
                )}
                <span className="block text-muted-foreground">{entry.reason}</span>
              </li>
            ))}
          </ol>
        </div>
      )}

      {chart.length > 0 && (
        <details>
          <summary className="flex min-h-11 cursor-pointer items-center text-sm font-medium text-primary">Sessions as a table</summary>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <caption className="sr-only">{track.name} sessions</caption>
              <thead>
                <tr className="text-muted-foreground">
                  <th scope="col" className="py-1 pr-2 font-medium">Date</th>
                  <th scope="col" className="py-1 pr-2 font-medium">Prescribed</th>
                  <th scope="col" className="py-1 pr-2 font-medium">Min</th>
                  <th scope="col" className="py-1 font-medium">Step</th>
                </tr>
              </thead>
              <tbody>
                {chart.map((point, i) => (
                  <tr key={`${point.date}-${i}`} className="border-t border-border text-foreground">
                    <td className="py-1 pr-2">{formatShortDate(point.date)}</td>
                    <td className="py-1 pr-2">{point.prescription}</td>
                    <td className="py-1 pr-2 tabular-nums">{Math.round(point.workMinutes)}</td>
                    <td className="py-1 tabular-nums">{point.step}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </details>
      )}

      {benchmarks.length > 0 && track.benchmark && (
        <p className="text-sm text-foreground">
          {track.benchmark.name}: best{' '}
          {benchmarks.filter((b) => b.isPersonalBest).at(-1)?.value ?? benchmarks[benchmarks.length - 1].value} {track.benchmark.unit}
        </p>
      )}
    </section>
  );
}

function TrackProgressLoader({ id }: { id: string }) {
  const query = useCardioTrack(id);
  if (query.isLoading) return <div className="h-48 animate-pulse rounded-lg bg-muted" aria-hidden="true" />;
  if (!query.data) return null;
  return <CardioTrackChart detail={query.data} />;
}

/** One chart per track; nothing at all without tracks (or while the list is unavailable). */
export function CardioTrackProgress() {
  const tracks = useCardioTracks();
  if (!tracks.data || tracks.data.length === 0) return null;
  return (
    <section aria-labelledby="cardio-progress-heading" className="space-y-3">
      <h2 id="cardio-progress-heading" className="text-lg font-semibold text-foreground">
        Cardio tracks
      </h2>
      {tracks.data.map((track) => (
        <TrackProgressLoader key={track.id} id={track.id} />
      ))}
    </section>
  );
}
