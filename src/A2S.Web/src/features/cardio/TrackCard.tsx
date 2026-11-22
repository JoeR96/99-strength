/** One cardio track: today's prescription, how close the next step is, any pending suggestion and the actions. */

import { Info, Pause, Play, Undo2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { CardioTrackStatus, modalityLabel, type CardioTrackDto } from '@/types/cardioTracks';
import { stepUpText } from './trackText';

export interface TrackCardProps {
  track: CardioTrackDto;
  onLog: (track: CardioTrackDto) => void;
  onDecide?: (track: CardioTrackDto, accept: boolean) => void;
  onUndo?: (track: CardioTrackDto) => void;
  onPause?: (track: CardioTrackDto, paused: boolean) => void;
  onInfo?: (track: CardioTrackDto) => void;
  busy?: boolean;
  /** Today shows the prescription and Log only. */
  compact?: boolean;
}

export function TrackCard({ track, onLog, onDecide, onUndo, onPause, onInfo, busy = false, compact = false }: TrackCardProps) {
  const paused = track.status === CardioTrackStatus.Paused;
  const headingId = `track-${track.id}-name`;

  return (
    <article className="space-y-3 rounded-lg border border-border bg-card p-4" aria-labelledby={headingId}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 id={headingId} className="text-lg font-semibold text-foreground">
            {track.name}
          </h3>
          <p className="text-sm text-muted-foreground">
            {modalityLabel[track.modality]} · {track.prescription.stageName}
          </p>
        </div>
        <div className="flex shrink-0 flex-wrap justify-end gap-1">
          {track.isHard && <Badge variant="warning">Zone 4–5</Badge>}
          {paused && <Badge variant="neutral">Paused</Badge>}
          {track.benchmarkDue && <Badge variant="primary">Benchmark due</Badge>}
        </div>
      </div>

      <div>
        <p className="text-xl font-semibold text-foreground" data-testid="prescription">
          {track.prescription.title}
        </p>
        {track.prescription.note && <p className="text-sm text-muted-foreground">{track.prescription.note}</p>}
        <p className="mt-1 text-sm text-foreground">{stepUpText(track)}</p>
      </div>

      {track.suggestionText && onDecide && (
        <div className="space-y-2 rounded-md border border-primary/50 p-3" role="status">
          <p className="text-sm text-foreground">{track.suggestionText}</p>
          <div className="grid grid-cols-2 gap-2">
            <Button onClick={() => onDecide(track, true)} disabled={busy}>
              Yes
            </Button>
            <Button variant="outline" onClick={() => onDecide(track, false)} disabled={busy}>
              No, carry on
            </Button>
          </div>
        </div>
      )}

      <Button className="w-full" onClick={() => onLog(track)} disabled={busy} aria-label={`Log session: ${track.name}`}>
        Log session
      </Button>

      {!compact && (
        <div className="grid grid-cols-3 gap-2">
          <Button variant="outline" onClick={() => onInfo?.(track)} aria-label={`Info about ${track.name}`} className="px-2">
            <Info aria-hidden="true" />
            Info
          </Button>
          <Button variant="outline" onClick={() => onPause?.(track, !paused)} disabled={busy} className="px-2" aria-label={`${paused ? "Resume" : "Pause"} ${track.name}`}>
            {paused ? <Play aria-hidden="true" /> : <Pause aria-hidden="true" />}
            {paused ? 'Resume' : 'Pause'}
          </Button>
          <Button variant="outline" onClick={() => onUndo?.(track)} disabled={busy || !track.canUndo} className="px-2" aria-label={`Undo last change to ${track.name}`}>
            <Undo2 aria-hidden="true" />
            Undo
          </Button>
        </div>
      )}
    </article>
  );
}
