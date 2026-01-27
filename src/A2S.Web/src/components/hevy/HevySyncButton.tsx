/**
 * Hevy Sync Button Component
 * Button to sync workout to Hevy
 */

import { useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { useHevy } from '@/contexts/HevyContext';
import { useSyncWorkoutToHevy } from '@/hooks/useHevySync';
import type { WorkoutDto } from '@/types/workout';

interface HevySyncButtonProps {
  workout: WorkoutDto;
  variant?: 'default' | 'outline' | 'ghost';
  size?: 'default' | 'sm' | 'lg';
  className?: string;
}

/**
 * Check if the current week is fully synced to Hevy
 */
function isWeekFullySynced(workout: WorkoutDto): boolean {
  const syncedRoutines = workout.hevySyncedRoutines || {};
  const currentWeek = workout.currentWeek;
  const daysPerWeek = workout.daysPerWeek || 4;

  for (let day = 1; day <= daysPerWeek; day++) {
    const key = `week${currentWeek}-day${day}`;
    if (!syncedRoutines[key]) {
      return false;
    }
  }

  return true;
}

export function HevySyncButton({
  workout,
  variant = 'outline',
  size = 'default',
  className,
}: HevySyncButtonProps) {
  const { isConfigured, isValid } = useHevy();
  const syncMutation = useSyncWorkoutToHevy();

  const isAlreadySynced = useMemo(() => isWeekFullySynced(workout), [workout]);

  const handleSync = () => {
    syncMutation.mutate(workout);
  };

  if (!isConfigured) {
    return (
      <Button variant={variant} size={size} className={className} disabled>
        <svg className="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
        </svg>
        Connect Hevy
      </Button>
    );
  }

  if (!isValid) {
    return (
      <Button variant={variant} size={size} className={className} disabled>
        <svg className="h-4 w-4 mr-2 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
        Hevy Error
      </Button>
    );
  }

  // If already synced for this week, show synced state
  if (isAlreadySynced) {
    return (
      <Button variant={variant} size={size} className={className} disabled>
        <svg className="h-4 w-4 mr-2 text-green-500" fill="currentColor" viewBox="0 0 20 20">
          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
        </svg>
        Week {workout.currentWeek} Synced
      </Button>
    );
  }

  return (
    <Button
      variant={variant}
      size={size}
      className={className}
      onClick={handleSync}
      disabled={syncMutation.isPending}
    >
      {syncMutation.isPending ? (
        <>
          <svg className="h-4 w-4 mr-2 animate-spin" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
          </svg>
          Syncing...
        </>
      ) : (
        <>
          <svg className="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Sync to Hevy
        </>
      )}
    </Button>
  );
}
