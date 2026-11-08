/** Logs a cardio session: duration is required, heart rate, RPE, distance and notes are optional. */

import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import type { CardioLogDto, TodaySessionDto } from '@/types/plan';

export interface CardioLogSheetProps {
  session: TodaySessionDto | null;
  onClose: () => void;
  onSubmit: (session: TodaySessionDto, log: CardioLogDto) => void;
  saving?: boolean;
}

function optionalNumber(raw: string): number | null {
  const n = Number(raw.trim().replace(',', '.'));
  return raw.trim() === '' || !Number.isFinite(n) ? null : n;
}

export function CardioLogSheet({ session, onClose, onSubmit, saving = false }: CardioLogSheetProps) {
  const [minutes, setMinutes] = useState('');
  const [avgHr, setAvgHr] = useState('');
  const [rpe, setRpe] = useState('');
  const [km, setKm] = useState('');
  const [zoneMinutes, setZoneMinutes] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!session) return;
    const mins = optionalNumber(minutes);
    if (mins === null || mins <= 0) {
      setError('How many minutes?');
      return;
    }
    const rpeValue = optionalNumber(rpe);
    if (rpeValue !== null && (rpeValue < 1 || rpeValue > 10)) {
      setError('RPE is 1 to 10');
      return;
    }
    const zone = optionalNumber(zoneMinutes);
    if (zone !== null && (zone < 0 || zone > mins)) {
      setError('Zone 4–5 minutes cannot be more than the session');
      return;
    }
    setError(null);
    const distance = optionalNumber(km);
    onSubmit(session, {
      durationSeconds: Math.round(mins * 60),
      avgHeartRate: optionalNumber(avgHr),
      rpe: rpeValue,
      distanceMeters: distance === null ? null : Math.round(distance * 1000),
      notes: notes.trim() === '' ? null : notes.trim(),
      highIntensityMinutes: zone === null ? null : Math.round(zone),
    });
  };

  return (
    <Dialog open={session !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="bottom-0 top-auto translate-y-0 rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2">
        <form onSubmit={submit} className="space-y-4">
          <DialogHeader>
            <DialogTitle>{session?.label ?? 'Cardio'}</DialogTitle>
            <DialogDescription>Log what you did; only the time is required.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-2 gap-3">
            <div className="col-span-2">
              <Label htmlFor="cardio-minutes">Minutes</Label>
              <Input
                id="cardio-minutes"
                inputMode="numeric"
                autoFocus
                value={minutes}
                onChange={(e) => setMinutes(e.target.value)}
                placeholder="38"
                className="mt-1 h-12 text-lg"
                aria-invalid={error ? true : undefined}
              />
            </div>
            <div>
              <Label htmlFor="cardio-hr">Avg HR</Label>
              <Input id="cardio-hr" inputMode="numeric" value={avgHr} onChange={(e) => setAvgHr(e.target.value)} placeholder="150" className="mt-1 h-12" />
            </div>
            <div>
              <Label htmlFor="cardio-rpe">RPE (1–10)</Label>
              <Input id="cardio-rpe" inputMode="numeric" value={rpe} onChange={(e) => setRpe(e.target.value)} placeholder="8" className="mt-1 h-12" />
            </div>
            <div>
              <Label htmlFor="cardio-km">Distance (km)</Label>
              <Input id="cardio-km" inputMode="decimal" value={km} onChange={(e) => setKm(e.target.value)} placeholder="5.0" className="mt-1 h-12" />
            </div>
            <div>
              <Label htmlFor="cardio-zone">Zone 4–5 minutes</Label>
              <Input
                id="cardio-zone"
                inputMode="numeric"
                value={zoneMinutes}
                onChange={(e) => setZoneMinutes(e.target.value)}
                placeholder="From your watch"
                className="mt-1 h-12"
              />
            </div>
            <div className="col-span-2">
              <Label htmlFor="cardio-notes">Notes</Label>
              <Input id="cardio-notes" value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Hills, windy" className="mt-1 h-12" />
            </div>
          </div>
          {error && (
            <p className="text-sm text-destructive" role="alert">
              {error}
            </p>
          )}

          <DialogFooter className="gap-2 sm:gap-2">
            <Button type="button" variant="outline" onClick={onClose} className="h-12">
              Cancel
            </Button>
            <Button type="submit" className="h-12" disabled={saving}>
              {saving ? 'Saving…' : 'Mark done'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
