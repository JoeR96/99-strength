/** Shown when Finish answers 409: another phone already finished this day, so this draft cannot be sent. */

import { Button } from '@/components/ui/button';

export interface FinishedElsewhereNoticeProps {
  message: string;
  onDiscard: () => void;
  onKeep: () => void;
}

export function FinishedElsewhereNotice({ message, onDiscard, onKeep }: FinishedElsewhereNoticeProps) {
  return (
    <div className="mx-auto max-w-2xl px-4 pt-4">
      <div role="alert" className="space-y-3 rounded-lg border border-warning/50 bg-card p-4" data-testid="gym-finished-elsewhere">
        <p className="font-semibold text-foreground">{message}</p>
        <p className="text-sm text-muted-foreground">
          Nothing from this phone was saved. Discard it to go back to Today, or keep it open to copy anything you need.
        </p>
        <div className="grid grid-cols-2 gap-2">
          <Button className="h-12" onClick={onDiscard}>
            Discard this phone's session
          </Button>
          <Button variant="outline" className="h-12" onClick={onKeep}>
            Keep it open
          </Button>
        </div>
      </div>
    </div>
  );
}
