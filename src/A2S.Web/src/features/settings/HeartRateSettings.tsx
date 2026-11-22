/**
 * Settings → Heart rate: max HR and the five zone floors (% of max, WHOOP's 50/60/70/80/90 by default).
 * Cardio tracks use them to judge easy sessions (average HR under the zone 2 ceiling) and hard ones.
 */

import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cardioTracksApi } from '@/api/cardioTracks';
import type { HeartRateZonesDto } from '@/types/cardioTracks';

export const WHOOP_ZONES = [50, 60, 70, 80, 90];
const zonesKey = ['cardio-tracks', 'heart-rate-zones'] as const;

/** Five rising floors between 30 and 99, or a message saying what is wrong. */
export function validateZones(max: number | null, floors: number[]): string | null {
  if (max !== null && (max < 100 || max > 240)) return 'Max heart rate is 100 to 240';
  if (floors.some((f) => !Number.isFinite(f) || f < 30 || f > 99)) return 'Zone floors are 30 to 99 %';
  if (floors.some((f, i) => i > 0 && f <= floors[i - 1])) return 'Each zone must start above the one before';
  return null;
}

export function HeartRateSettings() {
  const query = useQuery({ queryKey: zonesKey, queryFn: () => cardioTracksApi.getZones() });
  return query.data ? <HeartRateForm key={JSON.stringify(query.data)} zones={query.data} /> : null;
}

export function HeartRateForm({ zones }: { zones: HeartRateZonesDto }) {
  const queryClient = useQueryClient();
  const [max, setMax] = useState(zones.maxHeartRate?.toString() ?? '');
  const [floors, setFloors] = useState(zones.lowerPercents.map(String));
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: ({ maxHr, lowers }: { maxHr: number | null; lowers: number[] | null }) => cardioTracksApi.setZones(maxHr, lowers),
    onSuccess: (saved) => {
      queryClient.setQueryData(zonesKey, saved);
      toast.success('Heart-rate zones saved');
    },
    onError: () => toast.error('Could not save the zones'),
  });

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const maxHr = max.trim() === '' ? null : Number(max);
    const lowers = floors.map(Number);
    const problem = validateZones(maxHr, lowers);
    setError(problem);
    if (problem) return;
    const isDefault = lowers.every((f, i) => f === WHOOP_ZONES[i]);
    save.mutate({ maxHr, lowers: isDefault ? null : lowers });
  };

  const maxNumber = Number(max);
  const bpm = (percent: number) => (max.trim() !== '' && Number.isFinite(maxNumber) ? Math.round((maxNumber * percent) / 100) : null);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Heart rate</CardTitle>
        <CardDescription>Cardio tracks use these to judge easy and hard sessions. Leave max HR empty to judge on effort only.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-4" aria-label="Heart-rate zones">
          <div>
            <Label htmlFor="max-hr">Max heart rate (bpm)</Label>
            <Input id="max-hr" inputMode="numeric" value={max} onChange={(e) => setMax(e.target.value)} placeholder="190" className="mt-1 h-12" />
          </div>
          <fieldset>
            <legend className="mb-2 text-sm font-medium text-foreground">Zone starts (% of max)</legend>
            <div className="grid grid-cols-5 gap-2">
              {floors.map((value, i) => {
                const upper = i === 4 ? 100 : Number(floors[i + 1]);
                const low = bpm(Number(value));
                const high = bpm(upper);
                return (
                  <div key={i}>
                    <Label htmlFor={`zone-${i + 1}`}>Z{i + 1}</Label>
                    <Input
                      id={`zone-${i + 1}`}
                      inputMode="numeric"
                      value={value}
                      onChange={(e) => setFloors((current) => current.map((f, j) => (j === i ? e.target.value : f)))}
                      className="mt-1 h-12 px-2 text-center"
                    />
                    <p className="mt-1 text-center text-xs text-muted-foreground">{low !== null && high !== null ? `${low}–${high}` : `–${upper}%`}</p>
                  </div>
                );
              })}
            </div>
          </fieldset>
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
          <div className="grid grid-cols-2 gap-2">
            <Button type="button" variant="outline" onClick={() => setFloors(WHOOP_ZONES.map(String))}>
              WHOOP zones
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
