/**
 * Hevy Sync Hook
 * Provides mutations for syncing workouts to Hevy
 */

import { useMutation } from '@tanstack/react-query';
import { syncWorkoutToHevy, syncDayToHevy, type SyncResult } from '@/services/hevySyncService';
import type { WorkoutDto } from '@/types/workout';
import toast from 'react-hot-toast';

/**
 * Hook for syncing an entire workout program to Hevy
 */
export function useSyncWorkoutToHevy() {
  return useMutation({
    mutationFn: (workout: WorkoutDto) => syncWorkoutToHevy(workout),
    onSuccess: (result: SyncResult) => {
      if (result.success) {
        toast.success(result.message);
        if (result.errors && result.errors.length > 0) {
          // Show warnings for partial success
          result.errors.forEach((error) => {
            toast.error(error, { duration: 5000 });
          });
        }
      } else {
        toast.error(result.message);
        result.errors?.forEach((error) => {
          toast.error(error, { duration: 5000 });
        });
      }
    },
    onError: (error: Error) => {
      toast.error(`Sync failed: ${error.message}`);
    },
  });
}

/**
 * Hook for syncing a single day's workout to Hevy
 */
export function useSyncDayToHevy() {
  return useMutation({
    mutationFn: ({ workout, dayNumber }: { workout: WorkoutDto; dayNumber: number }) =>
      syncDayToHevy(workout, dayNumber),
    onSuccess: (result: SyncResult) => {
      if (result.success) {
        toast.success(result.message);
      } else {
        toast.error(result.message);
      }
    },
    onError: (error: Error) => {
      toast.error(`Sync failed: ${error.message}`);
    },
  });
}
