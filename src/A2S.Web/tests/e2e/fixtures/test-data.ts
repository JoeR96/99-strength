import type { WorkoutSummaryDto, WorkoutDto, ExerciseDto } from '../../src/types/workout';

export const TEST_USER = {
  id: 'user_test_123',
  firstName: 'Test',
  lastName: 'User',
  email: 'test@example.com',
  imageUrl: 'https://example.com/avatar.png',
};

export function createMockExercise(overrides: Partial<ExerciseDto> = {}): ExerciseDto {
  return {
    id: 'exercise-1',
    name: 'Bench Press',
    hevyExerciseTemplateId: 'bench-press',
    category: 'T1',
    assignedDay: 1,
    orderInDay: 1,
    isUnilateral: false,
    progression: {
      type: 'Linear',
      trainingMax: { value: 100, unit: 'Kilograms' },
      currentWeight: { value: 65, unit: 'Kilograms' },
      startingSets: 4,
      currentSets: 4,
      repRange: '12',
      startingWeight: { value: 100, unit: 'Kilograms' },
      isWeightConfirmed: true,
    },
    ...overrides,
  };
}

export function createMockWorkout(overrides: Partial<WorkoutDto> = {}): WorkoutDto {
  return {
    id: 'workout-1',
    name: 'My Program',
    variant: 'FourDay',
    status: 'Active',
    currentWeek: 1,
    currentDay: 1,
    totalWeeks: 21,
    daysPerWeek: 4,
    blockSequence: [1, 2, 3],
    completedDaysInCurrentWeek: [],
    exercises: [
      createMockExercise(),
      createMockExercise({
        id: 'exercise-2',
        name: 'Squat',
        hevyExerciseTemplateId: 'squat',
        assignedDay: 2,
        orderInDay: 1,
      }),
      createMockExercise({
        id: 'exercise-3',
        name: 'Overhead Press',
        hevyExerciseTemplateId: 'overhead-press',
        category: 'T2',
        assignedDay: 1,
        orderInDay: 2,
        progression: {
          type: 'RepsPerSet',
          currentWeight: { value: 40, unit: 'Kilograms' },
          startingSets: 3,
          currentSets: 3,
          repRange: '12-15',
          startingWeight: { value: 40, unit: 'Kilograms' },
          isWeightConfirmed: true,
        },
      }),
      createMockExercise({
        id: 'exercise-4',
        name: 'Deadlift',
        hevyExerciseTemplateId: 'deadlift',
        assignedDay: 3,
        orderInDay: 1,
      }),
    ],
    ...overrides,
  };
}

export function createMockWorkoutSummary(overrides: Partial<WorkoutSummaryDto> = {}): WorkoutSummaryDto {
  return {
    id: 'workout-1',
    name: 'My Program',
    variant: 'FourDay',
    status: 'Active',
    currentWeek: 1,
    totalWeeks: 21,
    exerciseCount: 4,
    ...overrides,
  };
}

export const MOCK_EXERCISE_LIBRARY = {
  items: [
    { name: 'Bench Press', equipment: 'Barbell', muscleGroup: 'Chest', description: '' },
    { name: 'Squat', equipment: 'Barbell', muscleGroup: 'Quads', description: '' },
    { name: 'Deadlift', equipment: 'Barbell', muscleGroup: 'Back', description: '' },
    { name: 'Overhead Press', equipment: 'Barbell', muscleGroup: 'Shoulders', description: '' },
    { name: 'Barbell Row', equipment: 'Barbell', muscleGroup: 'Back', description: '' },
    { name: 'Lat Pulldown', equipment: 'Cable', muscleGroup: 'Back', description: '' },
    { name: 'Dumbbell Curl', equipment: 'Dumbbell', muscleGroup: 'Biceps', description: '' },
    { name: 'Tricep Pushdown', equipment: 'Cable', muscleGroup: 'Triceps', description: '' },
  ],
  totalCount: 8,
  page: 1,
  pageSize: 50,
  templates: [],
};

export const MOCK_SIMULATION_RESULT = {
  workoutName: 'My Program',
  variant: 'FourDay',
  startWeek: 1,
  endWeek: 5,
  totalWeeks: 5,
  exercises: [
    {
      exerciseId: 'exercise-1',
      exerciseName: 'Bench Press',
      progressionType: 'Linear',
      dataPoints: [
        { session: 1, week: 1, block: 1, trainingMax: 100, trainingMaxUnit: 'kg', currentWeight: 65, currentWeightUnit: 'kg', summary: { type: 'Linear', details: {} } },
        { session: 2, week: 2, block: 1, trainingMax: 102.5, trainingMaxUnit: 'kg', currentWeight: 69.7, currentWeightUnit: 'kg', summary: { type: 'Linear', details: {} } },
        { session: 3, week: 3, block: 1, trainingMax: 105, trainingMaxUnit: 'kg', currentWeight: 73.5, currentWeightUnit: 'kg', summary: { type: 'Linear', details: {} } },
      ],
    },
  ],
};
