import apiClient from "./apiClient";
import type { TrackSegmentDto } from "@/types/cardioTracks";
import type {
  WorkoutDto,
  WorkoutSummaryDto,
  ExerciseLibrary,
  CreateWorkoutRequest,
  CompleteDayRequest,
  CompleteDayResult,
  DayNumber,
  UpdateExercisesRequest,
  UpdateExercisesResult,
  SubstituteExerciseRequest,
  SubstituteExerciseResult,
  WorkoutHistoryResponse,
} from "../types/workout";

export interface UndoCompletionResult {
  day: number;
  weekNumber: number;
  weekRolledBack: boolean;
  newCurrentWeek: number;
  newCurrentDay: number;
  message?: string;
}

/**
 * API client for workout operations
 */
export const workoutsApi = {
  /**
   * Get the current active workout
   */
  getCurrentWorkout: async (): Promise<WorkoutDto | null> => {
    try {
      const response = await apiClient.get<WorkoutDto>("/workouts/current");
      return response.data;
    } catch (error: any) {
      if (error.response?.status === 404) {
        return null; // No active workout
      }
      throw error;
    }
  },

  /**
   * Get all workouts for the current user
   */
  getAllWorkouts: async (): Promise<WorkoutSummaryDto[]> => {
    const response = await apiClient.get<WorkoutSummaryDto[]>("/workouts");
    return response.data;
  },

  /**
   * Create a new workout
   */
  createWorkout: async (
    request: CreateWorkoutRequest
  ): Promise<{ id: string }> => {
    const response = await apiClient.post<{ id: string }>(
      "/workouts",
      request
    );
    return response.data;
  },

  /**
   * Set a workout as active
   */
  setActiveWorkout: async (workoutId: string): Promise<void> => {
    await apiClient.post(`/workouts/${workoutId}/activate`);
  },

  /**
   * Delete a workout
   */
  deleteWorkout: async (workoutId: string): Promise<void> => {
    await apiClient.delete(`/workouts/${workoutId}`);
  },

  /**
   * Get the exercise library
   */
  getExerciseLibrary: async (): Promise<ExerciseLibrary> => {
    const response = await apiClient.get<ExerciseLibrary>(
      "/workouts/exercises/library"
    );
    return response.data;
  },

  /**
   * Complete a training day with exercise performances
   */
  completeDay: async (
    workoutId: string,
    day: DayNumber,
    request: CompleteDayRequest
  ): Promise<CompleteDayResult> => {
    const response = await apiClient.post<CompleteDayResult>(
      `/workouts/${workoutId}/days/${day}/complete`,
      request
    );
    return response.data;
  },

  /** Suggested TMs / starting weights for the workout's pending exercises, from the person's Hevy history. */
  getStartingNumberSuggestions: async (workoutId: string): Promise<StartingNumbersSuggestions> => {
    const response = await apiClient.get<StartingNumbersSuggestions>(`/workouts/${workoutId}/suggestions/starting-numbers`);
    return response.data;
  },

  /** Sets a pending (or any) A2S2 lift's training max, directly or estimated from a recent set. */
  setTrainingMax: async (workoutId: string, exerciseId: string, request: SetTrainingMaxRequest): Promise<SetTrainingMaxResult> => {
    const response = await apiClient.post<SetTrainingMaxResult>(`/workouts/${workoutId}/exercises/${exerciseId}/training-max`, request);
    return response.data;
  },

  /**
   * Update exercises in a workout (training max, weight, etc.)
   */
  updateExercises: async (
    workoutId: string,
    request: UpdateExercisesRequest
  ): Promise<UpdateExercisesResult> => {
    const response = await apiClient.put<UpdateExercisesResult>(
      `/workouts/${workoutId}/exercises`,
      request
    );
    return response.data;
  },

  /**
   * Substitute an exercise permanently in the workout
   */
  substituteExercise: async (
    workoutId: string,
    request: SubstituteExerciseRequest
  ): Promise<SubstituteExerciseResult> => {
    const response = await apiClient.put<SubstituteExerciseResult>(
      `/workouts/${workoutId}/exercises/${request.exerciseId}/substitute`,
      request
    );
    return response.data;
  },

  /**
   * Remove an exercise from the workout permanently.
   */
  removeExercise: async (
    workoutId: string,
    exerciseId: string
  ): Promise<void> => {
    await apiClient.delete(`/workouts/${workoutId}/exercises/${exerciseId}`);
  },

  /**
   * Update the working weight for an accessory exercise (RepsPerSet or MinimalSets).
   * Linear progression exercises must use skip progression instead.
   */
  updateWorkingWeight: async (
    workoutId: string,
    exerciseId: string,
    newWeight: number,
    unit: 1 | 2, // 1 = Kilograms, 2 = Pounds
    reason?: string
  ): Promise<void> => {
    await apiClient.put(
      `/workouts/${workoutId}/exercises/${exerciseId}/working-weight`,
      {
        newWeight,
        unit,
        reason,
      }
    );
  },

  confirmStartingWeight: async (
    workoutId: string,
    exerciseId: string,
    weight: number,
    unit: 1 | 2
  ): Promise<void> => {
    await apiClient.post(
      `/workouts/${workoutId}/exercises/${exerciseId}/confirm-weight`,
      { weight, unit }
    );
  },

  /**
   * The planned sets for one week/day, computed server-side (block, deload, progression).
   */
  /** The active workout's cycle: the current session, then the rest of this week and next week. */
  getCurrentCycle: async (): Promise<CycleDto> => {
    const response = await apiClient.get<CycleDto>('/workouts/current/cycle');
    return response.data;
  },

  getWeekPlan: async (workoutId: string, week: number, day: number): Promise<WeekPlanDto> => {
    const response = await apiClient.get<WeekPlanDto>(`/workouts/${workoutId}/weeks/${week}/days/${day}/plan`);
    return response.data;
  },

  /**
   * Undo the last completed day, rolling back progress
   */
  undoLastCompletion: async (workoutId: string): Promise<UndoCompletionResult> => {
    const response = await apiClient.post<UndoCompletionResult>(
      `/workouts/${workoutId}/undo-last-completion`
    );
    return response.data;
  },

  /**
   * Update the block sequence for a workout
   */
  updateBlockSequence: async (
    workoutId: string,
    blockSequence: number[]
  ): Promise<{ workoutId: string; blockSequence: number[]; totalWeeks: number; currentWeek: number; currentBlock: number }> => {
    const response = await apiClient.put(
      `/workouts/${workoutId}/block-sequence`,
      { blockSequence }
    );
    return response.data;
  },

  /**
   * Get workout history including exercise progression data
   */
  getWorkoutHistory: async (workoutId?: string): Promise<WorkoutHistoryResponse | null> => {
    try {
      const params = workoutId ? `?id=${workoutId}` : '';
      const response = await apiClient.get<WorkoutHistoryResponse>(`/workouts/history${params}`);
      return response.data;
    } catch (error: any) {
      if (error.response?.status === 404) {
        return null;
      }
      throw error;
    }
  },

};

/**
 * Week plan response from the backend.
 * Contains all information needed to display and execute a workout day.
 */
export interface WeekPlanDto {
  workoutId: string;
  workoutName: string;
  weekNumber: number;
  dayNumber: number;
  blockNumber: number;
  isDeloadWeek: boolean;
  intensityPercentage: number;
  exercises: PlannedExerciseDto[];
  /** The session's cardio finisher as a planned item after the exercises (null/absent: none). */
  cardio?: PlannedCardioDto | null;
  /** The plan slot label ("Upper A"), when a plan is linked. */
  sessionLabel?: string | null;
  warmUpRoutineId?: string | null;
  /** The home routine the plan pairs with this session ("Hips & lower back"). */
  pairedRoutineId?: string | null;
  /** The workout's current week and day: the next session to do. */
  isCurrent?: boolean;
}

/** A lift session's cardio finisher as a planned item (prescription, progression, sessions to step up). */
export interface PlannedCardioDto {
  kind: 'Track' | 'Protocol' | 'Fixed';
  name: string;
  prescription: string;
  /** "Cardio track: double progression", "Cardio track: week program" or "Fixed". */
  progressionType: string;
  orderInDay: number;
  minutes: number;
  intensityUnit?: string | null;
  intensity?: number | null;
  modality?: string | null;
  stageName?: string | null;
  sessionsToStepUp?: number | null;
  /** "2 more sessions to step up". */
  stepUpNote?: string | null;
  note?: string | null;
  cardioTrackId?: string | null;
  cardioProtocolId?: string | null;
  /** A track's fixed easy warm-up / cool-down around the work block; `minutes` then includes them. */
  warmUp?: TrackSegmentDto | null;
  coolDown?: TrackSegmentDto | null;
  /** Minutes of the work block alone. */
  workMinutes?: number | null;
  /** The whole session in one line. */
  text?: string | null;
}

/** One session of the training cycle (`GET /workouts/current/cycle`). */
export interface CycleSessionDto {
  week: number;
  day: number;
  /** 1-based within the week: "Session 2 of 4". */
  position: number;
  isCurrent: boolean;
  isDeloadWeek: boolean;
  label: string;
  plan: WeekPlanDto;
}

export interface CycleDto {
  workoutId: string;
  workoutName: string;
  currentWeek: number;
  currentDay: number;
  totalWeeks: number;
  daysPerWeek: number;
  isCompleted: boolean;
  /** The current session first, then the rest of this week and all of next week. */
  sessions: CycleSessionDto[];
}

export interface PlannedExerciseDto {
  exerciseId: string;
  name: string;
  category: string;
  equipment: string;
  orderInDay: number;
  hevyExerciseTemplateId: string;
  progressionType: string;
  /** Rest between sets for gym mode's timer: main lifts 180 s, everything else 90 s. */
  restSeconds?: number;
  /** Same number on the same day = a superset; label is the number + a/b. */
  supersetGroup?: number | null;
  /** One bonus set on that side (unilateral reps-per-set only). */
  extraSetSide?: 'None' | 'Left' | 'Right';
  plannedSets: PlannedSetDto[];
  metadata: PlannedExerciseMetadataDto;
}

export interface PlannedSetDto {
  setNumber: number;
  weightKg: number;
  weightLbs: number;
  originalUnit: string;
  targetReps: number;
  isAmrap: boolean;
}

export interface PlannedExerciseMetadataDto {
  trainingMaxValue?: number;
  trainingMaxUnit?: string;
  repRangeMinimum?: number;
  repRangeTarget?: number;
  repRangeMaximum?: number;
  isUnilateral?: boolean;
  targetTotalReps?: number;
  notes?: string;
  isWeightPending?: boolean;
  /** A2S2 lift without a training max yet: its planned sets have weight 0. */
  isTrainingMaxPending?: boolean;
  /** Linear only: e.g. 0.70 for 70 %. */
  intensityPercentage?: number | null;
  tier?: 'Primary' | 'Auxiliary' | null;
}

/** Body for `POST /workouts/{id}/exercises/{exerciseId}/training-max`: a TM, or a recent set to estimate it from. */
export type SetTrainingMaxRequest =
  | { trainingMax: number; unit?: 'Kilograms' | 'Pounds' }
  | { recentWeight: number; recentReps: number; unit?: 'Kilograms' | 'Pounds' };

export interface SetTrainingMaxResult {
  exerciseId: string;
  trainingMax: number;
  unit: string;
  estimated: boolean;
}

/** A suggested starting number with the set it came from (`GET /workouts/{id}/suggestions/starting-numbers`). */
export interface StartingNumberSuggestion {
  valueKg: number;
  estimatedOneRepMaxKg: number;
  setDate: string;
  setWeightKg: number;
  setReps: number;
  windowStart: string;
  windowEnd: string;
  stale: boolean;
  stalePenaltyPercent: number;
  reason: string;
}

export interface StartingNumberItem {
  exerciseId: string;
  exerciseName: string;
  kind: 'TrainingMax' | 'StartingWeight';
  matchedBy: 'templateId' | 'title' | 'catalogueName' | null;
  suggestion: StartingNumberSuggestion | null;
}

/** `hevyStatus` other than `ok` means Hevy could not be read; the UI falls back to manual input. */
export interface StartingNumbersSuggestions {
  hevyStatus: 'ok' | 'noKey' | 'unavailable';
  hevyMessage: string | null;
  items: StartingNumberItem[];
}
