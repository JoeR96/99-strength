/**
 * Hevy sync on Settings, shown once a key is stored: push this week's sessions to Hevy as
 * routines (with what happened to each day's session) and pull finished workouts now (with the
 * summary). Key management stays in `HevySettings`.
 */

import { Link } from 'react-router-dom';
import { CheckCircle2, Download, TriangleAlert, Upload, WifiOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useHevyStatus } from '@/contexts/hevyStatusContext';
import { hevyErrorMessage, matchedMessage, quietPullMessage, usePullHevy, usePushWeek, type PullHevyOutcome } from '@/hooks/useHevySync';
import { useActivePlan } from '@/hooks/usePlan';
import { compareLocalDates, formatDayLabel, type LocalDate } from '@/lib/localDate';
import { useOnlineStatus } from '@/offline/useOnlineStatus';
import type { PushWeekResult } from '@/types/hevySync';

export function HevySyncSettings() {
  const { isConfigured } = useHevyStatus();
  // Nothing to push or pull without a key; HevySettings above already asks for one.
  return isConfigured ? <HevySyncControls /> : null;
}

interface DayLine {
  date: LocalDate;
  label: string;
  /** "Created" / "Updated" for a pushed routine, or the reason it failed. */
  detail: string;
  failed: boolean;
}

/** Pushed and failed sessions as one list in day order. */
function dayLines(result: PushWeekResult): DayLine[] {
  const pushed = result.pushed.map<DayLine>((p) => ({
    date: p.date,
    label: p.label,
    detail: p.outcome === 'created' ? 'Created' : 'Updated',
    failed: false,
  }));
  const failed = result.failed.map<DayLine>((f) => ({ date: f.date, label: f.label, detail: f.error, failed: true }));
  // A stable sort keeps the server's order within a day.
  return [...pushed, ...failed].sort((a, b) => compareLocalDates(a.date, b.date));
}

function pullLine(outcome: PullHevyOutcome): string {
  const { summary } = outcome;
  if (summary.matched === 0) return quietPullMessage(summary);
  const inbox = summary.unmatched > 0 ? `, ${summary.unmatched} in the inbox` : '';
  return `${matchedMessage(outcome)}${inbox}`;
}

function HevySyncControls() {
  const online = useOnlineStatus();
  const planQuery = useActivePlan();
  const pushWeek = usePushWeek();
  const pull = usePullHevy();

  const plan = planQuery.data ?? null;
  const lines = pushWeek.data ? dayLines(pushWeek.data) : [];

  return (
    <Card data-testid="hevy-sync-settings">
      <CardHeader>
        <CardTitle>Hevy sync</CardTitle>
        <CardDescription>
          Send this week's sessions to Hevy as routines, and bring finished workouts back. Workouts are also pulled
          whenever you open Today.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        {!online && (
          <p className="flex items-center gap-2 text-sm text-muted-foreground" role="status" data-testid="hevy-sync-offline">
            <WifiOff className="h-4 w-4 shrink-0" aria-hidden="true" />
            You're offline. Pushing and pulling need a connection.
          </p>
        )}

        <section className="space-y-3" aria-label="Push this week">
          <Button
            className="h-11 w-full sm:w-auto"
            onClick={() => plan && pushWeek.mutate({ planId: plan.id })}
            disabled={!plan || !online || pushWeek.isPending}
            data-testid="hevy-push-week"
          >
            <Upload aria-hidden="true" />
            {pushWeek.isPending ? 'Pushing…' : 'Push this week to Hevy'}
          </Button>

          {planQuery.isSuccess && !plan && (
            <p className="text-sm text-muted-foreground">Start a plan on Today first; there are no sessions to push yet.</p>
          )}

          {pushWeek.isError && (
            <p className="text-sm text-destructive" role="alert">
              {hevyErrorMessage(pushWeek.error, "Couldn't push this week to Hevy. Try again.")}
            </p>
          )}

          {pushWeek.data && (
            <div data-testid="hevy-push-week-result">
              <p className="text-sm font-medium text-foreground">Week of {formatDayLabel(pushWeek.data.weekStart)}</p>
              {lines.length === 0 ? (
                <p className="mt-1 text-sm text-muted-foreground">Nothing to push: no sessions are planned this week.</p>
              ) : (
                <ul className="mt-2 divide-y divide-border rounded-lg border border-border">
                  {lines.map((line, index) => (
                    <li key={`${line.date}-${line.label}-${index}`} className="flex items-start gap-3 px-3 py-2 text-sm">
                      {line.failed ? (
                        <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-destructive" aria-hidden="true" />
                      ) : (
                        <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-success" aria-hidden="true" />
                      )}
                      <span className="w-20 shrink-0 text-muted-foreground">{formatDayLabel(line.date)}</span>
                      <span className="min-w-0 flex-1">
                        <span className="font-medium text-foreground">{line.label}</span>
                        <span className={line.failed ? 'block text-destructive' : 'block text-muted-foreground'}>
                          {line.failed ? `Not pushed: ${line.detail}` : line.detail}
                        </span>
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </section>

        <section className="space-y-3" aria-label="Pull from Hevy">
          <Button
            variant="outline"
            className="h-11 w-full sm:w-auto"
            onClick={() => pull.mutate({ manual: true, announce: 'never' })}
            disabled={!online || pull.isPending}
            data-testid="hevy-pull-now"
          >
            <Download aria-hidden="true" />
            {pull.isPending ? 'Pulling…' : 'Pull now'}
          </Button>

          {pull.isError && (
            <p className="text-sm text-destructive" role="alert">
              {hevyErrorMessage(pull.error, "Couldn't pull from Hevy. Try again.")}
            </p>
          )}

          {pull.data && (
            <div role="status" data-testid="hevy-pull-result">
              <p className="text-sm text-foreground">{pullLine(pull.data)}.</p>
              {!pull.data.summary.wasSkipped && (
                <p className="text-xs text-muted-foreground">
                  {pull.data.summary.pulled} pulled, {pull.data.summary.matched} matched, {pull.data.summary.unmatched} unmatched
                </p>
              )}
              {pull.data.summary.unmatched > 0 && (
                <Link to="/hevy/inbox" className="inline-flex min-h-11 items-center text-sm font-medium text-primary underline">
                  Open the inbox
                </Link>
              )}
            </div>
          )}
        </section>
      </CardContent>
    </Card>
  );
}
