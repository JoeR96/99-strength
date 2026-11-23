/**
 * A finisher's three parts: an easy warm-up, the progressing work block, an easy cool-down, and the total.
 * Renders nothing extra when the track has no warm-up or cool-down (the work line is then the whole session).
 */

import type { TrackSegmentDto } from '@/types/cardioTracks';
import { cn } from '@/lib/utils';

export interface FinisherPartsProps {
  work: string;
  warmUp?: TrackSegmentDto | null;
  coolDown?: TrackSegmentDto | null;
  totalMinutes?: number | null;
  className?: string;
}

function segmentText(segment: TrackSegmentDto): string {
  return [`${segment.minutes} min easy`, segment.level != null ? `L${segment.level}` : null, segment.zone].filter(Boolean).join(' · ');
}

/** "3 min easy (L3) → 5 min continuous @ level 6 → 3 min easy (L3)". */
export function finisherText(work: string, warmUp?: TrackSegmentDto | null, coolDown?: TrackSegmentDto | null): string {
  const easy = (s: TrackSegmentDto) => (s.level != null ? `${s.minutes} min easy (L${s.level})` : `${s.minutes} min easy`);
  return [warmUp ? easy(warmUp) : null, work, coolDown ? easy(coolDown) : null].filter(Boolean).join(' → ');
}

export function hasSegments(warmUp?: TrackSegmentDto | null, coolDown?: TrackSegmentDto | null): boolean {
  return !!warmUp || !!coolDown;
}

export function FinisherParts({ work, warmUp, coolDown, totalMinutes, className }: FinisherPartsProps) {
  if (!hasSegments(warmUp, coolDown)) {
    return <p className={cn('font-semibold text-foreground', className)}>{work}</p>;
  }
  return (
    <div className={className} data-testid="finisher-parts">
      <ol className="space-y-1 text-sm" aria-label="Finisher parts">
        {warmUp && (
          <li className="flex gap-2">
            <span className="w-20 shrink-0 text-muted-foreground">Warm-up</span>
            <span className="text-foreground">{segmentText(warmUp)}</span>
          </li>
        )}
        <li className="flex gap-2">
          <span className="w-20 shrink-0 text-muted-foreground">Work</span>
          <span className="font-semibold text-foreground">{work}</span>
        </li>
        {coolDown && (
          <li className="flex gap-2">
            <span className="w-20 shrink-0 text-muted-foreground">Cool-down</span>
            <span className="text-foreground">{segmentText(coolDown)}</span>
          </li>
        )}
      </ol>
      {totalMinutes != null && totalMinutes > 0 && <p className="mt-1 text-sm text-muted-foreground">{totalMinutes} min total</p>}
    </div>
  );
}
