import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";

interface SessionRecoveryModalProps {
  savedAt: string;
  completedSets: number;
  onResume: () => void;
  onStartFresh: () => void;
}

function getTimeAgo(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins} minute${diffMins === 1 ? '' : 's'} ago`;
  if (diffHours < 24) return `${diffHours} hour${diffHours === 1 ? '' : 's'} ago`;
  if (diffDays === 1) return 'yesterday';
  if (diffDays < 7) return `${diffDays} days ago`;
  return date.toLocaleDateString();
}

export function SessionRecoveryModal({ savedAt, completedSets, onResume, onStartFresh }: SessionRecoveryModalProps) {
  const savedDate = new Date(savedAt);
  const timeAgo = getTimeAgo(savedDate);

  return (
    <Dialog open={true} onOpenChange={(open) => { if (!open) onStartFresh(); }}>
      <DialogContent className="max-w-md p-0">
        <div className="p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-12 h-12 bg-blue-100 dark:bg-blue-900 rounded-full flex items-center justify-center">
              <svg className="w-6 h-6 text-blue-600 dark:text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <div>
              <DialogTitle className="text-lg font-bold">Incomplete Workout Found</DialogTitle>
              <p className="text-sm text-muted-foreground">Saved {timeAgo}</p>
            </div>
          </div>

          <div className="p-4 bg-muted rounded-lg mb-6">
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Completed sets</span>
              <span className="font-semibold">{completedSets}</span>
            </div>
          </div>

          <p className="text-sm text-muted-foreground mb-6">
            You have an incomplete workout. Would you like to resume where you left off or start fresh?
          </p>

          <div className="flex gap-3">
            <Button variant="outline" className="flex-1" onClick={onStartFresh}>Start Fresh</Button>
            <Button className="flex-1" onClick={onResume}>Resume</Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
