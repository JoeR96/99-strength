/**
 * Read-only look at an upcoming cardio or mobility session: a cardio track's prescription and
 * "N more sessions to step up" (or a protocol's intervals), or a mobility routine's drill list.
 * Nothing here logs anything.
 */

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { stepUpText } from '@/features/cardio/trackText';
import { useCardioTracks } from '@/hooks/useCardioTracks';
import { useCardioProtocols } from '@/hooks/useGymSession';
import { useMobilityRoutines } from '@/hooks/useMobilityRoutines';
import type { LocalDate } from '@/lib/localDate';
import { DoseUnit, type MobilityDrillDto } from '@/types/mobility';
import { SessionKind, type TodaySessionDto } from '@/types/plan';
import { useScheduledSlot } from './useScheduledSlot';
import { dayHeading } from './upNext';

function dose(drill: MobilityDrillDto): string {
  const unit =
    drill.dose.unit === DoseUnit.Seconds
      ? 's'
      : drill.dose.unit === DoseUnit.Breaths
        ? ' breaths'
        : ' reps';
  return `${drill.sets} × ${drill.dose.amount}${unit}${drill.perSide ? ' each side' : ''}`;
}

function minutesText(seconds: number): string {
  return seconds % 60 === 0 ? `${seconds / 60} min` : `${seconds} s`;
}

function CardioBody({ session, date }: { session: TodaySessionDto; date: LocalDate }) {
  const slot = useScheduledSlot(session, date);
  const trackId = slot?.cardioTrackId ?? null;
  const tracks = useCardioTracks(Boolean(trackId));
  const protocols = useCardioProtocols(!trackId);
  const track = trackId ? tracks.data?.find((t) => t.id === trackId) : null;
  if (track) {
    return (
      <div className="space-y-1" data-testid="preview-track">
        <p className="font-semibold text-foreground">{track.prescription.title}</p>
        <p className="text-sm text-muted-foreground">{stepUpText(track)}</p>
      </div>
    );
  }
  const protocol = protocols.data?.find((p) => p.id === session.cardioProtocolId);
  if (!protocol) return <p className="text-sm text-muted-foreground">Loading…</p>;
  return (
    <div className="space-y-2" data-testid="preview-protocol">
      <p className="font-semibold text-foreground">
        {protocol.name} · {protocol.totalMinutes} min
      </p>
      {protocol.notes && <p className="text-sm text-muted-foreground">{protocol.notes}</p>}
      {protocol.intervals.length > 0 && (
        <ol className="space-y-1">
          {protocol.intervals.map((interval, i) => (
            <li
              key={`${interval.label}-${i}`}
              className="flex justify-between gap-2 text-sm text-foreground"
            >
              <span>{interval.label}</span>
              <span className="text-muted-foreground">{minutesText(interval.seconds)}</span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

function MobilityBody({ routineId }: { routineId: string | null }) {
  const routines = useMobilityRoutines();
  const routine = routines.data?.find((r) => r.id === routineId);
  if (!routine)
    return (
      <p className="text-sm text-muted-foreground">
        {routines.isLoading ? 'Loading…' : 'No routine attached.'}
      </p>
    );
  return (
    <div className="space-y-2" data-testid="preview-routine">
      <p className="font-semibold text-foreground">
        {routine.name} · {routine.targetMinutes} min
      </p>
      <ul className="space-y-1">
        {routine.drills.map((drill, i) => (
          <li
            key={`${drill.name}-${i}`}
            className="flex justify-between gap-2 text-sm text-foreground"
          >
            <span className="min-w-0 truncate">{drill.name}</span>
            <span className="shrink-0 text-muted-foreground">{dose(drill)}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

export interface SessionPreviewSheetProps {
  preview: { session: TodaySessionDto; date: LocalDate } | null;
  today: LocalDate;
  onClose: () => void;
}

export function SessionPreviewSheet({ preview, today, onClose }: SessionPreviewSheetProps) {
  return (
    <Dialog open={preview !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="bottom-0 top-auto translate-y-0 rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2">
        {preview && (
          <>
            <DialogHeader>
              <DialogTitle>{preview.session.label}</DialogTitle>
              <DialogDescription>Preview · {dayHeading(preview.date, today)}</DialogDescription>
            </DialogHeader>
            {preview.session.kind === SessionKind.Cardio ? (
              <CardioBody session={preview.session} date={preview.date} />
            ) : (
              <MobilityBody routineId={preview.session.mobilityRoutineId} />
            )}
            <DialogFooter>
              <Button variant="outline" className="h-11 w-full" onClick={onClose}>
                Close
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
