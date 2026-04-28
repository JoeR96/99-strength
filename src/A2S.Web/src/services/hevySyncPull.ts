/**
 * Hevy Sync Pull
 * Functions for pulling workout data from Hevy back into the app
 */

import { hevyApi } from './hevyApi';
import { calculatePrescribedWeight, getPrescribedSetsAndReps } from '@/utils/workoutCalculations';
import type { HevyWorkout } from '@/types/hevy';
import type { WorkoutDto } from '@/types/workout';

export interface PulledSetData {
  setNumber: number;
  weight: number; // in kg
  reps: number;
  isAmrap: boolean;
}

export interface PulledWorkoutData {
  exerciseId: string;
  exerciseName: string;
  hevyTemplateId: string;
  sets: PulledSetData[];
}

export interface DetectedSubstitution {
  originalExerciseId: string;
  originalExerciseName: string;
  originalHevyTemplateId: string;
  hevyExerciseName: string;
  hevyTemplateId: string;
  sets: PulledSetData[];
}

export interface WeightDiscrepancy {
  exerciseId: string;
  exerciseName: string;
  prescribedWeight: number;
  actualWeights: number[];
  hasVaryingWeights: boolean;
  sets: PulledSetData[];
  progressionType: string;
}

export interface MissingExercise {
  exerciseId: string;
  exerciseName: string;
  prescribedSets: number;
  prescribedReps: number;
  prescribedWeight: number;
}

export interface PullWorkoutResult {
  success: boolean;
  message: string;
  exercises?: PulledWorkoutData[];
  substitutions?: DetectedSubstitution[];
  unmatchedHevyExercises?: string[];
  weightDiscrepancies?: WeightDiscrepancy[];
  missingExercises?: MissingExercise[];
  workoutTitle?: string;
  startTime?: string;
  endTime?: string;
}

export async function pullWorkoutFromHevy(
  workout: WorkoutDto,
  dayNumber: number
): Promise<PullWorkoutResult> {
  if (!hevyApi.isConfigured()) {
    return {
      success: false,
      message: 'Hevy API key not configured. Please set your API key in settings.',
    };
  }

  const dayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const dayName = dayNames[dayNumber - 1] || `Day ${dayNumber}`;

  const possibleTitles = [
    `${workout.name} - Week ${workout.currentWeek} / Day ${dayNumber} (${dayName})`,
    `${workout.name} - Week ${workout.currentWeek} Day ${dayNumber}`,
    `Week ${workout.currentWeek} Day ${dayNumber}`,
  ];

  try {
    let allWorkouts: HevyWorkout[] = [];
    let page = 1;
    const maxPages = 5;

    while (page <= maxPages) {
      const response = await hevyApi.getWorkouts(page, 10);
      allWorkouts = allWorkouts.concat(response.workouts);
      if (page >= response.page_count) break;
      page++;
    }

    let matchedWorkout: HevyWorkout | undefined;

    for (const title of possibleTitles) {
      matchedWorkout = allWorkouts.find(
        (w) => w.title.toLowerCase() === title.toLowerCase()
      );
      if (matchedWorkout) break;
    }

    if (!matchedWorkout) {
      const weekDayPattern = `week ${workout.currentWeek}`;
      const dayPattern = `day ${dayNumber}`;

      matchedWorkout = allWorkouts.find(
        (w) => {
          const lowerTitle = w.title.toLowerCase();
          return lowerTitle.includes(weekDayPattern) &&
                 (lowerTitle.includes(dayPattern) || lowerTitle.includes(dayName.toLowerCase()));
        }
      );
    }

    if (!matchedWorkout) {
      return {
        success: false,
        message: `No workout found for Week ${workout.currentWeek} Day ${dayNumber}. Make sure you've completed this workout in Hevy.`,
      };
    }

    const exerciseTemplates = await hevyApi.getAllExerciseTemplates();
    const templateMap = new Map(exerciseTemplates.map(t => [t.id, t]));

    const dayExercises = workout.exercises
      .filter((e) => e.assignedDay === dayNumber)
      .sort((a, b) => a.orderInDay - b.orderInDay);

    const pulledExercises: PulledWorkoutData[] = [];
    const substitutions: DetectedSubstitution[] = [];
    const unmatchedHevyExercises: string[] = [];
    const weightDiscrepancies: WeightDiscrepancy[] = [];

    const matchedOurExercises = new Set<string>();
    const matchedHevyExercises = new Set<number>();

    const hevyExerciseInfos = matchedWorkout.exercises.map((hevyExercise, index) => {
      const hevyTemplate = templateMap.get(hevyExercise.exercise_template_id);
      const hevyExerciseName = hevyTemplate?.title || `Unknown (${hevyExercise.exercise_template_id})`;
      const sets: PulledSetData[] = hevyExercise.sets.map((set, setIndex) => ({
        setNumber: setIndex + 1,
        weight: set.weight_kg || 0,
        reps: set.reps || 0,
        isAmrap: set.type === 'failure',
      }));
      return { hevyExercise, hevyExerciseName, sets, index };
    });

    const recordMatch = (exercise: typeof dayExercises[0], hevyInfo: typeof hevyExerciseInfos[0]) => {
      matchedOurExercises.add(exercise.id);
      matchedHevyExercises.add(hevyInfo.index);

      const prescribedWeight = calculatePrescribedWeight(exercise, workout.currentWeek);
      const actualWeights = hevyInfo.sets.map(s => s.weight);
      const hasVaryingWeights = new Set(actualWeights).size > 1;

      // First-time RepsPerSet/MinimalSets log: no prescribed weight exists yet,
      // so the Hevy weight is by definition the starting weight — no discrepancy.
      const isFirstTimeWeightCapture =
        (exercise.progression.type === 'RepsPerSet' || exercise.progression.type === 'MinimalSets') &&
        prescribedWeight === 0;

      const shouldReportDiscrepancy = !isFirstTimeWeightCapture && (hasVaryingWeights ||
        (actualWeights.length > 0 && Math.abs(actualWeights[0] - prescribedWeight) > 0.01));

      if (shouldReportDiscrepancy) {
        weightDiscrepancies.push({
          exerciseId: exercise.id,
          exerciseName: exercise.name,
          prescribedWeight,
          actualWeights,
          hasVaryingWeights,
          sets: hevyInfo.sets,
          progressionType: exercise.progression.type,
        });
      }

      pulledExercises.push({
        exerciseId: exercise.id,
        exerciseName: exercise.name,
        hevyTemplateId: hevyInfo.hevyExercise.exercise_template_id,
        sets: hevyInfo.sets,
      });
    };

    // Pass 1: Match by template ID
    for (const hevyInfo of hevyExerciseInfos) {
      const match = dayExercises.find(
        (e) => e.hevyExerciseTemplateId === hevyInfo.hevyExercise.exercise_template_id && !matchedOurExercises.has(e.id)
      );
      if (match) {
        recordMatch(match, hevyInfo);
      }
    }

    // Pass 2: Match by name
    for (const hevyInfo of hevyExerciseInfos) {
      if (matchedHevyExercises.has(hevyInfo.index)) continue;
      const nameMatch = dayExercises.find(
        (e) => !matchedOurExercises.has(e.id) && e.name.toLowerCase() === hevyInfo.hevyExerciseName.toLowerCase()
      );
      if (nameMatch) {
        recordMatch(nameMatch, hevyInfo);
      }
    }

    // Pass 3: Remaining = substitutions
    for (const hevyInfo of hevyExerciseInfos) {
      if (matchedHevyExercises.has(hevyInfo.index)) continue;

      const remainingUnmatched = dayExercises.filter(e => !matchedOurExercises.has(e.id));

      let matchingExercise = remainingUnmatched.find(e => e.orderInDay === hevyInfo.index + 1);

      if (!matchingExercise && remainingUnmatched.length > 0) {
        matchingExercise = remainingUnmatched[0];
      }

      if (matchingExercise) {
        matchedOurExercises.add(matchingExercise.id);
        matchedHevyExercises.add(hevyInfo.index);
        substitutions.push({
          originalExerciseId: matchingExercise.id,
          originalExerciseName: matchingExercise.name,
          originalHevyTemplateId: matchingExercise.hevyExerciseTemplateId,
          hevyExerciseName: hevyInfo.hevyExerciseName,
          hevyTemplateId: hevyInfo.hevyExercise.exercise_template_id,
          sets: hevyInfo.sets,
        });
      } else {
        unmatchedHevyExercises.push(hevyInfo.hevyExerciseName);
      }
    }

    const totalMatched = pulledExercises.length + substitutions.length;
    if (totalMatched === 0) {
      return {
        success: false,
        message: 'Could not match any exercises from Hevy workout to your program.',
        unmatchedHevyExercises,
      };
    }

    const missingExercises: MissingExercise[] = [];
    for (const exercise of dayExercises) {
      if (!matchedOurExercises.has(exercise.id)) {
        const prescribedWeight = calculatePrescribedWeight(exercise, workout.currentWeek);
        const { sets, reps } = getPrescribedSetsAndReps(exercise, workout.currentWeek);

        missingExercises.push({
          exerciseId: exercise.id,
          exerciseName: exercise.name,
          prescribedSets: sets,
          prescribedReps: reps,
          prescribedWeight,
        });
      }
    }

    let message = `Pulled workout data from "${matchedWorkout.title}"`;
    if (substitutions.length > 0) {
      message += ` (${substitutions.length} substitution${substitutions.length > 1 ? 's' : ''} detected)`;
    }
    if (weightDiscrepancies.length > 0) {
      message += ` (${weightDiscrepancies.length} weight change${weightDiscrepancies.length > 1 ? 's' : ''} detected)`;
    }
    if (missingExercises.length > 0) {
      message += ` (${missingExercises.length} exercise${missingExercises.length > 1 ? 's' : ''} missing)`;
    }

    return {
      success: true,
      message,
      exercises: pulledExercises,
      substitutions: substitutions.length > 0 ? substitutions : undefined,
      unmatchedHevyExercises: unmatchedHevyExercises.length > 0 ? unmatchedHevyExercises : undefined,
      weightDiscrepancies: weightDiscrepancies.length > 0 ? weightDiscrepancies : undefined,
      missingExercises: missingExercises.length > 0 ? missingExercises : undefined,
      workoutTitle: matchedWorkout.title,
      startTime: matchedWorkout.start_time,
      endTime: matchedWorkout.end_time,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    return {
      success: false,
      message: `Failed to pull workout from Hevy: ${message}`,
    };
  }
}
