/**
 * Asks for a cardio exercise's starting values before its session, when the block says they are not set:
 * the walk's speed (km/h) and incline (5 % prefilled), or the stair climber's hard level on reaching its
 * intervals (the suggestion prefilled). Saves through `setCardioSettings`; the week plan is refetched and
 * gym mode picks up the new block.
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useSetCardioSettings } from '@/hooks/useWorkouts';
import type { CardioSettingsRequest, PlannedCardioBlockDto } from '@/api/workouts';

const SHEET = 'bottom-0 top-auto max-h-[92dvh] translate-y-0 overflow-y-auto rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2';
const STARTING_INCLINE = 5;

export interface CardioSettingsDialogProps {
  open: boolean;
  workoutId: string;
  exerciseId: string;
  name: string;
  block: PlannedCardioBlockDto;
  onClose: () => void;
}

function numberOf(text: string): number | null {
  const n = Number(text.trim().replace(',', '.'));
  return text.trim() !== '' && Number.isFinite(n) ? n : null;
}

function serverMessage(error: unknown): string {
  const data = (error as { response?: { data?: { detail?: string; title?: string } } })?.response?.data;
  return data?.detail ?? data?.title ?? 'Could not save. Try again.';
}

export function CardioSettingsDialog({ open, workoutId, exerciseId, name, block, onClose }: CardioSettingsDialogProps) {
  const walk = block.intensityUnit === 'InclinePercent';
  const [intensity, setIntensity] = useState(() => String(walk ? (block.intensity ?? STARTING_INCLINE) : (block.suggestedIntensity ?? block.intensity ?? '')));
  const [speed, setSpeed] = useState(() => (block.speed != null ? String(block.speed) : ''));
  const [error, setError] = useState<string | null>(null);
  const save = useSetCardioSettings();

  const submit = () => {
    const i = numberOf(intensity);
    const s = numberOf(speed);
    const problem =
      i === null || i <= 0 || i > 100
        ? walk
          ? 'Incline above 0 and up to 100 %.'
          : 'Level above 0 and up to 100.'
        : walk && (s === null || s < 0.5 || s > 25)
          ? 'Speed between 0.5 and 25 km/h.'
          : null;
    setError(problem);
    if (problem) return;
    const request: CardioSettingsRequest = walk ? { intensity: i!, speed: s! } : { intensity: i! };
    save.mutate({ workoutId, exerciseId, request }, { onSuccess: onClose, onError: (e) => setError(serverMessage(e)) });
  };

  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent className={SHEET}>
        <form
          className="space-y-4"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            submit();
          }}
        >
          <DialogHeader>
            <DialogTitle>{walk ? 'Set up your walk' : 'Pick your hard level'}</DialogTitle>
            <DialogDescription>
              {walk
                ? `${name}: pick a speed for a brisk walk where you can still talk in full sentences. The incline starts at ${STARTING_INCLINE} %.`
                : `${name}, ${block.stageName.toLowerCase()}: pick a level you could hold for 2 hard minutes, 4 times.`}
            </DialogDescription>
          </DialogHeader>

          {walk && (
            <div>
              <Label htmlFor="cardio-speed">Speed (km/h)</Label>
              <Input id="cardio-speed" type="number" inputMode="decimal" min={0.5} max={25} step={0.1} className="mt-1 h-12 px-3" value={speed} onChange={(e) => setSpeed(e.target.value)} />
            </div>
          )}
          <div>
            <Label htmlFor="cardio-intensity">{walk ? 'Incline (%)' : 'Hard level'}</Label>
            <Input
              id="cardio-intensity"
              type="number"
              inputMode="decimal"
              min={0}
              max={100}
              step={walk ? 0.5 : 1}
              className="mt-1 h-12 px-3"
              value={intensity}
              onChange={(e) => setIntensity(e.target.value)}
            />
            {!walk && block.suggestedIntensity != null && <p className="mt-1 text-sm text-muted-foreground">Suggested: level {block.suggestedIntensity}</p>}
          </div>

          {error && (
            <p className="text-sm text-destructive" role="alert">
              {error}
            </p>
          )}

          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" className="h-12" onClick={onClose}>
              Not now
            </Button>
            <Button type="submit" className="h-12" disabled={save.isPending} data-testid="cardio-settings-save">
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
