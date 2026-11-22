/**
 * Settings → Gym sessions: for each lift slot in the active plan's schedule, pick the warm-up routine
 * (default "Mobility A") and an optional finisher (a cardio protocol). Saving replaces the schedule
 * with the same slots and the new choices.
 */

import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { useActivePlan } from '@/hooks/usePlan';
import { useMobilityRoutines } from '@/hooks/useMobilityRoutines';
import { useCardioProtocols, useUpdateSchedule } from '@/hooks/useGymSession';
import { SessionKind, type FixedFinisherDto, type ScheduledSessionDto } from '@/types/plan';
import { useCardioTracks } from '@/hooks/useCardioTracks';

/** The fixed cool-down choice: logged as cardio after the lifts, no track or protocol. */
const TREADMILL_VALUE = 'fixed:treadmill-10';
const TREADMILL_COOL_DOWN: FixedFinisherDto = { kind: 'Treadmill', minutes: 10, notes: 'Easy incline' };

const DAY = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const SELECT = 'mt-1 h-12 w-full rounded-md border border-border bg-muted/50 px-3 text-base text-foreground focus:outline-none focus:ring-2 focus:ring-primary';

export function GymSessionSettings() {
  const { data: plan } = useActivePlan();
  const routines = useMobilityRoutines();
  const protocols = useCardioProtocols(Boolean(plan));
  const tracks = useCardioTracks();
  const save = useUpdateSchedule();
  // Unsaved choices per schedule index, laid over the plan's schedule.
  const [edits, setEdits] = useState<Record<number, Partial<ScheduledSessionDto>>>({});
  const schedule = useMemo(() => (plan?.schedule ?? []).map((s, i) => ({ ...s, ...edits[i] })), [plan, edits]);

  const lifts = useMemo(() => schedule.map((s, i) => ({ s, i })).filter(({ s }) => s.kind === SessionKind.Lift), [schedule]);
  if (!plan || lifts.length === 0) return null;

  const change = (index: number, patch: Partial<ScheduledSessionDto>) =>
    setEdits((current) => ({ ...current, [index]: { ...current[index], ...patch } }));

  const dirty = JSON.stringify(schedule) !== JSON.stringify(plan.schedule);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Gym sessions</CardTitle>
        <CardDescription>The warm-up and finisher gym mode puts around each lifting day.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {lifts.map(({ s, i }) => (
          <fieldset key={`${s.day}-${s.label}-${i}`} className="rounded-md border border-border p-3">
            <legend className="px-1 font-medium text-foreground">
              {DAY[s.day]}: {s.label}
            </legend>
            <div className="grid gap-3 min-[480px]:grid-cols-2">
              <div>
                <Label htmlFor={`warm-up-${i}`}>Warm-up</Label>
                <select id={`warm-up-${i}`} className={SELECT} value={s.warmUpRoutineId ?? ''} onChange={(e) => change(i, { warmUpRoutineId: e.target.value || null })}>
                  <option value="">Mobility A (default)</option>
                  {(routines.data ?? []).map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <Label htmlFor={`finisher-${i}`}>Finisher</Label>
                <select
                  id={`finisher-${i}`}
                  className={SELECT}
                  value={
                    s.finisherFixed
                      ? TREADMILL_VALUE
                      : s.finisherCardioTrackId
                        ? `track:${s.finisherCardioTrackId}`
                        : (s.finisherCardioProtocolId ?? '')
                  }
                  onChange={(e) => {
                    const value = e.target.value;
                    change(
                      i,
                      value === TREADMILL_VALUE
                        ? { finisherFixed: TREADMILL_COOL_DOWN, finisherCardioTrackId: null, finisherCardioProtocolId: null }
                        : value.startsWith('track:')
                          ? { finisherCardioTrackId: value.slice(6), finisherCardioProtocolId: null, finisherFixed: null }
                          : { finisherCardioProtocolId: value || null, finisherCardioTrackId: null, finisherFixed: null }
                    );
                  }}
                >
                  <option value="">None</option>
                  <option value={TREADMILL_VALUE}>Treadmill, easy incline, 10 min</option>
                  {(tracks.data ?? []).map((t) => (
                    <option key={t.id} value={`track:${t.id}`}>
                      {t.name} (track)
                    </option>
                  ))}
                  {(protocols.data ?? []).map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </fieldset>
        ))}
        <Button
          className="h-12 w-full"
          disabled={!dirty || save.isPending}
          onClick={() =>
            save.mutate(
              { planId: plan.id, schedule },
              {
                onSuccess: () => {
                  setEdits({});
                  toast.success('Gym sessions saved');
                },
                onError: () => toast.error('Could not save the schedule') }
            )
          }
        >
          {save.isPending ? 'Saving…' : 'Save gym sessions'}
        </Button>
      </CardContent>
    </Card>
  );
}
