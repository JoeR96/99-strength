import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { workoutsApi } from "../api/workouts";
import type { CreateWorkoutRequest } from "../types/workout";

/**
 * Query key factory for workouts
 */
export const workoutKeys = {
  all: ["workouts"] as const,
  current: () => [...workoutKeys.all, "current"] as const,
  list: () => [...workoutKeys.all, "list"] as const,
  exerciseLibrary: () => ["exerciseLibrary"] as const,
};

/**
 * Hook to fetch the current active workout
 */
export function useCurrentWorkout() {
  return useQuery({
    queryKey: workoutKeys.current(),
    queryFn: () => workoutsApi.getCurrentWorkout(),
    staleTime: 1000 * 60 * 5, // 5 minutes
  });
}

/**
 * Hook to fetch all workouts for the current user
 */
export function useAllWorkouts() {
  return useQuery({
    queryKey: workoutKeys.list(),
    queryFn: () => workoutsApi.getAllWorkouts(),
    staleTime: 1000 * 60 * 5, // 5 minutes
  });
}

/**
 * Hook to fetch the exercise library
 */
export function useExerciseLibrary() {
  return useQuery({
    queryKey: workoutKeys.exerciseLibrary(),
    queryFn: () => workoutsApi.getExerciseLibrary(),
    staleTime: Infinity, // Exercise library rarely changes
  });
}

/**
 * Hook to create a new workout
 */
export function useCreateWorkout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateWorkoutRequest) =>
      workoutsApi.createWorkout(request),
    onSuccess: () => {
      // Invalidate current workout query to fetch the newly created workout
      queryClient.invalidateQueries({ queryKey: workoutKeys.current() });
      queryClient.invalidateQueries({ queryKey: workoutKeys.list() });
    },
  });
}

/**
 * Hook to set a workout as active
 */
export function useSetActiveWorkout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (workoutId: string) => workoutsApi.setActiveWorkout(workoutId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: workoutKeys.current() });
      queryClient.invalidateQueries({ queryKey: workoutKeys.list() });
    },
  });
}

/**
 * Hook to delete a workout
 */
export function useDeleteWorkout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (workoutId: string) => workoutsApi.deleteWorkout(workoutId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: workoutKeys.current() });
      queryClient.invalidateQueries({ queryKey: workoutKeys.list() });
    },
  });
}
