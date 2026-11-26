/**
 * The top of the Cardio page ("This week"): the week's zone 4–5 guardrails and any gated track waiting to start. Renders nothing while loading, on error, or
 * when there are no tracks, so the page never waits on it.
 */

import { useState } from 'react';
import toast from 'react-hot-toast';
import { useLogTrackSession, useStartGatedTrack, useTodayCardio } from '@/hooks/useCardioTracks';
import type { CardioTrackDto, LogTrackSessionRequest } from '@/types/cardioTracks';
import { TrackCard } from './TrackCard';
import { TrackLogSheet } from './TrackLogSheet';

export function CardioThisWeek() {
  const todayQuery = useTodayCardio();
  const log = useLogTrackSession();
  const start = useStartGatedTrack();
  const [logging, setLogging] = useState<CardioTrackDto | null>(null);
  const data = todayQuery.data;

  const gated = data?.gated ?? [];
  // Active tracks are listed in full below on the Cardio page; this section is the week's guardrails and the gates.
  if (!data || (data.warnings.length === 0 && gated.length === 0)) {
    return null;
  }

  const submit = (track: CardioTrackDto, request: LogTrackSessionRequest) =>
    log.mutate(
      { id: track.id, request, label: track.name },
      {
        onSuccess: (outcome) => {
          setLogging(null);
          toast.success(
            outcome.queued
              ? 'Saved on this phone. It will sync when you are back online.'
              : `${outcome.data.reason}. Next: ${outcome.data.track.prescription.title}`
          );
        },
        onError: () => toast.error("Couldn't save that. Try again."),
      }
    );

  return (
    <section aria-labelledby="cardio-tracks-heading" className="space-y-2">
      <div className="flex items-center justify-between gap-3">
        <h2 id="cardio-tracks-heading" className="text-section">
          This week
        </h2>
      </div>
      {data.warnings.map((warning) => (
        <p key={warning.code} role="status" className="rounded-lg border border-warning/50 bg-card p-3 text-sm text-foreground">
          {warning.message}
        </p>
      ))}
      {gated.map((track) => (
        <TrackCard
          key={track.id}
          track={track}
          compact
          onLog={setLogging}
          busy={start.isPending}
          onStartGate={(t) =>
            start.mutate(t.id, {
              onSuccess: () => toast.success(` started`),
              onError: () => toast.error("Couldn't start that track. Try again."),
            })
          }
        />
      ))}
      <TrackLogSheet key={logging?.id ?? 'none'} track={logging} onClose={() => setLogging(null)} onSubmit={submit} saving={log.isPending} />
    </section>
  );
}
