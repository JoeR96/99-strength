/** The evidence line for a Hevy-based suggestion with a "Use suggestion" button (44 px). */

import { Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { StartingNumberSuggestion } from '@/api/workouts';
import { suggestionLine } from './startingNumbers';

export interface SuggestionEvidenceProps {
  suggestion: StartingNumberSuggestion;
  onUse: () => void;
  /** Shown on the button's accessible name, e.g. the exercise, when several sit on one screen. */
  label?: string;
}

export function SuggestionEvidence({ suggestion, onUse, label }: SuggestionEvidenceProps) {
  return (
    <div className="flex flex-wrap items-center gap-2 rounded-md border border-border bg-muted/40 p-3" data-testid="suggestion-evidence">
      <p className="min-w-0 flex-1 text-sm text-foreground">
        <Sparkles className="mr-1 inline h-4 w-4 text-primary" aria-hidden="true" />
        {suggestionLine(suggestion)}
      </p>
      <Button
        type="button"
        variant="outline"
        className="h-11"
        onClick={onUse}
        aria-label={label ? `Use suggestion for ${label}` : 'Use suggestion'}
        data-testid="use-suggestion"
      >
        Use suggestion
      </Button>
    </div>
  );
}
