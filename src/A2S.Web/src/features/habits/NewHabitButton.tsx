/** The one way to add a habit: a full-width 48 px button. */

import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';

export function NewHabitButton({ onClick }: { onClick: () => void }) {
  return (
    <Button type="button" variant="outline" className="h-12 w-full" onClick={onClick} data-testid="new-habit">
      <Plus aria-hidden="true" />
      New habit
    </Button>
  );
}
