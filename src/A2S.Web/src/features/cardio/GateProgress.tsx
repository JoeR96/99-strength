/**
 * A gated track's start gate: each condition with its progress, and a "Start" suggestion once every
 * condition is met. Starting is always the person's call; nothing starts by itself.
 */

import { CheckCircle2, Circle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { GateProgressDto } from '@/types/cardioTracks';

export interface GateProgressProps {
  trackName: string;
  gate: GateProgressDto;
  onStart?: () => void;
  busy?: boolean;
}

export function GateProgress({ trackName, gate, onStart, busy = false }: GateProgressProps) {
  const total = gate.conditions.length;
  return (
    <div className="space-y-2" data-testid="gate-progress">
      <p className="text-sm font-medium text-foreground">
        Not started — gate · {gate.metCount} of {total} met
      </p>
      <ul className="space-y-2">
        {gate.conditions.map((c) => (
          <li key={c.label} className="flex items-start gap-2 text-sm">
            {c.met ? (
              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-success" aria-hidden="true" />
            ) : (
              <Circle className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" />
            )}
            <span className="min-w-0">
              <span className="text-foreground">{c.label}</span>
              <span className="sr-only">{c.met ? ' (met)' : ' (not met yet)'}</span>
              <span className="block text-muted-foreground">{c.progress}</span>
            </span>
          </li>
        ))}
      </ul>
      {gate.allMet && onStart && (
        <div className="space-y-2 rounded-md border border-primary/50 p-3" role="status">
          <p className="text-sm text-foreground">Every condition is met. Start {trackName}?</p>
          <Button className="h-11 w-full" onClick={onStart} disabled={busy}>
            Start {trackName}
          </Button>
        </div>
      )}
    </div>
  );
}
