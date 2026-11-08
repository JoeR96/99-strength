/** Pure helpers for the weight trend chart and the pace label. */

import { compareLocalDates, daysBetween, toEpochDays, type LocalDate } from '@/lib/localDate';
import type { Pace, PaceDto, TrendPointDto } from '@/types/bodyWeight';
import type { CheckpointDto } from '@/types/plan';

export interface TrendRow {
  date: LocalDate;
  /** Days since the epoch, so the chart can use a real time axis. */
  t: number;
  kg: number | null;
  ewma: number | null;
  movingAverage7: number | null;
  /** Straight-line target between checkpoints; null outside the checkpoint range. */
  target: number | null;
  /** Set on the checkpoint's own date so the chart can mark it. */
  checkpointKg: number | null;
}

export interface TargetAnchor {
  date: LocalDate;
  kg: number;
}

/** The straight-line target on `date`, or null before the first / after the last anchor. */
export function interpolateTarget(anchors: TargetAnchor[], date: LocalDate): number | null {
  if (anchors.length === 0) return null;
  const sorted = [...anchors].sort((a, b) => compareLocalDates(a.date, b.date));
  if (compareLocalDates(date, sorted[0].date) < 0) return null;
  const last = sorted[sorted.length - 1];
  if (compareLocalDates(date, last.date) > 0) return null;
  for (let i = 0; i < sorted.length - 1; i++) {
    const a = sorted[i];
    const b = sorted[i + 1];
    if (compareLocalDates(date, a.date) >= 0 && compareLocalDates(date, b.date) <= 0) {
      const span = daysBetween(a.date, b.date);
      if (span === 0) return b.kg;
      const t = daysBetween(a.date, date) / span;
      return Math.round((a.kg + (b.kg - a.kg) * t) * 100) / 100;
    }
  }
  return last.kg;
}

/** Start weight on the start date plus every checkpoint, oldest first. */
export function targetAnchors(startDate: LocalDate | undefined, startKg: number | undefined, checkpoints: CheckpointDto[] | undefined): TargetAnchor[] {
  const anchors: TargetAnchor[] = [];
  if (startDate && startKg !== undefined) anchors.push({ date: startDate, kg: startKg });
  for (const c of checkpoints ?? []) anchors.push({ date: c.date, kg: c.targetKg });
  return anchors.sort((a, b) => compareLocalDates(a.date, b.date));
}

/** Merges daily points and checkpoint dates into one series for Recharts. */
export function buildTrendSeries(points: TrendPointDto[], anchors: TargetAnchor[], checkpoints: CheckpointDto[] = []): TrendRow[] {
  const byDate = new Map<LocalDate, TrendRow>();
  const ensure = (date: LocalDate): TrendRow => {
    let row = byDate.get(date);
    if (!row) {
      row = { date, t: toEpochDays(date), kg: null, ewma: null, movingAverage7: null, target: null, checkpointKg: null };
      byDate.set(date, row);
    }
    return row;
  };
  for (const p of points) {
    const row = ensure(p.date);
    row.kg = p.kg;
    row.ewma = p.ewma;
    row.movingAverage7 = p.movingAverage7;
  }
  for (const c of checkpoints) ensure(c.date).checkpointKg = c.targetKg;
  for (const a of anchors) ensure(a.date);
  for (const row of byDate.values()) row.target = interpolateTarget(anchors, row.date);
  return [...byDate.values()].sort((a, b) => compareLocalDates(a.date, b.date));
}

/**
 * The rows worth plotting: from the first weigh-in to the next checkpoint after `today`
 * (or the last weigh-in when none is ahead). Checkpoints months away would otherwise
 * squash the data into a sliver.
 */
export function windowRows(rows: TrendRow[], today: LocalDate, checkpoints: CheckpointDto[] = []): TrendRow[] {
  const firstPoint = rows.find((r) => r.kg !== null);
  const lastPoint = [...rows].reverse().find((r) => r.kg !== null);
  if (!firstPoint || !lastPoint) return [];
  const nextCheckpoint = [...checkpoints]
    .sort((a, b) => compareLocalDates(a.date, b.date))
    .find((c) => compareLocalDates(c.date, today) > 0);
  const end = nextCheckpoint && compareLocalDates(nextCheckpoint.date, lastPoint.date) > 0 ? nextCheckpoint.date : lastPoint.date;
  return rows.filter((r) => compareLocalDates(r.date, firstPoint.date) >= 0 && compareLocalDates(r.date, end) <= 0);
}

export interface PaceLabel {
  text: string;
  tone: 'success' | 'warning' | 'primary' | 'muted';
  /** "+0.4 kg" / "−0.6 kg" relative to the target line, absent for NoTarget. */
  delta: string | null;
}

export function formatDelta(deltaKg: number): string {
  const rounded = Math.round(deltaKg * 10) / 10;
  const sign = rounded > 0 ? '+' : rounded < 0 ? '−' : '';
  return `${sign}${Math.abs(rounded).toFixed(1)} kg`;
}

const paceText: Record<Pace, string> = {
  OnPace: 'On pace',
  Behind: 'Behind',
  Ahead: 'Ahead',
  NoTarget: 'No target',
};

const paceTone: Record<Pace, PaceLabel['tone']> = {
  OnPace: 'success',
  Behind: 'warning',
  Ahead: 'primary',
  NoTarget: 'muted',
};

export function paceLabel(pace: PaceDto | null | undefined): PaceLabel {
  if (!pace || pace.pace === 'NoTarget') return { text: paceText.NoTarget, tone: 'muted', delta: null };
  return { text: paceText[pace.pace], tone: paceTone[pace.pace], delta: formatDelta(pace.deltaKg) };
}

/** Y-axis bounds with a little headroom; falls back to a sane band with no data. */
export function yDomain(rows: TrendRow[]): [number, number] {
  const values = rows.flatMap((r) => [r.kg, r.ewma, r.target, r.checkpointKg]).filter((v): v is number => v !== null);
  if (values.length === 0) return [80, 100];
  const min = Math.floor(Math.min(...values) - 1);
  const max = Math.ceil(Math.max(...values) + 1);
  return [min, max];
}
