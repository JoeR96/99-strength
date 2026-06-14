/**
 * useExerciseSelection Hook Tests
 * Tests for exercise selection state management
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useExerciseSelection } from './useExerciseSelection';
import type { ExerciseTemplate, SelectedExercise, DayNumber } from '../types/workout';
import { ExerciseCategory } from '../types/workout';

// Helper to create mock exercise templates
function createTemplate(
  name: string,
  equipment: number = 0 // 0 = Barbell
): ExerciseTemplate {
  return {
    name,
    equipment,
    description: `${name} description`,
    defaultSets: 3,
    defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
  };
}

describe('useExerciseSelection', () => {
  describe('Initial State', () => {
    it('should start with empty exercise list', () => {
      const { result } = renderHook(() => useExerciseSelection());

      expect(result.current.selectedExercises).toEqual([]);
    });

    it('should have empty exercisesByDay for all days', () => {
      const { result } = renderHook(() => useExerciseSelection());

      expect(result.current.exercisesByDay[1]).toEqual([]);
      expect(result.current.exercisesByDay[2]).toEqual([]);
      expect(result.current.exercisesByDay[3]).toEqual([]);
      expect(result.current.exercisesByDay[4]).toEqual([]);
      expect(result.current.exercisesByDay[5]).toEqual([]);
      expect(result.current.exercisesByDay[6]).toEqual([]);
    });
  });

  describe('addExercise', () => {
    it('should add an exercise with a unique ID', () => {
      const { result } = renderHook(() => useExerciseSelection());

      act(() => {
        result.current.addExercise(createTemplate('Test Exercise'));
      });

      expect(result.current.selectedExercises).toHaveLength(1);
      expect(result.current.selectedExercises[0].id).toBeDefined();
      expect(result.current.selectedExercises[0].template.name).toBe('Test Exercise');
    });

    it('should generate unique IDs for multiple exercises', () => {
      const { result } = renderHook(() => useExerciseSelection());

      act(() => {
        result.current.addExercise(createTemplate('Exercise 1'));
        result.current.addExercise(createTemplate('Exercise 2'));
      });

      const [ex1, ex2] = result.current.selectedExercises;
      expect(ex1.id).not.toBe(ex2.id);
    });

    it('should return the new exercise ID', () => {
      const { result } = renderHook(() => useExerciseSelection());

      let newId: string;
      act(() => {
        newId = result.current.addExercise(createTemplate('Test Exercise'));
      });

      expect(newId!).toBe(result.current.selectedExercises[0].id);
    });

    describe('Smart Defaults for Main Lifts', () => {
      it('should assign Squat to Day 1 with Linear progression', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Barbell Squat'));
        });

        const exercise = result.current.selectedExercises[0];
        expect(exercise.assignedDay).toBe(1);
        expect(exercise.category).toBe(ExerciseCategory.MainLift);
        expect(exercise.progressionType).toBe('Linear');
        expect(exercise.orderInDay).toBe(1);
      });

      it('should assign Bench Press to Day 2 with Linear progression', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Bench Press'));
        });

        const exercise = result.current.selectedExercises[0];
        expect(exercise.assignedDay).toBe(2);
        expect(exercise.category).toBe(ExerciseCategory.MainLift);
        expect(exercise.progressionType).toBe('Linear');
      });

      it('should assign Deadlift to Day 3 with Linear progression', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Deadlift'));
        });

        const exercise = result.current.selectedExercises[0];
        expect(exercise.assignedDay).toBe(3);
        expect(exercise.category).toBe(ExerciseCategory.MainLift);
        expect(exercise.progressionType).toBe('Linear');
      });

      it('should assign Overhead Press to Day 4 in 4+ day programs', () => {
        const { result } = renderHook(() => useExerciseSelection(4));

        act(() => {
          result.current.addExercise(createTemplate('Overhead Press'));
        });

        const exercise = result.current.selectedExercises[0];
        expect(exercise.assignedDay).toBe(4);
      });

      it('should assign Press to Day 2 in 3-day programs', () => {
        const { result } = renderHook(() => useExerciseSelection(3 as 4 | 5 | 6));

        act(() => {
          result.current.addExercise(createTemplate('Overhead Press'));
        });

        const exercise = result.current.selectedExercises[0];
        expect(exercise.assignedDay).toBe(2);
      });
    });

    describe('Smart Defaults for Compound Exercises', () => {
      it('should identify barbell rows as Auxiliary', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Barbell Row', 0));
        });

        const exercise = result.current.selectedExercises[0];
        expect(exercise.category).toBe(ExerciseCategory.Auxiliary);
        expect(exercise.progressionType).toBe('Linear');
      });

      it('should identify RDL as MainLift (contains deadlift)', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Romanian Deadlift', 0));
        });

        const exercise = result.current.selectedExercises[0];
        // Romanian Deadlift contains "deadlift" so it's treated as main lift
        expect(exercise.category).toBe(ExerciseCategory.MainLift);
      });

      it('should identify front squat as MainLift (contains squat)', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Front Squat', 0));
        });

        const exercise = result.current.selectedExercises[0];
        // Front Squat contains "squat" so it's treated as main lift
        expect(exercise.category).toBe(ExerciseCategory.MainLift);
      });

      it('should identify pause bench as MainLift (contains bench)', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Pause Bench Press', 0));
        });

        const exercise = result.current.selectedExercises[0];
        // Pause Bench Press contains "bench" so it's treated as main lift
        expect(exercise.category).toBe(ExerciseCategory.MainLift);
      });
    });

    describe('Smart Defaults for Accessory Exercises', () => {
      it('should identify dumbbell exercises as Accessory with RepsPerSet', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Dumbbell Curl', 1)); // 1 = Dumbbell
        });

        const exercise = result.current.selectedExercises[0];
        expect(exercise.category).toBe(ExerciseCategory.Accessory);
        expect(exercise.progressionType).toBe('RepsPerSet');
      });

      it('should identify cable exercises as Accessory', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Cable Fly', 2)); // 2 = Cable
        });

        const exercise = result.current.selectedExercises[0];
        expect(exercise.category).toBe(ExerciseCategory.Accessory);
        expect(exercise.progressionType).toBe('RepsPerSet');
      });
    });

    describe('Order in Day Calculation', () => {
      it('should assign order 1 to first exercise in a day', () => {
        const { result } = renderHook(() => useExerciseSelection());

        act(() => {
          result.current.addExercise(createTemplate('Barbell Squat'));
        });

        expect(result.current.selectedExercises[0].orderInDay).toBe(1);
      });

      it('should increment order for subsequent exercises in same day', () => {
        const { result } = renderHook(() => useExerciseSelection());

        // Add main lift (Day 1, order 1)
        act(() => {
          result.current.addExercise(createTemplate('Barbell Squat'));
        });

        // Add accessory in separate act
        let legPressId: string;
        act(() => {
          legPressId = result.current.addExercise(createTemplate('Leg Press', 3)); // Machine
        });

        // Move to Day 1 in separate act
        act(() => {
          result.current.updateExercise(legPressId, { assignedDay: 1 });
        });

        const day1Exercises = result.current.exercisesByDay[1];
        expect(day1Exercises).toHaveLength(2);
        expect(day1Exercises[0].orderInDay).toBe(1);
        expect(day1Exercises[1].orderInDay).toBe(2);
      });
    });
  });

  describe('removeExercise', () => {
    it('should remove an exercise by ID', () => {
      const { result } = renderHook(() => useExerciseSelection());

      let exerciseId: string;
      act(() => {
        exerciseId = result.current.addExercise(createTemplate('Exercise 1'));
        result.current.addExercise(createTemplate('Exercise 2'));
      });

      expect(result.current.selectedExercises).toHaveLength(2);

      act(() => {
        result.current.removeExercise(exerciseId);
      });

      expect(result.current.selectedExercises).toHaveLength(1);
      expect(result.current.selectedExercises[0].template.name).toBe('Exercise 2');
    });

    it('should not remove anything if ID does not exist', () => {
      const { result } = renderHook(() => useExerciseSelection());

      act(() => {
        result.current.addExercise(createTemplate('Exercise 1'));
      });

      act(() => {
        result.current.removeExercise('non-existent-id');
      });

      expect(result.current.selectedExercises).toHaveLength(1);
    });

    it('should re-sequence orderInDay so no gap is left after removing a middle exercise', () => {
      const { result } = renderHook(() => useExerciseSelection());

      // Day 2 with five contiguous exercises (orders 1..5)
      const day2: SelectedExercise[] = [1, 2, 3, 4, 5].map((order) => ({
        id: `ex-${order}`,
        template: createTemplate(`Exercise ${order}`),
        hevyExerciseTemplateId: '',
        category: ExerciseCategory.Accessory,
        progressionType: 'RepsPerSet',
        assignedDay: 2 as DayNumber,
        orderInDay: order,
      }));

      act(() => {
        result.current.setExercises(day2);
      });

      // Remove the 3rd exercise (order 3) — previously left a gap at position 3
      act(() => {
        result.current.removeExercise('ex-3');
      });

      const remaining = result.current.exercisesByDay[2];
      expect(remaining).toHaveLength(4);
      // Orders must be contiguous 1..4 with no gap (backend requires this)
      expect(remaining.map((e) => e.orderInDay)).toEqual([1, 2, 3, 4]);
      // The removed exercise is gone; relative order of the rest is preserved
      expect(remaining.map((e) => e.template.name)).toEqual([
        'Exercise 1',
        'Exercise 2',
        'Exercise 4',
        'Exercise 5',
      ]);
    });

    it('should only re-sequence the affected day, leaving other days untouched', () => {
      const { result } = renderHook(() => useExerciseSelection());

      const exercises: SelectedExercise[] = [
        { id: 'd1-1', template: createTemplate('D1 A'), hevyExerciseTemplateId: '', category: ExerciseCategory.MainLift, progressionType: 'Linear', assignedDay: 1 as DayNumber, orderInDay: 1 },
        { id: 'd1-2', template: createTemplate('D1 B'), hevyExerciseTemplateId: '', category: ExerciseCategory.Accessory, progressionType: 'RepsPerSet', assignedDay: 1 as DayNumber, orderInDay: 2 },
        { id: 'd2-1', template: createTemplate('D2 A'), hevyExerciseTemplateId: '', category: ExerciseCategory.Accessory, progressionType: 'RepsPerSet', assignedDay: 2 as DayNumber, orderInDay: 1 },
        { id: 'd2-2', template: createTemplate('D2 B'), hevyExerciseTemplateId: '', category: ExerciseCategory.Accessory, progressionType: 'RepsPerSet', assignedDay: 2 as DayNumber, orderInDay: 2 },
      ];

      act(() => {
        result.current.setExercises(exercises);
      });

      act(() => {
        result.current.removeExercise('d1-1'); // remove first of Day 1
      });

      // Day 1 re-sequenced to start at 1
      expect(result.current.exercisesByDay[1].map((e) => e.orderInDay)).toEqual([1]);
      // Day 2 untouched
      expect(result.current.exercisesByDay[2].map((e) => e.orderInDay)).toEqual([1, 2]);
    });
  });

  describe('updateExercise', () => {
    it('should update exercise properties', () => {
      const { result } = renderHook(() => useExerciseSelection());

      let exerciseId: string;
      act(() => {
        exerciseId = result.current.addExercise(createTemplate('Test Exercise'));
      });

      act(() => {
        result.current.updateExercise(exerciseId, {
          progressionType: 'RepsPerSet',
          category: ExerciseCategory.Accessory,
        });
      });

      const exercise = result.current.selectedExercises[0];
      expect(exercise.progressionType).toBe('RepsPerSet');
      expect(exercise.category).toBe(ExerciseCategory.Accessory);
    });

    it('should recalculate order when changing days', () => {
      const { result } = renderHook(() => useExerciseSelection());

      // Add exercises to Day 1
      let exerciseId: string;
      act(() => {
        result.current.addExercise(createTemplate('Barbell Squat')); // Day 1, order 1
        exerciseId = result.current.addExercise(createTemplate('Leg Press', 3));
      });

      // Move second exercise to Day 1
      act(() => {
        result.current.updateExercise(exerciseId, { assignedDay: 1 });
      });

      // Now move it to Day 2
      act(() => {
        result.current.updateExercise(exerciseId, { assignedDay: 2 as DayNumber });
      });

      // Should be order 1 in Day 2 (only exercise there)
      const exercise = result.current.selectedExercises.find((e) => e.id === exerciseId);
      expect(exercise?.assignedDay).toBe(2);
      expect(exercise?.orderInDay).toBe(1);
    });

    it('should not update non-existent exercise', () => {
      const { result } = renderHook(() => useExerciseSelection());

      act(() => {
        result.current.addExercise(createTemplate('Test Exercise'));
      });

      const originalExercise = { ...result.current.selectedExercises[0] };

      act(() => {
        result.current.updateExercise('non-existent-id', { progressionType: 'RepsPerSet' });
      });

      expect(result.current.selectedExercises[0]).toEqual(originalExercise);
    });
  });

  describe('reorderExercisesInDay', () => {
    it('should reorder exercises within a day', () => {
      const { result } = renderHook(() => useExerciseSelection());

      // Add three exercises to Day 1 in separate acts
      let id2: string, id3: string;
      act(() => {
        result.current.addExercise(createTemplate('Barbell Squat'));
      });
      act(() => {
        id2 = result.current.addExercise(createTemplate('Leg Press', 3));
      });
      act(() => {
        id3 = result.current.addExercise(createTemplate('Leg Curl', 3));
      });
      // Move all to Day 1 in separate acts
      act(() => {
        result.current.updateExercise(id2!, { assignedDay: 1 });
      });
      act(() => {
        result.current.updateExercise(id3!, { assignedDay: 1 });
      });

      const originalOrder = result.current.exercisesByDay[1].map((e) => e.template.name);

      // Move first exercise to last position
      act(() => {
        result.current.reorderExercisesInDay(1, 0, 2);
      });

      const newOrder = result.current.exercisesByDay[1].map((e) => e.template.name);
      expect(newOrder[0]).toBe(originalOrder[1]);
      expect(newOrder[1]).toBe(originalOrder[2]);
      expect(newOrder[2]).toBe(originalOrder[0]);

      // Verify orderInDay is updated
      result.current.exercisesByDay[1].forEach((exercise, index) => {
        expect(exercise.orderInDay).toBe(index + 1);
      });
    });
  });

  describe('reorderExercisesAcrossDays', () => {
    it('should move exercise between days', () => {
      const { result } = renderHook(() => useExerciseSelection());

      // Add exercise to Day 1
      let exerciseId: string;
      act(() => {
        exerciseId = result.current.addExercise(createTemplate('Barbell Squat'));
      });

      expect(result.current.exercisesByDay[1]).toHaveLength(1);
      expect(result.current.exercisesByDay[2]).toHaveLength(0);

      // Move to Day 2
      act(() => {
        result.current.reorderExercisesAcrossDays(exerciseId, 2, 1);
      });

      expect(result.current.exercisesByDay[1]).toHaveLength(0);
      expect(result.current.exercisesByDay[2]).toHaveLength(1);
      expect(result.current.exercisesByDay[2][0].orderInDay).toBe(1);
    });

    it('should update order in both source and destination days', () => {
      const { result } = renderHook(() => useExerciseSelection());

      // Add exercises to Day 1 and Day 2
      let moveId: string;
      act(() => {
        result.current.addExercise(createTemplate('Barbell Squat')); // Day 1
        moveId = result.current.addExercise(createTemplate('Leg Press', 3));
        result.current.updateExercise(moveId, { assignedDay: 1 }); // Day 1
        result.current.addExercise(createTemplate('Bench Press')); // Day 2
      });

      // Move Leg Press from Day 1 to Day 2 at position 1
      act(() => {
        result.current.reorderExercisesAcrossDays(moveId, 2, 1);
      });

      // Day 1 should have Squat at order 1
      expect(result.current.exercisesByDay[1]).toHaveLength(1);
      expect(result.current.exercisesByDay[1][0].orderInDay).toBe(1);

      // Day 2 should have Leg Press at order 1, Bench at order 2
      expect(result.current.exercisesByDay[2]).toHaveLength(2);
      expect(result.current.exercisesByDay[2][0].template.name).toBe('Leg Press');
      expect(result.current.exercisesByDay[2][0].orderInDay).toBe(1);
      expect(result.current.exercisesByDay[2][1].orderInDay).toBe(2);
    });
  });

  describe('getNextOrderInDay', () => {
    it('should return 1 for empty day', () => {
      const { result } = renderHook(() => useExerciseSelection());

      expect(result.current.getNextOrderInDay(3)).toBe(1);
    });

    it('should return next available order', () => {
      const { result } = renderHook(() => useExerciseSelection());

      act(() => {
        result.current.addExercise(createTemplate('Barbell Squat')); // Day 1
      });

      expect(result.current.getNextOrderInDay(1)).toBe(2);
    });
  });

  describe('clearExercises', () => {
    it('should remove all exercises', () => {
      const { result } = renderHook(() => useExerciseSelection());

      act(() => {
        result.current.addExercise(createTemplate('Exercise 1'));
        result.current.addExercise(createTemplate('Exercise 2'));
        result.current.addExercise(createTemplate('Exercise 3'));
      });

      expect(result.current.selectedExercises).toHaveLength(3);

      act(() => {
        result.current.clearExercises();
      });

      expect(result.current.selectedExercises).toHaveLength(0);
    });
  });

  describe('setExercises', () => {
    it('should replace all exercises', () => {
      const { result } = renderHook(() => useExerciseSelection());

      const newExercises: SelectedExercise[] = [
        {
          id: 'preset-1',
          template: createTemplate('Preset Exercise'),
          hevyExerciseTemplateId: 'ABC12345',
          category: ExerciseCategory.MainLift,
          progressionType: 'Linear',
          assignedDay: 1,
          orderInDay: 1,
        },
      ];

      act(() => {
        result.current.setExercises(newExercises);
      });

      expect(result.current.selectedExercises).toEqual(newExercises);
    });
  });

  describe('exercisesByDay Memoization', () => {
    it('should group exercises by day', () => {
      const { result } = renderHook(() => useExerciseSelection());

      act(() => {
        result.current.addExercise(createTemplate('Barbell Squat')); // Day 1
        result.current.addExercise(createTemplate('Bench Press')); // Day 2
        result.current.addExercise(createTemplate('Deadlift')); // Day 3
      });

      expect(result.current.exercisesByDay[1]).toHaveLength(1);
      expect(result.current.exercisesByDay[1][0].template.name).toBe('Barbell Squat');

      expect(result.current.exercisesByDay[2]).toHaveLength(1);
      expect(result.current.exercisesByDay[2][0].template.name).toBe('Bench Press');

      expect(result.current.exercisesByDay[3]).toHaveLength(1);
      expect(result.current.exercisesByDay[3][0].template.name).toBe('Deadlift');
    });

    it('should sort exercises within each day by orderInDay', () => {
      const { result } = renderHook(() => useExerciseSelection());

      // Add squat to Day 1
      act(() => {
        result.current.addExercise(createTemplate('Barbell Squat')); // Day 1, order 1
      });

      // Add leg press and move to Day 1
      act(() => {
        const id = result.current.addExercise(createTemplate('Leg Press', 3));
        result.current.updateExercise(id, { assignedDay: 1 }); // Day 1, order 2
      });

      const day1 = result.current.exercisesByDay[1];
      expect(day1).toHaveLength(2);
      // First exercise should have order 1, second should have order 2
      expect(day1[0].orderInDay).toBe(1);
      expect(day1[1].orderInDay).toBe(2);
    });
  });
});
