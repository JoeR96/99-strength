/**
 * A cardio exercise's settings: the walk's speed (km/h) and incline (5 % prefilled on the first walk), or the stair
 * climber's level (the hard level on reaching its intervals, the suggestion prefilled). Asked before the first session
 * while they are blank, and offered later as "Change settings" with the current values; every change is a progression
 * history entry on the server. Saves through `setCardioSettings`; the week plan is refetched and gym mode picks up the
 * new block.
 *
 * With no signal, blank starting values are handed to `onChosen` instead (gym mode keeps them in its draft and sends
 * them with the finish, where the server sets them first); a change to values already set waits for a connection.
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useSetCardioSettings } from '@/hooks/useWorkouts';
import { useOnlineStatus } from '@/offline/useOnlineStatus';
import { isNetworkError } from '@/offline/errors';
import type { CardioSettingsRequest, PlannedCardioBlockDto } from '@/api/workouts';

const SHEET = 'bottom-0 top-auto max-h-[92dvh] translate-y-0 overflow-y-auto rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2';
const STARTING_INCLINE = 5;
const NO_SIGNAL = 'No signal: settings can be changed once you are back online.';

/** What the dialog reads from a block: a week plan's block, or a cardio exercise's progression. */
export type CardioSettingsBlock = Pick<PlannedCardioBlockDto, 'intensityUnit' | 'intensity' | 'suggestedIntensity' | 'speed' | 'isSettingsPending' | 'stageName' | 'kind'>;

export interface CardioSettingsValues {
  intensity: number;
  speed?: number | null;
}

export interface CardioSettingsDialogProps {
  open: boolean;
  workoutId: string;
  exerciseId: string;
  name: string;
  block: CardioSettingsBlock;
  onClose: () => void;
  /** Blank starting values chosen with no signal (or the save never reached the server): kept on the phone. */
  onChosen?: (values: CardioSettingsValues) => void;
}

function numberOf(text: string): number | null {
  const n = Number(text.trim().replace(',', '.'));
  return text.trim() !== '' && Number.isFinite(n) ? n : null;
}

function serverMessage(error: unknown): string {
  const data = (error as { response?: { data?: { detail?: string; title?: string } } })?.response?.data;
  return data?.detail ?? data?.title ?? 'Could not save. Try again.';
}

function initialIntensity(block: CardioSettingsBlock, walk: boolean): string {
  if (walk) return String(block.intensity ?? STARTING_INCLINE);
  const level = block.isSettingsPending ? (block.suggestedIntensity ?? block.intensity) : (block.intensity ?? block.suggestedIntensity);
  return level != null ? String(level) : '';
}

export function CardioSettingsDialog({ open, workoutId, exerciseId, name, block, onClose, onChosen }: CardioSettingsDialogProps) {
  const walk = block.intensityUnit === 'InclinePercent';
  const pending = block.isSettingsPending;
  const online = useOnlineStatus();
  const [intensity, setIntensity] = useState(() => initialIntensity(block, walk));
  const [speed, setSpeed] = useState(() => (block.speed != null ? String(block.speed) : ''));
  const [error, setError] = useState<string | null>(null);
  const save = useSetCardioSettings();
  const levelLabel = pending || block.kind === 'Intervals' ? 'Hard level' : 'Level';

  const keepOnPhone = (values: CardioSettingsValues) => {
    onChosen?.(values);
    onClose();
  };

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
    const values: CardioSettingsValues = walk ? { intensity: i!, speed: s! } : { intensity: i! };
    if (!online) {
      if (pending && onChosen) keepOnPhone(values);
      else setError(NO_SIGNAL);
      return;
    }
    const request: CardioSettingsRequest = walk ? { intensity: i!, speed: s! } : { intensity: i! };
    save.mutate(
      { workoutId, exerciseId, request },
      {
        onSuccess: () => (pending ? keepOnPhone(values) : onClose()),
        onError: (e) => {
          if (pending && onChosen && isNetworkError(e)) keepOnPhone(values);
          else setError(isNetworkError(e) ? NO_SIGNAL : serverMessage(e));
        },
      }
    );
  };

  const title = pending ? (walk ? 'Set up your walk' : 'Pick your hard level') : 'Change settings';
  const description = pending
    ? walk
      ? `${name}: pick a speed for a brisk walk where you can still talk in full sentences. The incline starts at ${STARTING_INCLINE} %.`
      : `${name}, ${block.stageName.toLowerCase()}: pick a level you could hold for 2 hard minutes, 4 times.`
    : `${name}: the change is kept in its progression history.`;

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
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>{description}</DialogDescription>
          </DialogHeader>

          {walk && (
            <div>
              <Label htmlFor="cardio-speed">Speed (km/h)</Label>
              <Input id="cardio-speed" type="number" inputMode="decimal" min={0.5} max={25} step={0.1} className="mt-1 h-12 px-3" value={speed} onChange={(e) => setSpeed(e.target.value)} />
            </div>
          )}
          <div>
            <Label htmlFor="cardio-intensity">{walk ? 'Incline (%)' : levelLabel}</Label>
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
            {!walk && pending && block.suggestedIntensity != null && <p className="mt-1 text-sm text-muted-foreground">Suggested: level {block.suggestedIntensity}</p>}
          </div>

          {error && (
            <p className="text-sm text-destructive" role="alert">
              {error}
            </p>
          )}

          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" className="h-12" onClick={onClose}>
              {pending ? 'Not now' : 'Cancel'}
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
