/**
 * Hevy Sync Service
 * Handles syncing A2S workouts to Hevy
 *
 * Uses stored Hevy exercise template IDs directly instead of fuzzy matching.
 */

import { hevyApi } from './hevyApi';
import { workoutsApi } from '@/api/workouts';
import type {
  HevyCreateWorkoutRequest,
  HevyCreateRoutineRequest,
  HevyRoutineSet,
  HevyRoutineExercise,
  HevyWorkoutExercise,
  HevyWorkoutSet,
  HevyRoutine,
  HevyWorkout,
} from '@/types/hevy';
import type {
  WorkoutDto,
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
} from '@/types/workout';

// Completed set data from workout session
export interface CompletedSetData {
  setNumber: number;
  weight: number;
  reps: number;
  isAmrap: boolean;
}

// Completed exercise data from workout session
export interface CompletedExerciseData {
  exercise: ExerciseDto;
  sets: CompletedSetData[];
  weightUnit: string; // 'kg' or 'lbs'
}

/**
 * Convert weight to kg if needed and round to 2 decimal places
 */
function convertToKg(weight: number, unit: string): number {
  let weightKg = weight;
  if (unit.toLowerCase() === 'lbs' || unit.toLowerCase() === 'pounds') {
    weightKg = weight * 0.453592;
  }
  // Round to 2 decimal places
  return Math.round(weightKg * 100) / 100;
}

/**
 * Round weight to 2 decimal places
 */
function roundWeight(weight: number): number {
  return Math.round(weight * 100) / 100;
}

/**
 * Round weight to nearest gym increment (2.5kg or 5lbs)
 */
function roundToGymIncrement(weight: number, unit: 'kg' | 'lbs' = 'kg'): number {
  const increment = unit === 'kg' ? 2.5 : 5;
  return Math.round(weight / increment) * increment;
}

/**
 * A2S 2.0 Hypertrophy Program Week Parameters
 * 21-week program with 3 blocks of 7 weeks each
 */
interface WeekParameters {
  intensity: number;  // Percentage of Training Max (e.g., 0.75 = 75%)
  sets: number;       // Number of working sets
  targetReps: number; // Target reps per set
  isDeload: boolean;  // Whether this is a deload week
}

/**
 * Get the A2S 2.0 parameters for a specific week
 */
function getWeekParameters(weekNumber: number): WeekParameters {
  // A2S 2.0 Hypertrophy program structure
  const weekParams: Record<number, WeekParameters> = {
    // Block 1 - Volume Phase (Weeks 1-7)
    1:  { intensity: 0.75, sets: 5, targetReps: 10, isDeload: false },
    2:  { intensity: 0.85, sets: 4, targetReps: 8,  isDeload: false },
    3:  { intensity: 0.90, sets: 3, targetReps: 6,  isDeload: false },
    4:  { intensity: 0.80, sets: 5, targetReps: 9,  isDeload: false },
    5:  { intensity: 0.85, sets: 4, targetReps: 7,  isDeload: false },
    6:  { intensity: 0.90, sets: 3, targetReps: 5,  isDeload: false },
    7:  { intensity: 0.65, sets: 5, targetReps: 10, isDeload: true },

    // Block 2 - Intensity Phase (Weeks 8-14)
    8:  { intensity: 0.85, sets: 4, targetReps: 8, isDeload: false },
    9:  { intensity: 0.90, sets: 3, targetReps: 6, isDeload: false },
    10: { intensity: 0.95, sets: 2, targetReps: 4, isDeload: false },
    11: { intensity: 0.85, sets: 4, targetReps: 7, isDeload: false },
    12: { intensity: 0.90, sets: 3, targetReps: 5, isDeload: false },
    13: { intensity: 0.95, sets: 2, targetReps: 3, isDeload: false },
    14: { intensity: 0.65, sets: 5, targetReps: 10, isDeload: true },

    // Block 3 - Peak Phase (Weeks 15-21)
    15: { intensity: 0.90, sets: 3, targetReps: 6, isDeload: false },
    16: { intensity: 0.95, sets: 2, targetReps: 4, isDeload: false },
    17: { intensity: 1.00, sets: 1, targetReps: 2, isDeload: false },
    18: { intensity: 0.95, sets: 2, targetReps: 4, isDeload: false },
    19: { intensity: 1.00, sets: 1, targetReps: 2, isDeload: false },
    20: { intensity: 1.05, sets: 1, targetReps: 2, isDeload: false },
    21: { intensity: 0.65, sets: 5, targetReps: 10, isDeload: true },
  };

  // Default to week 1 if out of range
  return weekParams[weekNumber] || weekParams[1];
}

/**
 * Convert completed exercise data to Hevy workout exercise format
 * Uses the stored hevyExerciseTemplateId directly for accurate mapping.
 */
function convertCompletedExerciseToHevy(
  exerciseData: CompletedExerciseData,
  amrapTarget?: number
): HevyWorkoutExercise {
  // Use the stored Hevy exercise template ID directly
  const templateId = exerciseData.exercise.hevyExerciseTemplateId;

  const sets: HevyWorkoutSet[] = exerciseData.sets
    .filter(set => set.reps > 0) // Only include sets with reps
    .map((set) => ({
      type: set.isAmrap ? 'failure' : 'normal',
      weight_kg: convertToKg(set.weight, exerciseData.weightUnit),
      reps: set.reps,
      distance_meters: null,
      duration_seconds: null,
      custom_metric: null,
      rpe: null,
    }));

  // Add AMRAP target note if this is an AMRAP exercise
  const notes = amrapTarget ? `AMRAP target: ${amrapTarget} reps` : null;

  return {
    exercise_template_id: templateId,
    superset_id: null,
    notes,
    sets,
  };
}

/**
 * Convert A2S exercise to Hevy routine exercise format (for routines/templates)
 * Uses the stored hevyExerciseTemplateId directly for accurate mapping.
 *
 * @param exercise - The exercise DTO with progression data
 * @param weekNumber - The current week number (1-21) for calculating working weight/sets/reps
 */
function convertExerciseToHevyRoutine(exercise: ExerciseDto, weekNumber: number): HevyRoutineExercise {
  // Use the stored Hevy exercise template ID directly
  const templateId = exercise.hevyExerciseTemplateId;
  const sets: HevyRoutineSet[] = [];
  let notes: string | null = null;

  if (exercise.progression.type === 'Linear') {
    const prog = exercise.progression as LinearProgressionDto;
    const weekParams = getWeekParameters(weekNumber);

    // Calculate working weight: Training Max × Intensity, rounded to gym increment
    const trainingMaxKg = prog.trainingMax.unit === 1
      ? prog.trainingMax.value
      : prog.trainingMax.value * 0.453592; // Convert lbs to kg
    const workingWeight = roundToGymIncrement(trainingMaxKg * weekParams.intensity, 'kg');

    // Create sets based on week parameters
    for (let i = 0; i < weekParams.sets; i++) {
      const isLastSet = i === weekParams.sets - 1;
      sets.push({
        type: isLastSet && prog.useAmrap ? 'failure' : 'normal',
        weight_kg: roundWeight(workingWeight),
        reps: weekParams.targetReps, // Use specific rep target, not null
        // Note: rep_range is omitted entirely - Hevy API rejects null values
      });
    }

    // Build notes with AMRAP target information
    const noteParts: string[] = [];
    if (weekParams.isDeload) {
      noteParts.push('DELOAD WEEK');
    }
    noteParts.push(`TM: ${prog.trainingMax.value}${prog.trainingMax.unit === 1 ? 'kg' : 'lbs'}`);
    noteParts.push(`Intensity: ${Math.round(weekParams.intensity * 100)}%`);
    if (prog.useAmrap) {
      noteParts.push(`AMRAP target: ${weekParams.targetReps}+ reps on last set`);
    }
    notes = noteParts.join(' | ');

  } else if (exercise.progression.type === 'RepsPerSet') {
    const prog = exercise.progression as RepsPerSetProgressionDto;

    // Convert weight to kg if needed
    const weightKg = prog.weightUnit?.toLowerCase() === 'pounds'
      ? prog.currentWeight * 0.453592
      : prog.currentWeight;

    for (let i = 0; i < prog.currentSetCount; i++) {
      sets.push({
        type: 'normal',
        weight_kg: roundWeight(weightKg),
        reps: prog.repRange.target, // Use target reps
        // Note: rep_range is omitted entirely - Hevy API rejects null values
      });
    }

    // Note with rep range info
    notes = `Rep range: ${prog.repRange.minimum}-${prog.repRange.maximum} | Target: ${prog.repRange.target} reps`;

  } else if (exercise.progression.type === 'MinimalSets') {
    const prog = exercise.progression as MinimalSetsProgressionDto;

    // Convert weight to kg if needed
    const weightKg = prog.weightUnit?.toLowerCase() === 'pounds'
      ? prog.currentWeight * 0.453592
      : prog.currentWeight;

    const repsPerSet = Math.ceil(prog.targetTotalReps / prog.currentSetCount);

    for (let i = 0; i < prog.currentSetCount; i++) {
      sets.push({
        type: 'normal',
        weight_kg: roundWeight(weightKg),
        reps: repsPerSet,
        // Note: rep_range is omitted entirely - Hevy API rejects null values
      });
    }

    notes = `Target: ${prog.targetTotalReps} total reps across ${prog.minimumSets}-${prog.maximumSets} sets`;
  }

  return {
    exercise_template_id: templateId,
    superset_id: null,
    rest_seconds: 120,
    notes,
    sets,
  };
}

export interface SyncResult {
  success: boolean;
  message: string;
  workout?: HevyWorkout;
  routine?: HevyRoutine;
  routines?: HevyRoutine[];
  errors?: string[];
}

/**
 * Create a completed workout in Hevy
 * This is used after finishing a workout session
 */
export async function createCompletedWorkoutInHevy(
  workout: WorkoutDto,
  dayNumber: number,
  completedExercises: CompletedExerciseData[],
  startTime: Date,
  endTime: Date
): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured. Please set your API key in settings.',
    };
  }

  const dayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const dayName = dayNames[dayNumber - 1] || `Day ${dayNumber}`;

  // Format: "Program Name - Week X / Day Y (DayName)"
  const workoutTitle = `${workout.name} - Week ${workout.currentWeek} / Day ${dayNumber} (${dayName})`;

  try {
    // Get week parameters for correct AMRAP target
    const weekParams = getWeekParameters(workout.currentWeek);

    const hevyExercises: HevyWorkoutExercise[] = [];

    for (const exerciseData of completedExercises) {
      // Only include exercises that have completed sets
      const completedSets = exerciseData.sets.filter(s => s.reps > 0);
      if (completedSets.length > 0) {
        // Determine AMRAP target for Linear exercises based on current week
        let amrapTarget: number | undefined;
        if (exerciseData.exercise.progression.type === 'Linear') {
          const prog = exerciseData.exercise.progression as LinearProgressionDto;
          if (prog.useAmrap) {
            amrapTarget = weekParams.targetReps; // Use actual week's target reps
          }
        }
        const hevyExercise = convertCompletedExerciseToHevy(exerciseData, amrapTarget);
        hevyExercises.push(hevyExercise);
      }
    }

    if (hevyExercises.length === 0) {
      return {
        success: false,
        message: 'No completed exercises to sync',
      };
    }

    const workoutRequest: HevyCreateWorkoutRequest = {
      workout: {
        title: workoutTitle,
        description: `Block ${workout.currentBlock} - Auto-synced from A2S Tracker`,
        start_time: startTime.toISOString(),
        end_time: endTime.toISOString(),
        is_private: false,
        exercises: hevyExercises,
      },
    };

    const createdWorkout = await hevyApi.createWorkout(workoutRequest);

    return {
      success: true,
      message: `Workout "${workoutTitle}" synced to Hevy!`,
      workout: createdWorkout,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    return {
      success: false,
      message: `Failed to sync workout: ${message}`,
    };
  }
}

/**
 * Sync a day's exercises as a routine template to Hevy
 * Uses the format: "Program Name - Week X Day Y"
 */
export async function syncDayAsRoutine(
  workout: WorkoutDto,
  dayNumber: number,
  folderId?: string | null
): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured. Please set your API key in settings.',
    };
  }

  // New format: "Program Name - Week X Day Y"
  const routineTitle = `${workout.name} - Week ${workout.currentWeek} Day ${dayNumber}`;

  try {
    const dayExercises = workout.exercises
      .filter((e) => e.assignedDay === dayNumber)
      .sort((a, b) => a.orderInDay - b.orderInDay);

    if (dayExercises.length === 0) {
      return {
        success: false,
        message: `No exercises found for Day ${dayNumber}`,
      };
    }

    // Get week parameters for the routine description
    const weekParams = getWeekParameters(workout.currentWeek);

    const hevyExercises: HevyRoutineExercise[] = [];
    for (const exercise of dayExercises) {
      // Pass the current week to calculate correct working weight, sets, and reps
      const hevyExercise = convertExerciseToHevyRoutine(exercise, workout.currentWeek);
      hevyExercises.push(hevyExercise);
    }

    // Convert string folder ID to number if provided
    const folderIdNum = folderId ? parseInt(folderId, 10) :
                        workout.hevyRoutineFolderId ? parseInt(workout.hevyRoutineFolderId, 10) :
                        null;

    // Build routine notes with week info
    const routineNotes = weekParams.isDeload
      ? `Block ${workout.currentBlock} | DELOAD WEEK | Intensity: ${Math.round(weekParams.intensity * 100)}%`
      : `Block ${workout.currentBlock} | Intensity: ${Math.round(weekParams.intensity * 100)}% | ${weekParams.sets} sets × ${weekParams.targetReps} reps`;

    const routineRequest: HevyCreateRoutineRequest = {
      routine: {
        title: routineTitle,
        folder_id: folderIdNum && !isNaN(folderIdNum) ? folderIdNum : null,
        notes: routineNotes,
        exercises: hevyExercises,
      },
    };

    const routine = await hevyApi.createRoutine(routineRequest);

    // Persist the synced routine ID to the database
    try {
      await workoutsApi.setHevySyncedRoutine(
        workout.id,
        workout.currentWeek,
        dayNumber,
        routine.id
      );
      console.log(`Persisted synced routine: week${workout.currentWeek}-day${dayNumber} = ${routine.id}`);
    } catch (persistError) {
      console.error('Failed to persist synced routine ID:', persistError);
      // Don't fail the sync - the routine was created in Hevy
    }

    return {
      success: true,
      message: `Routine "${routineTitle}" created in Hevy!`,
      routine,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    return {
      success: false,
      message: `Failed to create routine: ${message}`,
    };
  }
}

/**
 * Sync an entire A2S workout program to Hevy as routines
 * Creates a folder for the program (if not exists) and one routine per training day
 */
export async function syncWorkoutToHevy(workout: WorkoutDto): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured. Please set your API key in settings.',
    };
  }

  const daysPerWeek = workout.daysPerWeek || 4;
  const createdRoutines: HevyRoutine[] = [];
  const errors: string[] = [];

  // Step 1: Get or create routine folder for this program
  let folderId = workout.hevyRoutineFolderId;
  if (!folderId) {
    console.log(`Creating routine folder for "${workout.name}"...`);
    const folderResult = await getOrCreateRoutineFolder(workout.name);
    if (folderResult) {
      folderId = folderResult.folderId;
      console.log(`Folder ${folderResult.created ? 'created' : 'found'}: ${folderId}`);

      // Persist folder ID to workout in database
      try {
        await workoutsApi.setHevyFolderId(workout.id, folderId);
        console.log(`Folder ID saved to workout`);
      } catch (err) {
        console.error('Failed to save folder ID to workout:', err);
        // Continue anyway - folder was created in Hevy
      }
    } else {
      errors.push('Failed to create routine folder');
    }
  }

  // Step 2: Create routines for each day
  for (let day = 1; day <= daysPerWeek; day++) {
    const result = await syncDayAsRoutine(workout, day, folderId);
    if (result.success && result.routine) {
      createdRoutines.push(result.routine);
    } else if (!result.success) {
      errors.push(result.message);
    }
  }

  if (errors.length > 0 && createdRoutines.length === 0) {
    return {
      success: false,
      message: 'Failed to sync workout to Hevy',
      errors,
    };
  }

  const folderMsg = folderId ? ` in folder "${workout.name}"` : '';
  return {
    success: true,
    message: `Successfully synced ${createdRoutines.length} routines${folderMsg} to Hevy`,
    routines: createdRoutines,
    errors: errors.length > 0 ? errors : undefined,
  };
}

/**
 * Sync a single day's workout to Hevy
 * Creates folder if needed and stores the routine in it
 */
export async function syncDayToHevy(
  workout: WorkoutDto,
  dayNumber: number
): Promise<SyncResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured. Please set your API key in settings.',
    };
  }

  // Get or create folder if we don't have one
  let folderId = workout.hevyRoutineFolderId;
  if (!folderId) {
    console.log(`Creating routine folder for "${workout.name}"...`);
    const folderResult = await getOrCreateRoutineFolder(workout.name);
    if (folderResult) {
      folderId = folderResult.folderId;
      console.log(`Folder ${folderResult.created ? 'created' : 'found'}: ${folderId}`);

      // Persist folder ID to workout in database
      try {
        await workoutsApi.setHevyFolderId(workout.id, folderId);
        console.log(`Folder ID saved to workout`);
      } catch (err) {
        console.error('Failed to save folder ID to workout:', err);
        // Continue anyway - folder was created in Hevy
      }
    }
  }

  return syncDayAsRoutine(workout, dayNumber, folderId);
}

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
    console.log('getOrCreateRoutineFolder: Hevy API not configured');
    return null;
  }

  try {
    // First, try to find an existing folder with this program name
    // Fetch all pages of folders
    console.log(`getOrCreateRoutineFolder: Looking for folder "${programName}"`);
    let allFolders: { id: number; title: string }[] = [];
    let page = 1;
    let hasMore = true;

    while (hasMore) {
      // Note: Hevy API max pageSize for routine_folders is 10
      const response = await hevyApi.getRoutineFolders(page, 10);
      allFolders = allFolders.concat(response.routine_folders.map(f => ({ id: f.id, title: f.title })));
      hasMore = page < response.page_count;
      page++;
    }

    console.log(`getOrCreateRoutineFolder: Found ${allFolders.length} existing folders`);

    const existingFolder = allFolders.find(
      (f) => f.title.toLowerCase() === programName.toLowerCase()
    );

    if (existingFolder) {
      console.log(`getOrCreateRoutineFolder: Found existing folder with ID ${existingFolder.id}`);
      return { folderId: String(existingFolder.id), created: false };
    }

    // Create a new folder
    console.log(`getOrCreateRoutineFolder: Creating new folder "${programName}"`);
    const newFolder = await hevyApi.createRoutineFolder({ routine_folder: { title: programName } });
    console.log(`getOrCreateRoutineFolder: Created folder with ID ${newFolder.id}`);
    return { folderId: String(newFolder.id), created: true };
  } catch (error) {
    console.error('Failed to get or create routine folder:', error);
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
      message: 'Routine deleted from Hevy',
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
  } catch (error) {
    console.error('Failed to fetch routines:', error);
    return [];
  }
}

/**
 * Handle routine lifecycle when a day is completed and week progresses.
 * This will:
 * 1. Delete the old routine for this day (from the previous week)
 * 2. Create a new routine for this day (for the next week)
 *
 * @param workout - The workout with updated week number
 * @param dayNumber - The day that was just completed
 * @param previousWeek - The week number before progression
 * @param newWeek - The week number after progression
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
    // 1. Find and delete the old routine for this day
    const oldRoutineTitle = `${workout.name} - Week ${previousWeek} Day ${dayNumber}`;
    const allRoutines = await hevyApi.getAllRoutines();
    const oldRoutine = allRoutines.find(
      (r) => r.title.toLowerCase() === oldRoutineTitle.toLowerCase()
    );

    if (oldRoutine) {
      try {
        await hevyApi.deleteRoutine(oldRoutine.id);
        deletedRoutineId = oldRoutine.id;
        console.log(`Deleted old routine: ${oldRoutineTitle}`);
      } catch (deleteError) {
        const msg = deleteError instanceof Error ? deleteError.message : 'Unknown error';
        errors.push(`Failed to delete old routine: ${msg}`);
        console.error(`Failed to delete old routine:`, deleteError);
      }
    } else {
      console.log(`No old routine found with title: ${oldRoutineTitle}`);
    }

    // 2. Create the new routine for the next week
    // Create a modified workout object with the new week number for routine generation
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
      console.log(`Created new routine: ${createResult.routine.title}`);
    } else {
      errors.push(createResult.message);
    }

    // Build result message
    const messages: string[] = [];
    if (deletedRoutineId) {
      messages.push(`Deleted Week ${previousWeek} Day ${dayNumber} routine`);
    }
    if (createdRoutine) {
      messages.push(`Created Week ${newWeek} Day ${dayNumber} routine`);
    }

    if (errors.length > 0 && !createdRoutine) {
      return {
        success: false,
        message: errors.join('; '),
        errors,
      };
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
