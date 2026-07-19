import { hevyApi } from "@/services/hevyApi";
import { syncDayAsRoutine, getOrCreateRoutineFolder } from "@/services/hevySyncService";
import { workoutsApi } from "@/api/workouts";
import type { WorkoutDto } from "@/types/workout";
import toast from "react-hot-toast";

/**
 * Orchestrates the Hevy delete/recreate/resync flow that runs after exercise
 * edits are saved. Behavior-preserving extraction from EditExercisesModal's
 * handleSave: same API calls, same ordering, same 1s wait, same toasts.
 */
export function useSyncExerciseEditsToHevy() {
  const syncExerciseEditsToHevy = async (
    workout: WorkoutDto,
    day: number,
    onSyncRequired?: () => void
  ) => {
    // If Hevy is configured, delete old routine and push updated one
    if (hevyApi.isConfigured()) {
      toast.loading("Syncing to Hevy...", { id: "edit-exercises" });

      const syncKey = `week${workout.currentWeek}-day${day}`;
      const existingRoutineId = workout.hevySyncedRoutines?.[syncKey];

      // Delete existing routine first
      if (existingRoutineId) {
        try {
          await hevyApi.deleteRoutine(existingRoutineId);
          // Wait for Hevy API to process the deletion
          await new Promise((r) => setTimeout(r, 1000));
        } catch (deleteError) {
          console.warn("Failed to delete old routine:", deleteError);
        }
      }

      let folderId = workout.hevyRoutineFolderId;
      if (!folderId) {
        const folderResult = await getOrCreateRoutineFolder(workout.name);
        if (folderResult) {
          folderId = folderResult.folderId;
          try {
            await workoutsApi.setHevyFolderId(workout.id, folderId);
          } catch (err) {
            console.error("Failed to save folder ID:", err);
          }
        }
      }

      // Fetch latest workout data directly (don't rely on parent refetch timing)
      try {
        const latestWorkout = await workoutsApi.getCurrentWorkout();
        if (latestWorkout) {
          const result = await syncDayAsRoutine(latestWorkout, day as any, folderId, true);
          if (result.success) {
            toast.success("Exercises updated and Hevy routine refreshed!", { id: "edit-exercises" });
          } else {
            toast.error(`Exercises updated but Hevy sync failed: ${result.message}`, { id: "edit-exercises" });
          }
        } else {
          toast.success("Exercises updated! Re-sync to Hevy manually.", { id: "edit-exercises" });
        }
      } catch (syncErr) {
        console.error("Hevy sync error:", syncErr);
        toast.success("Exercises updated! Re-sync to Hevy to apply changes.", { id: "edit-exercises" });
      }

      // Notify parent to refetch workout data
      onSyncRequired?.();
    } else {
      onSyncRequired?.();
      toast.success("Exercises updated!");
    }
  };

  return { syncExerciseEditsToHevy };
}
