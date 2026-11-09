/**
 * Hevy Sync Management
 * Folder management, routine lifecycle, cleanup, and utility functions
 */

import { hevyApi } from './hevyApi';
import { syncDayAsRoutine } from './hevySyncService';
import type { SyncResult } from './hevySyncHelpers';
import type { HevyRoutine } from '@/types/hevy';
import type { WorkoutDto } from '@/types/workout';

/**
 * Check if Hevy sync is available (API key configured and valid)
 */
export async function checkHevySyncAvailable(): Promise<boolean> {
  if (!hevyApi.isConfigured()) {
    return false;
  }
  return hevyApi.validateApiKey();
}

/**
 * Get or create a routine folder for a workout program
 */
export async function getOrCreateRoutineFolder(
  programName: string
): Promise<{ folderId: string; created: boolean } | null> {
  if (!hevyApi.isConfigured()) {
    return null;
  }

  try {
    let allFolders: { id: number; title: string }[] = [];
    let page = 1;
    let hasMore = true;

    while (hasMore) {
      const response = await hevyApi.getRoutineFolders(page, 10);
      allFolders = allFolders.concat(response.routine_folders.map(f => ({ id: f.id, title: f.title })));
      hasMore = page < response.page_count;
      page++;
    }

    const existingFolder = allFolders.find(
      (f) => f.title.toLowerCase() === programName.toLowerCase()
    );

    if (existingFolder) {
      return { folderId: String(existingFolder.id), created: false };
    }

    const newFolder = await hevyApi.createRoutineFolder({ routine_folder: { title: programName } });
    return { folderId: String(newFolder.id), created: true };
  } catch {
    return null;
  }
}

/**
 * Delete a routine from Hevy
 */
export async function deleteRoutineFromHevy(routineId: string): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured.',
    };
  }

  try {
    await hevyApi.deleteRoutine(routineId);
    return {
      success: true,
      message: `Routine ${routineId} deleted from Hevy`,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    return {
      success: false,
      message: `Failed to delete routine: ${message}`,
    };
  }
}

/**
 * Resync a day — delete existing routine and create a new one
 */
export async function resyncDayToHevy(
  workout: WorkoutDto,
  dayNumber: number
): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured. Please set your API key in settings.',
    };
  }

  const errors: string[] = [];

  try {
    const syncKey = `week${workout.currentWeek}-day${dayNumber}`;
    const existingRoutineId = workout.hevySyncedRoutines?.[syncKey];

    if (existingRoutineId) {
      try {
        await hevyApi.deleteRoutine(existingRoutineId);
      } catch (deleteError) {
        const errMsg = deleteError instanceof Error ? deleteError.message : 'Unknown error';
        if (!errMsg.includes('404')) {
          errors.push(`Failed to delete existing routine: ${errMsg}`);
        }
      }
    }

    const result = await syncDayAsRoutine(workout, dayNumber, workout.hevyRoutineFolderId);

    if (!result.success) {
      return {
        success: false,
        message: result.message,
        errors: errors.length > 0 ? errors : undefined,
      };
    }

    return {
      success: true,
      message: existingRoutineId
        ? `Day ${dayNumber} routine updated in Hevy!`
        : `Day ${dayNumber} routine created in Hevy!`,
      routine: result.routine,
      errors: errors.length > 0 ? errors : undefined,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    return {
      success: false,
      message: `Failed to resync routine: ${message}`,
    };
  }
}

/**
 * Get all routines from Hevy, optionally filtered by program name prefix
 */
export async function getRoutinesFromHevy(
  programNameFilter?: string
): Promise<HevyRoutine[]> {
  if (!hevyApi.isConfigured()) {
    return [];
  }

  try {
    const routines = await hevyApi.getAllRoutines();
    if (programNameFilter) {
      return routines.filter((r) =>
        r.title.toLowerCase().startsWith(programNameFilter.toLowerCase())
      );
    }
    return routines;
  } catch {
    return [];
  }
}

/**
 * Cleanup duplicate routines in Hevy — keep only the most recent
 */
export async function cleanupDuplicateRoutines(routineTitle: string): Promise<void> {
  try {
    const allRoutines = await hevyApi.getAllRoutines();
    const duplicates = allRoutines.filter(
      (r) => r.title.toLowerCase() === routineTitle.toLowerCase()
    );

    if (duplicates.length <= 1) {
      return;
    }

    duplicates.sort((a, b) => new Date(b.created_at).getTime() - new Date(a.created_at).getTime());

    const toDelete = duplicates.slice(1);

    for (const routine of toDelete) {
      try {
        await hevyApi.deleteRoutine(routine.id);
      } catch {
        // Best-effort cleanup
      }
    }
  } catch {
    // Best-effort cleanup
  }
}

/**
 * Handle routine lifecycle when a day is completed and week progresses
 */
export async function handleRoutineLifecycle(
  workout: WorkoutDto,
  dayNumber: number,
  previousWeek: number,
  newWeek: number
): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured.',
    };
  }

  const errors: string[] = [];
  let deletedRoutineId: string | null = null;
  let createdRoutine: HevyRoutine | null = null;

  try {
    // Create new routine FIRST, then delete old one
    const workoutForNewWeek: WorkoutDto = {
      ...workout,
      currentWeek: newWeek,
    };

    const createResult = await syncDayAsRoutine(
      workoutForNewWeek,
      dayNumber,
      workout.hevyRoutineFolderId
    );

    if (createResult.success && createResult.routine) {
      createdRoutine = createResult.routine;

      // Delete old routine(s) after successful creation
      const oldRoutineTitle = `${workout.name} - Week ${previousWeek} Day ${dayNumber}`;

      const allRoutines = await hevyApi.getAllRoutines();
      const oldRoutines = allRoutines.filter(
        (r) => r.title.toLowerCase() === oldRoutineTitle.toLowerCase()
      );

      for (const oldRoutine of oldRoutines) {
        try {
          await hevyApi.deleteRoutine(oldRoutine.id);
          deletedRoutineId = oldRoutine.id;
        } catch (deleteError) {
          const msg = deleteError instanceof Error ? deleteError.message : 'Unknown error';
          errors.push(`Warning: Failed to delete old routine ${oldRoutine.id}: ${msg}`);
        }
      }
    } else {
      errors.push(createResult.message);
      return {
        success: false,
        message: `Failed to create new routine: ${createResult.message}`,
        errors,
      };
    }

    const messages: string[] = [];
    if (deletedRoutineId) {
      messages.push(`Deleted Week ${previousWeek} Day ${dayNumber} routine`);
    }
    if (createdRoutine) {
      messages.push(`Created Week ${newWeek} Day ${dayNumber} routine`);
    }

    return {
      success: true,
      message: messages.join('. ') || 'Routine lifecycle completed',
      routine: createdRoutine || undefined,
      errors: errors.length > 0 ? errors : undefined,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    return {
      success: false,
      message: `Routine lifecycle failed: ${message}`,
    };
  }
}
