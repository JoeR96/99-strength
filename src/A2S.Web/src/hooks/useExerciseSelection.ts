import { useState, useMemo, useCallback } from "react";
import { nanoid } from "nanoid";
import type {
  ExerciseTemplate,
  SelectedExercise,
  DayNumber,
  ProgramVariant,
} from "../types/workout";
import { ExerciseCategory as ExerciseCategoryEnum } from "../types/workout";

/**
 * Smart defaults for exercise configuration based on exercise type
 */
function getDefaultConfiguration(
  template: ExerciseTemplate,
  programVariant: ProgramVariant
): Omit<SelectedExercise, "id" | "template"> {
  const exerciseName = template.name.toLowerCase();

  // Main 4 lifts
  const isMainLift =
    exerciseName.includes("squat") ||
    exerciseName.includes("bench") ||
    exerciseName.includes("deadlift") ||
    exerciseName.includes("overhead press") ||
    exerciseName.includes("press") && !exerciseName.includes("leg");

  if (isMainLift) {
    // Auto-assign to spread across program (simplified rotation)
    const dayMapping: Record<string, DayNumber> = {
      squat: 1,
      bench: 2,
      deadlift: 3,
      press: programVariant >= 4 ? 4 : 2, // Day 4 if 4+ days, else Day 2
    };

    let assignedDay: DayNumber = 1;
    for (const [key, day] of Object.entries(dayMapping)) {
      if (exerciseName.includes(key)) {
        assignedDay = day as DayNumber;
        break;
      }
    }

    return {
      hevyExerciseTemplateId: '', // Will be set when exercise is selected from Hevy library
      category: ExerciseCategoryEnum.MainLift,
      progressionType: "Linear",
      assignedDay,
      orderInDay: 1, // Main lifts typically go first
    };
  }

  // Barbell compounds (auxiliary exercises)
  const isBarbell = template.equipment === 0; // EquipmentType.Barbell
  const isCompound =
    exerciseName.includes("row") ||
    exerciseName.includes("pull") ||
    exerciseName.includes("rdl") ||
    exerciseName.includes("romanian") ||
    exerciseName.includes("front squat") ||
    exerciseName.includes("pause");

  if (isBarbell && isCompound) {
    return {
      hevyExerciseTemplateId: '', // Will be set when exercise is selected from Hevy library
      category: ExerciseCategoryEnum.Auxiliary,
      progressionType: "Linear",
      assignedDay: 1, // Will be adjusted by user
      orderInDay: 2, // After main lift
    };
  }

  // Everything else defaults to accessory with RepsPerSet
  return {
    hevyExerciseTemplateId: '', // Will be set when exercise is selected from Hevy library
    category: ExerciseCategoryEnum.Accessory,
    progressionType: "RepsPerSet",
    assignedDay: 1,
    orderInDay: 3,
  };
}

/**
 * Hook for managing exercise selection state
 */
export function useExerciseSelection(programVariant: ProgramVariant = 4) {
  const [selectedExercises, setSelectedExercises] = useState<SelectedExercise[]>([]);

  /**
   * Add an exercise from the library with smart defaults
   */
  const addExercise = useCallback(
    (template: ExerciseTemplate) => {
      const defaults = getDefaultConfiguration(template, programVariant);

      // Calculate the next available order for the assigned day
      const exercisesInDay = selectedExercises.filter(
        (ex) => ex.assignedDay === defaults.assignedDay
      );
      const nextOrder = exercisesInDay.length > 0
        ? Math.max(...exercisesInDay.map((ex) => ex.orderInDay)) + 1
        : 1;

      const newExercise: SelectedExercise = {
        id: nanoid(),
        template,
        ...defaults,
        orderInDay: nextOrder, // Override with calculated order
      };

      setSelectedExercises((prev) => [...prev, newExercise]);
      return newExercise.id;
    },
    [programVariant, selectedExercises]
  );

  /**
   * Remove an exercise by ID
   */
  const removeExercise = useCallback((id: string) => {
    setSelectedExercises((prev) => prev.filter((ex) => ex.id !== id));
  }, []);

  /**
   * Update an exercise configuration
   * If assignedDay changes, automatically recalculates orderInDay to be sequential
   */
  const updateExercise = useCallback(
    (id: string, updates: Partial<Omit<SelectedExercise, "id" | "template">>) => {
      setSelectedExercises((prev) => {
        const exercise = prev.find((ex) => ex.id === id);
        if (!exercise) return prev;

        // Check if we're changing the assigned day
        const isChangingDay = updates.assignedDay !== undefined && updates.assignedDay !== exercise.assignedDay;

        if (isChangingDay) {
          // Get the new day
          const newDay = updates.assignedDay!;

          // Calculate the next available order for the new day
          const exercisesInNewDay = prev.filter(
            (ex) => ex.assignedDay === newDay && ex.id !== id
          );
          const nextOrder = exercisesInNewDay.length > 0
            ? Math.max(...exercisesInNewDay.map((ex) => ex.orderInDay)) + 1
            : 1;

          // Also need to reorder exercises in the old day to fill the gap
          const oldDay = exercise.assignedDay;
          const exercisesInOldDay = prev.filter(
            (ex) => ex.assignedDay === oldDay && ex.id !== id
          );

          // Reorder old day exercises to fill the gap
          const reorderedOldDay = exercisesInOldDay
            .sort((a, b) => a.orderInDay - b.orderInDay)
            .map((ex, index) => ({
              ...ex,
              orderInDay: index + 1,
            }));

          // Apply updates with the new orderInDay
          const updatedExercise = {
            ...exercise,
            ...updates,
            orderInDay: nextOrder,
          };

          // Combine all exercises
          const otherExercises = prev.filter(
            (ex) => ex.assignedDay !== oldDay && ex.assignedDay !== newDay
          );
          const newDayExercises = exercisesInNewDay.concat(updatedExercise);

          return [...otherExercises, ...reorderedOldDay, ...newDayExercises].sort((a, b) => {
            if (a.assignedDay !== b.assignedDay) {
              return a.assignedDay - b.assignedDay;
            }
            return a.orderInDay - b.orderInDay;
          });
        }

        // Simple update without day change
        return prev.map((ex) => (ex.id === id ? { ...ex, ...updates } : ex));
      });
    },
    []
  );

  /**
   * Reorder exercises (for drag-and-drop)
   */
  const reorderExercises = useCallback((startIndex: number, endIndex: number) => {
    setSelectedExercises((prev) => {
      const result = Array.from(prev);
      const [removed] = result.splice(startIndex, 1);
      result.splice(endIndex, 0, removed);
      return result;
    });
  }, []);

  /**
   * Reorder exercises within a specific day
   */
  const reorderExercisesInDay = useCallback(
    (day: DayNumber, startIndex: number, endIndex: number) => {
      setSelectedExercises((prev) => {
        const exercisesInDay = prev.filter((ex) => ex.assignedDay === day);
        const otherExercises = prev.filter((ex) => ex.assignedDay !== day);

        const result = Array.from(exercisesInDay);
        const [removed] = result.splice(startIndex, 1);
        result.splice(endIndex, 0, removed);

        // Update orderInDay for all exercises in this day
        const reordered = result.map((ex, index) => ({
          ...ex,
          orderInDay: index + 1,
        }));

        return [...otherExercises, ...reordered].sort((a, b) => {
          if (a.assignedDay !== b.assignedDay) {
            return a.assignedDay - b.assignedDay;
          }
          return a.orderInDay - b.orderInDay;
        });
      });
    },
    []
  );

  /**
   * Get exercises grouped by day
   */
  const exercisesByDay = useMemo(() => {
    const grouped: Record<DayNumber, SelectedExercise[]> = {
      1: [],
      2: [],
      3: [],
      4: [],
      5: [],
      6: [],
    };

    selectedExercises.forEach((exercise) => {
      grouped[exercise.assignedDay].push(exercise);
    });

    // Sort exercises within each day by orderInDay
    Object.keys(grouped).forEach((day) => {
      grouped[Number(day) as DayNumber].sort((a, b) => a.orderInDay - b.orderInDay);
    });

    return grouped;
  }, [selectedExercises]);

  /**
   * Reorder exercises across days (for column-based drag and drop)
   */
  const reorderExercisesAcrossDays = useCallback(
    (exerciseId: string, newDay: DayNumber, newOrder: number) => {
      setSelectedExercises((prev) => {
        const exercise = prev.find((ex) => ex.id === exerciseId);
        if (!exercise) return prev;

        const oldDay = exercise.assignedDay;
        const exercisesInNewDay = prev.filter((ex) => ex.assignedDay === newDay && ex.id !== exerciseId);
        const exercisesInOldDay = prev.filter((ex) => ex.assignedDay === oldDay && ex.id !== exerciseId);
        const otherExercises = prev.filter((ex) => ex.assignedDay !== newDay && ex.assignedDay !== oldDay);

        // Update the moved exercise
        const movedExercise = {
          ...exercise,
          assignedDay: newDay,
          orderInDay: newOrder,
        };

        // Reorder exercises in the new day
        const newDayExercises = [...exercisesInNewDay];
        newDayExercises.splice(newOrder - 1, 0, movedExercise);
        const reorderedNewDay = newDayExercises.map((ex, index) => ({
          ...ex,
          orderInDay: index + 1,
        }));

        // Reorder exercises in the old day
        const reorderedOldDay = exercisesInOldDay
          .sort((a, b) => a.orderInDay - b.orderInDay)
          .map((ex, index) => ({
            ...ex,
            orderInDay: index + 1,
          }));

        // Combine all exercises
        return [...otherExercises, ...reorderedNewDay, ...reorderedOldDay].sort((a, b) => {
          if (a.assignedDay !== b.assignedDay) {
            return a.assignedDay - b.assignedDay;
          }
          return a.orderInDay - b.orderInDay;
        });
      });
    },
    []
  );

  /**
   * Get the next available order number for a specific day
   */
  const getNextOrderInDay = useCallback(
    (day: DayNumber): number => {
      const exercisesInDay = exercisesByDay[day] || [];
      return exercisesInDay.length > 0
        ? Math.max(...exercisesInDay.map((ex) => ex.orderInDay)) + 1
        : 1;
    },
    [exercisesByDay]
  );

  /**
   * Clear all selected exercises
   */
  const clearExercises = useCallback(() => {
    setSelectedExercises([]);
  }, []);

  /**
   * Set exercises (useful for initialization/testing)
   */
  const setExercises = useCallback((exercises: SelectedExercise[]) => {
    setSelectedExercises(exercises);
  }, []);

  return {
    selectedExercises,
    exercisesByDay,
    addExercise,
    removeExercise,
    updateExercise,
    reorderExercises,
    reorderExercisesInDay,
    reorderExercisesAcrossDays,
    getNextOrderInDay,
    clearExercises,
    setExercises,
  };
}
