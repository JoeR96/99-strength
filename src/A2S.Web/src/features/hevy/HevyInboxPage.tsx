/**
 * The Hevy inbox: workouts pulled from Hevy that fitted no planned session (trained on a rest
 * day, did Upper on a Lower day). Each one is placed with Assign, or dismissed with Ignore.
 * Offline the list is read-only; a failed pull or load says so and offers a retry.
 */

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ChevronLeft, Inbox, RefreshCw, TriangleAlert, WifiOff } from 'lucide-react';
import toast from 'react-hot-toast';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { useHevyStatus } from '@/contexts/hevyStatusContext';
import {
  hevyErrorMessage,
  useAssignHevyWorkout,
  useHevyInbox,
  useIgnoreHevyWorkout,
  usePullHevy,
} from '@/hooks/useHevySync';
import { formatDayLabel } from '@/lib/localDate';
import { useOnlineStatus } from '@/offline/useOnlineStatus';
import type { AssignHevyWorkoutRequest, HevyInboxItemDto } from '@/types/hevySync';
import { HevyAssignSheet } from './HevyAssignSheet';
import { exerciseCounts, exerciseNames, workoutMinutes } from './inboxAssign';

export function HevyInboxPage() {
  const { isConfigured, isLoading: statusLoading } = useHevyStatus();
  const online = useOnlineStatus();
  const inbox = useHevyInbox(isConfigured);
  const pull = usePullHevy();
  const assign = useAssignHevyWorkout();
  const ignore = useIgnoreHevyWorkout();
  const [assigningId, setAssigningId] = useState<string | null>(null);

  const items = inbox.data ?? [];
  const assigning = items.find((i) => i.hevyWorkoutId === assigningId) ?? null;
  const readOnly = !online;

  const openAssign = (item: HevyInboxItemDto) => {
    assign.reset();
    setAssigningId(item.hevyWorkoutId);
  };

  const assignTo = (item: HevyInboxItemDto, request: AssignHevyWorkoutRequest, label: string | null) => {
    assign.mutate(
      { hevyWorkoutId: item.hevyWorkoutId, request },
      {
        onSuccess: () => {
          setAssigningId(null);
          toast.success(label ? `${item.title} assigned to ${label}` : `${item.title} recorded as an extra session`);
        },
      }
    );
  };

  const ignoreItem = (item: HevyInboxItemDto) => {
    ignore.mutate(
      { hevyWorkoutId: item.hevyWorkoutId },
      {
        onSuccess: () => toast.success(`Ignored ${item.title}`),
        onError: (error) => toast.error(hevyErrorMessage(error, "Couldn't ignore that workout. Try again.")),
      }
    );
  };

  const waitingOffline = inbox.isPending && inbox.fetchStatus === 'paused';

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page space-y-4 py-4">
        <header className="space-y-1">
          <Link to="/today" className="inline-flex min-h-11 min-w-11 items-center gap-1 text-sm font-medium text-muted-foreground hover:text-foreground">
            <ChevronLeft className="h-4 w-4" aria-hidden="true" />
            Today
          </Link>
          <div className="flex items-start justify-between gap-3">
            <div>
              <h1 className="text-2xl font-bold text-foreground">Hevy inbox</h1>
              <p className="text-sm text-muted-foreground">Workouts pulled from Hevy that didn't fit a planned session.</p>
            </div>
            {isConfigured && (
              <Button
                variant="outline"
                size="icon"
                className="h-11 w-11 shrink-0"
                onClick={() => pull.mutate({ manual: true, announce: 'always' })}
                disabled={pull.isPending || readOnly}
                aria-label="Pull from Hevy"
                data-testid="inbox-pull"
              >
                <RefreshCw className={pull.isPending ? 'animate-spin' : undefined} aria-hidden="true" />
              </Button>
            )}
          </div>
        </header>

        {isConfigured && readOnly && (
          <p
            className="flex items-center gap-2 rounded-lg border border-border bg-card px-3 py-3 text-sm text-muted-foreground"
            role="status"
            data-testid="inbox-offline"
          >
            <WifiOff className="h-4 w-4 shrink-0" aria-hidden="true" />
            You're offline. The inbox is read-only until you're back online.
          </p>
        )}

        {isConfigured && pull.isError && !readOnly && (
          <p
            className="flex items-center gap-2 rounded-lg border border-warning/50 bg-warning/10 px-3 py-3 text-sm text-foreground"
            role="status"
            data-testid="inbox-pull-failed"
          >
            <TriangleAlert className="h-4 w-4 shrink-0 text-warning" aria-hidden="true" />
            {hevyErrorMessage(pull.error, "Couldn't pull from Hevy.")} The list below is what was pulled before.
          </p>
        )}

        {!isConfigured && !statusLoading && (
          <section className="rounded-lg border border-dashed border-border p-4" data-testid="inbox-not-connected">
            <p className="text-sm text-foreground">Hevy isn't connected, so there is nothing to pull.</p>
            <Button asChild variant="outline" className="mt-3 h-11">
              <Link to="/settings">Add your Hevy key in Settings</Link>
            </Button>
          </section>
        )}

        {isConfigured && inbox.isLoading && (
          <div className="space-y-3" aria-hidden="true">
            <div className="h-36 animate-pulse rounded-lg bg-muted" />
            <div className="h-36 animate-pulse rounded-lg bg-muted" />
          </div>
        )}

        {isConfigured && waitingOffline && (
          <p className="rounded-lg border border-dashed border-border p-4 text-sm text-muted-foreground">
            The inbox hasn't been loaded on this device yet. It will appear when you're back online.
          </p>
        )}

        {isConfigured && inbox.isError && (
          <section className="rounded-lg border border-destructive/50 bg-card p-4" role="alert">
            <p className="text-sm text-foreground">Couldn't load the Hevy inbox.</p>
            <Button variant="outline" className="mt-3 h-11" onClick={() => void inbox.refetch()}>
              <RefreshCw aria-hidden="true" />
              Try again
            </Button>
          </section>
        )}

        {isConfigured && inbox.isSuccess && items.length === 0 && (
          <section className="rounded-lg border border-dashed border-border p-6 text-center" data-testid="inbox-empty">
            <Inbox className="mx-auto h-8 w-8 text-muted-foreground" aria-hidden="true" />
            <p className="mt-2 font-medium text-foreground">Nothing waiting</p>
            <p className="mt-1 text-sm text-muted-foreground">Every workout pulled from Hevy matched a planned session.</p>
          </section>
        )}

        {items.length > 0 && (
          <ul className="space-y-3" aria-label="Unmatched Hevy workouts">
            {items.map((item) => {
              const minutes = workoutMinutes(item);
              const ignoring = ignore.isPending && ignore.variables?.hevyWorkoutId === item.hevyWorkoutId;
              return (
                <li key={item.hevyWorkoutId}>
                  <article className="rounded-lg border border-border bg-card p-4" data-testid={`inbox-item-${item.hevyWorkoutId}`}>
                    <div className="flex items-start justify-between gap-3">
                      <h2 className="min-w-0 truncate text-xl font-semibold text-foreground">{item.title}</h2>
                      <span className="shrink-0 text-sm text-muted-foreground">{formatDayLabel(item.localDate)}</span>
                    </div>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {exerciseCounts(item)}
                      {minutes !== null && `, ${minutes} min`}
                    </p>
                    {item.exercises.length > 0 && <p className="mt-1 text-sm text-foreground">{exerciseNames(item)}</p>}
                    {item.reason && <p className="mt-1 text-xs text-muted-foreground">Not matched: {item.reason}</p>}

                    <div className="mt-4 grid grid-cols-2 gap-2">
                      <Button
                        className="h-12 w-full px-3"
                        onClick={() => openAssign(item)}
                        disabled={readOnly || ignoring}
                        aria-label={`Assign ${item.title}`}
                        data-testid="inbox-assign"
                      >
                        Assign
                      </Button>
                      <Button
                        variant="outline"
                        className="h-12 w-full px-3"
                        onClick={() => ignoreItem(item)}
                        disabled={readOnly || ignoring}
                        aria-label={ignoring ? `Ignoring ${item.title}` : `Ignore ${item.title}`}
                        data-testid="inbox-ignore"
                      >
                        {ignoring ? 'Ignoring…' : 'Ignore'}
                      </Button>
                    </div>
                  </article>
                </li>
              );
            })}
          </ul>
        )}
      </main>

      <HevyAssignSheet
        item={readOnly ? null : assigning}
        onClose={() => setAssigningId(null)}
        onAssign={assignTo}
        saving={assign.isPending}
        error={assign.isError ? hevyErrorMessage(assign.error, "Couldn't assign that workout. Try again.") : null}
      />
    </div>
  );
}
