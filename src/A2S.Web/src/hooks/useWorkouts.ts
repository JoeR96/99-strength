import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { workoutsApi } from "../api/workouts";
import type { CreateWorkoutRequest } from "../types/workout";

/**
 * Query key factory for workouts
 */
export const workoutKeys = {
  all: ["workouts"] as const,
  current: () => [...workoutKeys.all, "current"] as const,
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
    },
  });
}
