/**
 * Cardio (`/cardio`, also `/cardio-tracks`): this week's prescriptions, the running gate and the zone 4–5
 * guardrails at the top (moved off Today), then every track. Each one progresses on its own, the way A2S2 does for lifts. Start one from a
 * template (walk builder, walk → run, stair finisher), log sessions with one effort tap, accept or
 * decline what it suggests, pause it, undo its last change, or edit its numbers in the info sheet.
 */

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Plus, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import {
  useCardioTracks,
  useCardioTrackTemplates,
  useDecideSuggestion,
  useLogTrackSession,
  useSetTrackPaused,
  useStartCardioTrack,
  useUndoTrack,
  useStartGatedTrack,
  useApplyDefaultGate,
  useUpdateCardioTrack,
} from '@/hooks/useCardioTracks';
import type { CardioTrackDto, LogTrackSessionRequest, UpdateCardioTrackRequest } from '@/types/cardioTracks';
import { TrackCard } from './TrackCard';
import { CardioThisWeek } from './CardioThisWeek';
import { TrackLogSheet } from './TrackLogSheet';
import { TrackInfoSheet } from './TrackInfoSheet';
import { clearTrackInfoDraft } from './trackInfoDraft';

export function TracksPage() {
  const tracksQuery = useCardioTracks();
  const templatesQuery = useCardioTrackTemplates();
  const start = useStartCardioTrack();
  const log = useLogTrackSession();
  const decide = useDecideSuggestion();
  const undo = useUndoTrack();
  const startGated = useStartGatedTrack();
  const applyGate = useApplyDefaultGate();
  const setPaused = useSetTrackPaused();
  const update = useUpdateCardioTrack();
  const [logging, setLogging] = useState<CardioTrackDto | null>(null);
  const [info, setInfo] = useState<CardioTrackDto | null>(null);

  const tracks = tracksQuery.data;
  const busy = log.isPending || decide.isPending || undo.isPending || setPaused.isPending;
  const fail = () => toast.error("Couldn't save that. Try again.");

  const submitLog = (track: CardioTrackDto, request: LogTrackSessionRequest) =>
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
        onError: fail,
      }
    );

  const save = (track: CardioTrackDto, request: UpdateCardioTrackRequest) =>
    update.mutate(
      { id: track.id, request },
      {
        onSuccess: () => {
          clearTrackInfoDraft(track.id);
          setInfo(null);
        },
        onError: fail,
      }
    );

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page space-y-4 py-4">
        <header className="space-y-1">
          <h1 className="text-hero">Cardio</h1>
          <p className="text-sm text-muted-foreground">
            Walks, runs and finishers. Each track steps up on its own when you log good sessions. A finisher attached to a lift is
            also on the Current workout page.
          </p>
        </header>

        <CardioThisWeek />

        {tracksQuery.isLoading && <div className="h-40 animate-pulse rounded-lg bg-muted" aria-hidden="true" />}

        {tracksQuery.isError && (
          <div role="alert" className="space-y-3 rounded-lg border border-destructive/40 p-4">
            <p className="text-foreground">Couldn't load your tracks.</p>
            <Button variant="outline" onClick={() => void tracksQuery.refetch()}>
              <RefreshCw aria-hidden="true" />
              Try again
            </Button>
          </div>
        )}

        {tracks && tracks.length === 0 && (
          <p className="rounded-lg border border-dashed border-border p-4 text-sm text-muted-foreground">
            No tracks yet. Start one below; you can change every number later.
          </p>
        )}

        {tracks && tracks.length > 0 && (
          <section aria-label="Your tracks" className="space-y-3">
            {tracks.map((track) => (
              <TrackCard
                key={track.id}
                track={track}
                busy={busy}
                onLog={setLogging}
                onInfo={setInfo}
                onDecide={(t, accept) => decide.mutate({ id: t.id, accept }, { onError: fail })}
                onUndo={(t) => undo.mutate(t.id, { onSuccess: () => toast.success('Undone'), onError: fail })}
                onPause={(t, paused) => setPaused.mutate({ track: t, paused }, { onError: fail })}
                onStartGate={(t) => startGated.mutate(t.id, { onSuccess: () => toast.success(` started`), onError: fail })}
                onAddGate={(t) => applyGate.mutate(t.id, { onSuccess: () => toast.success('Waiting for the running gate'), onError: fail })}
              />
            ))}
          </section>
        )}

        <section aria-labelledby="templates-heading" className="space-y-2">
          <h2 id="templates-heading" className="text-lg font-semibold text-foreground">
            Start a track
          </h2>
          {templatesQuery.data?.map((template) => (
            <div key={template.key} className="space-y-2 rounded-lg border border-border bg-card p-4">
              <p className="font-medium text-foreground">{template.name}</p>
              <p className="text-sm text-muted-foreground">{template.description}</p>
              <Button
                variant="outline"
                className="w-full"
                disabled={start.isPending}
                aria-label={`Start ${template.name}`}
                onClick={() =>
                  start.mutate(
                    { templateKey: template.key },
                    { onSuccess: (t) => toast.success(`${t.name} started: ${t.prescription.title}`), onError: fail }
                  )
                }
              >
                <Plus aria-hidden="true" />
                Start
              </Button>
            </div>
          ))}
        </section>

        <Link to="/progress" className="flex min-h-12 items-center text-sm font-medium text-primary underline-offset-4 hover:underline">
          See each track's chart on Progress
        </Link>
      </main>

      <TrackLogSheet key={logging?.id ?? 'none'} track={logging} onClose={() => setLogging(null)} onSubmit={submitLog} saving={log.isPending} />
      {info && <TrackInfoSheet key={info.id} track={info} onClose={() => setInfo(null)} onSave={save} saving={update.isPending} />}
    </div>
  );
}
