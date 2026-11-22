/**
 * Today's cardio tracks: each active track's prescription and "N more sessions to step up", with
 * a one-tap log, plus the week's zone 4–5 guardrail. Renders nothing while loading, on error, or
 * when there are no tracks, so Today never waits on it.
 */

import { useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useLogTrackSession, useStartGatedTrack, useTodayCardio } from '@/hooks/useCardioTracks';
import type { CardioTrackDto, LogTrackSessionRequest } from '@/types/cardioTracks';
import { TrackCard } from './TrackCard';
import { TrackLogSheet } from './TrackLogSheet';

export function TodayCardioTracks() {
  const todayQuery = useTodayCardio();
  const log = useLogTrackSession();
  const start = useStartGatedTrack();
  const [logging, setLogging] = useState<CardioTrackDto | null>(null);
  const data = todayQuery.data;

  const gated = data?.gated ?? [];
  if (!data || (data.tracks.length === 0 && gated.length === 0)) {
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
        <h2 id="cardio-tracks-heading" className="text-sm font-medium text-muted-foreground">
          Cardio tracks
        </h2>
        <Link to="/cardio-tracks" className="flex min-h-11 items-center text-sm font-medium text-primary underline-offset-4 hover:underline">
          All tracks
        </Link>
      </div>
      {data.warnings.map((warning) => (
        <p key={warning.code} role="status" className="rounded-lg border border-warning/50 bg-card p-3 text-sm text-foreground">
          {warning.message}
        </p>
      ))}
      {data.tracks.map((track) => (
        <TrackCard key={track.id} track={track} compact onLog={setLogging} busy={log.isPending} />
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
