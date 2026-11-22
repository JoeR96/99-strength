/**
 * Hevy Sync Service
 * What is left of the browser-driven Hevy code: the Plan tab's "Pull from Hevy", through
 * `/hevy/{resource}` on our API, which attaches the stored key. Finished sessions are posted to
 * Hevy by the server (gym mode, `POST /gym-sessions`); the browser never writes workouts.
 *
 * Routines are not built here any more. The server pushes them (`api/hevySync.ts`,
 * `hooks/useHevySync.ts`) and stores their Hevy ids.
 */

import {
  type CompletedSetData,
  type CompletedExerciseData,
  type SyncResult,
  clearHevyTemplateCache,
} from './hevySyncHelpers';

import {
  type PulledSetData,
  type PulledWorkoutData,
  type DetectedSubstitution,
  type WeightDiscrepancy,
  type MissingExercise,
  type PullWorkoutResult,
  pullWorkoutFromHevy,
} from './hevySyncPull';

// Re-exported so callers have one module to import from
export type {
  CompletedSetData,
  CompletedExerciseData,
  SyncResult,
  PulledSetData,
  PulledWorkoutData,
  DetectedSubstitution,
  WeightDiscrepancy,
  MissingExercise,
  PullWorkoutResult,
};

export {
  clearHevyTemplateCache,
  pullWorkoutFromHevy,
};
