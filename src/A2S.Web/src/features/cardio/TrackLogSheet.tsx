/**
 * Logs a session against a cardio track. One tap is enough: Easy, Solid or Brutal (RPE 4 / 7 / 9).
 * Minutes default to the prescription; everything else (level, floors, HR, zone minutes, a pain
 * flag) is optional and tucked under "More".
 */

import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import {
  CardioModality,
  EffortRating,
  IntensityUnit,
  PainFlag,
  intensityLabel,
  type CardioTrackDto,
  type LogTrackSessionRequest,
} from '@/types/cardioTracks';

export interface TrackLogSheetProps {
  track: CardioTrackDto | null;
  onClose: () => void;
  onSubmit: (track: CardioTrackDto, request: LogTrackSessionRequest) => void;
  saving?: boolean;
  /** Starts with the optional fields open (stories). */
  initiallyExpanded?: boolean;
}

const efforts = [
  { value: EffortRating.Easy, label: 'Easy', hint: 'Could talk in full sentences' },
  { value: EffortRating.Solid, label: 'Solid', hint: 'Working, a few words at a time' },
  { value: EffortRating.Brutal, label: 'Brutal', hint: 'All I had' },
] as const;

const pains = [
  { value: PainFlag.None, label: 'No pain' },
  { value: PainFlag.Niggle, label: 'Niggle' },
  { value: PainFlag.Stop, label: 'Had to stop' },
] as const;

export function optionalNumber(raw: string): number | null {
  const n = Number(raw.trim().replace(',', '.'));
  return raw.trim() === '' || !Number.isFinite(n) ? null : n;
}

function newClientKey(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
}

export function TrackLogSheet({ track, onClose, onSubmit, saving = false, initiallyExpanded = false }: TrackLogSheetProps) {
  const [effort, setEffort] = useState<EffortRating | null>(null);
  const [expanded, setExpanded] = useState(initiallyExpanded);
  const [minutes, setMinutes] = useState('');
  const [workMinutes, setWorkMinutes] = useState('');
  const [completed, setCompleted] = useState(true);
  const [intensity, setIntensity] = useState('');
  const [floors, setFloors] = useState('');
  const [avgHr, setAvgHr] = useState('');
  const [zoneMinutes, setZoneMinutes] = useState('');
  const [rpe, setRpe] = useState('');
  const [pain, setPain] = useState<PainFlag>(PainFlag.None);
  const [painArea, setPainArea] = useState('');
  const [error, setError] = useState<string | null>(null);
  // One key per opened sheet, so a retried submit (offline, double tap) is recorded once.
  const [clientKey] = useState(newClientKey);

  const prescribed = track?.prescription.minutes ?? 0;
  // With a warm-up/cool-down, minutes are the whole session and the work block is logged beside it.
  const segmented = !!(track?.prescription.warmUp || track?.prescription.coolDown);
  const prescribedWork = track?.prescription.workMinutes ?? 0;
  const unit = track?.prescription.intensityUnit ?? IntensityUnit.None;

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!track) return;
    if (effort === null) {
      setError('How did it feel? Pick Easy, Solid or Brutal.');
      return;
    }
    const mins = optionalNumber(minutes) ?? prescribed;
    if (mins <= 0) {
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
    const level = optionalNumber(intensity);
    onSubmit(track, {
      completedAsPrescribed: completed && pain !== PainFlag.Stop,
      clientKey,
      log: {
        durationSeconds: Math.round(mins * 60),
        avgHeartRate: optionalNumber(avgHr),
        rpe: rpeValue,
        distanceMeters: null,
        notes: null,
        highIntensityMinutes: zone === null ? null : Math.round(zone),
        effort,
        intensityUnit: level === null || unit === IntensityUnit.None ? null : unit,
        intensityValue: unit === IntensityUnit.None ? null : level,
        floors: optionalNumber(floors),
        workMinutes: segmented ? optionalNumber(workMinutes) ?? prescribedWork : null,
        pain,
        painArea: pain === PainFlag.None || painArea.trim() === '' ? null : painArea.trim(),
      },
    });
  };

  return (
    <Dialog open={track !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sheet-surface bottom-0 top-auto w-full translate-y-0 overflow-y-auto rounded-b-none rounded-t-2xl p-6 pb-[calc(1.5rem+env(safe-area-inset-bottom))] sm:bottom-auto sm:top-1/2 sm:w-[calc(100%-2rem)] sm:-translate-y-1/2 sm:rounded-2xl sm:pb-6 max-h-[92vh]">
        <form onSubmit={submit} className="space-y-4" aria-label={`Log ${track?.name ?? 'cardio'}`}>
          <DialogHeader>
            <DialogTitle>{track?.name ?? 'Cardio'}</DialogTitle>
            <DialogDescription>
              {track ? `Today: ${track.prescription.text ?? track.prescription.title}${segmented ? ` · ${prescribed} min total` : ''}` : ''}
            </DialogDescription>
          </DialogHeader>

          <fieldset>
            <legend className="mb-2 text-sm font-medium text-foreground">How did it feel?</legend>
            <div className="grid grid-cols-3 gap-2">
              {efforts.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  aria-pressed={effort === option.value}
                  onClick={() => setEffort(option.value)}
                  className={cn(
                    'flex min-h-20 flex-col items-center justify-center rounded-lg border px-2 py-3 text-center transition-colors',
                    effort === option.value ? 'border-primary bg-primary text-primary-foreground' : 'border-transparent bg-foreground/[0.06] text-foreground hover:bg-foreground/[0.1]'
                  )}
                >
                  <span className="text-lg font-semibold">{option.label}</span>
                  <span className={cn('text-xs', effort === option.value ? 'text-primary-foreground' : 'text-muted-foreground')}>{option.hint}</span>
                </button>
              ))}
            </div>
          </fieldset>

          <label className="flex min-h-11 items-center gap-3 text-sm text-foreground">
            <input type="checkbox" className="h-5 w-5 accent-primary" checked={completed} onChange={(e) => setCompleted(e.target.checked)} />
            Did it all, as prescribed
          </label>

          <Button type="button" variant="outline" className="w-full" aria-expanded={expanded} onClick={() => setExpanded((v) => !v)}>
            {expanded ? 'Fewer details' : 'More details (optional)'}
          </Button>

          {expanded && (
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="track-minutes">{segmented ? 'Total minutes' : 'Minutes'}</Label>
                <Input id="track-minutes" inputMode="decimal" value={minutes} onChange={(e) => setMinutes(e.target.value)} placeholder={String(prescribed)} className="mt-1 h-12" />
              </div>
              {segmented && (
                <div>
                  <Label htmlFor="track-work-minutes">Work minutes</Label>
                  <Input id="track-work-minutes" inputMode="decimal" value={workMinutes} onChange={(e) => setWorkMinutes(e.target.value)} placeholder={String(prescribedWork)} className="mt-1 h-12" />
                </div>
              )}
              {unit !== IntensityUnit.None && (
                <div>
                  <Label htmlFor="track-intensity">{intensityLabel[unit]}</Label>
                  <Input
                    id="track-intensity"
                    inputMode="decimal"
                    value={intensity}
                    onChange={(e) => setIntensity(e.target.value)}
                    placeholder={track?.prescription.intensity?.toString() ?? ''}
                    className="mt-1 h-12"
                  />
                </div>
              )}
              {track?.modality === CardioModality.StairClimber && (
                <div>
                  <Label htmlFor="track-floors">Floors</Label>
                  <Input id="track-floors" inputMode="numeric" value={floors} onChange={(e) => setFloors(e.target.value)} placeholder="35" className="mt-1 h-12" />
                </div>
              )}
              <div>
                <Label htmlFor="track-hr">Avg HR</Label>
                <Input id="track-hr" inputMode="numeric" value={avgHr} onChange={(e) => setAvgHr(e.target.value)} placeholder="From your watch" className="mt-1 h-12" />
              </div>
              <div>
                <Label htmlFor="track-zone">Zone 4–5 min</Label>
                <Input id="track-zone" inputMode="numeric" value={zoneMinutes} onChange={(e) => setZoneMinutes(e.target.value)} placeholder="WHOOP" className="mt-1 h-12" />
              </div>
              <div>
                <Label htmlFor="track-rpe">Exact RPE</Label>
                <Input id="track-rpe" inputMode="numeric" value={rpe} onChange={(e) => setRpe(e.target.value)} placeholder="1–10" className="mt-1 h-12" />
              </div>
              <fieldset className="col-span-2">
                <legend className="mb-2 text-sm font-medium text-foreground">Pain</legend>
                <div className="grid grid-cols-3 gap-2">
                  {pains.map((option) => (
                    <button
                      key={option.value}
                      type="button"
                      aria-pressed={pain === option.value}
                      onClick={() => setPain(option.value)}
                      className={cn(
                        'min-h-12 rounded-md border px-2 text-sm',
                        pain === option.value ? 'border-primary bg-primary text-primary-foreground' : 'border-transparent bg-foreground/[0.06] text-foreground'
                      )}
                    >
                      {option.label}
                    </button>
                  ))}
                </div>
              </fieldset>
              {pain !== PainFlag.None && (
                <div className="col-span-2">
                  <Label htmlFor="track-pain-area">Where?</Label>
                  <Input id="track-pain-area" value={painArea} onChange={(e) => setPainArea(e.target.value)} placeholder="Left knee" className="mt-1 h-12" />
                </div>
              )}
            </div>
          )}

          {error && (
            <p className="text-sm text-destructive" role="alert">
              {error}
            </p>
          )}

          <div className="grid grid-cols-2 gap-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={saving}>
              {saving ? 'Saving…' : 'Log session'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
