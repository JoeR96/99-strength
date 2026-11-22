/** "Restore unsaved changes?" — offered when a form opens and this phone kept a draft of it. */

import { Button } from '@/components/ui/button';
import { describeSavedAt } from './draftTime';

export interface DraftRestorePromptProps {
  /** ISO instant the draft was last saved. */
  savedAt: string;
  onRestore: () => void;
  onDiscard: () => void;
  /** Fixed "now" for stories and tests. */
  now?: Date;
}

export function DraftRestorePrompt({ savedAt, onRestore, onDiscard, now }: DraftRestorePromptProps) {
  return (
    <div role="region" aria-labelledby="draft-restore-title" className="space-y-3 rounded-lg border border-primary/50 bg-card p-4" data-testid="draft-restore-prompt">
      <h2 id="draft-restore-title" className="text-base font-semibold text-foreground">
        Restore unsaved changes?
      </h2>
      <p className="text-sm text-muted-foreground">You left this with changes that weren't saved ({describeSavedAt(savedAt, now)}).</p>
      <div className="grid grid-cols-2 gap-2">
        <Button type="button" className="h-11" onClick={onRestore}>
          Restore
        </Button>
        <Button type="button" variant="outline" className="h-11" onClick={onDiscard}>
          Discard
        </Button>
      </div>
    </div>
  );
}
