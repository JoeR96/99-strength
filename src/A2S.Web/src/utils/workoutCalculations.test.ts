import { describe, it, expect } from 'vitest';
import { calculatePrescribedWeight, getPrescribedSetsAndReps } from './workoutCalculations';
import type {
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
} from '@/types/workout';

function makeLinearExercise(overrides?: Partial<LinearProgressionDto>): ExerciseDto {
  return {
    id: 'ex-1',
    name: 'Squat',
    category: 1,
    equipment: 0,
    assignedDay: 1,
    orderInDay: 1,
    hevyExerciseTemplateId: 'ABCD1234',
    progression: {
      type: 'Linear',
      trainingMax: { value: 100, unit: 1 }, // 100kg
      useAmrap: true,
      baseSetsPerExercise: 5,
      ...overrides,
    } as LinearProgressionDto,
  };
}

function makeRpsExercise(overrides?: Partial<RepsPerSetProgressionDto>): ExerciseDto {
  return {
    id: 'ex-2',
    name: 'Lat Pulldown',
    category: 3,
    equipment: 4,
    assignedDay: 2,
    orderInDay: 1,
    hevyExerciseTemplateId: 'ABCD5678',
    progression: {
      type: 'RepsPerSet',
      currentWeight: 50,
      currentSetCount: 3,
      repRange: { minimum: 8, maximum: 12 },
      isUnilateral: false,
      ...overrides,
    } as RepsPerSetProgressionDto,
  };
}

function makeMinimalExercise(overrides?: Partial<MinimalSetsProgressionDto>): ExerciseDto {
  return {
    id: 'ex-3',
    name: 'Pushups',
    category: 3,
    equipment: 5,
    assignedDay: 3,
    orderInDay: 1,
    hevyExerciseTemplateId: 'ABCD9999',
    progression: {
      type: 'MinimalSets',
      currentWeight: 0,
      currentSetCount: 4,
      targetTotalReps: 40,
      minimumSets: 3,
      maximumSets: 6,
      ...overrides,
    } as MinimalSetsProgressionDto,
  };
}

describe('calculatePrescribedWeight', () => {
  it('calculates Linear weight: TM × intensity, rounded to 2.5kg', () => {
    const exercise = makeLinearExercise();
    // Week 1: 65% of 100kg = 65kg
    const weight = calculatePrescribedWeight(exercise, 1);
    expect(weight).toBe(65);
  });

  it('calculates Linear weight for week 20 (79%)', () => {
    const exercise = makeLinearExercise();
    // Week 20: 79% of 100kg = 79, rounds to 80
    const weight = calculatePrescribedWeight(exercise, 20);
    expect(weight).toBe(80);
  });

  it('converts lbs TM to kg before calculation', () => {
    const exercise = makeLinearExercise({
      trainingMax: { value: 225, unit: 2 }, // 225 lbs ≈ 102kg
    });
    const weight = calculatePrescribedWeight(exercise, 1);
    // 225 lbs * 0.453592 * 0.65 = ~66.4kg → rounds to 67.5
    expect(weight).toBe(67.5);
  });

  it('returns RPS current weight in kg', () => {
    const exercise = makeRpsExercise({ currentWeight: 50 });
    expect(calculatePrescribedWeight(exercise, 1)).toBe(50);
  });

  it('converts lbs RPS weight to kg', () => {
    const exercise = makeRpsExercise({
      currentWeight: 100,
      weightUnit: 'pounds',
    });
    const weight = calculatePrescribedWeight(exercise, 1);
    expect(weight).toBeCloseTo(45.36, 1);
  });

  it('returns MinimalSets current weight', () => {
    const exercise = makeMinimalExercise({ currentWeight: 0 });
    expect(calculatePrescribedWeight(exercise, 1)).toBe(0);
  });

  it('returns 0 for unknown progression type', () => {
    const exercise = makeLinearExercise();
    exercise.progression = { type: 'Unknown' } as any;
    expect(calculatePrescribedWeight(exercise, 1)).toBe(0);
  });
});

describe('getPrescribedSetsAndReps', () => {
  it('returns correct sets/reps for Linear week 1', () => {
    const exercise = makeLinearExercise();
    const result = getPrescribedSetsAndReps(exercise, 1);
    expect(result.sets).toBe(4);
    expect(result.reps).toBe(12);
  });

  it('returns correct sets/reps for deload week', () => {
    const exercise = makeLinearExercise();
    const result = getPrescribedSetsAndReps(exercise, 7);
    expect(result.sets).toBe(4);
    expect(result.reps).toBe(5);
  });

  it('returns RPS max reps and current set count', () => {
    const exercise = makeRpsExercise({
      currentSetCount: 3,
      repRange: { minimum: 8, maximum: 12 },
    });
    const result = getPrescribedSetsAndReps(exercise, 1);
    expect(result.sets).toBe(3);
    expect(result.reps).toBe(12);
  });

  it('returns MinimalSets distributed reps', () => {
    const exercise = makeMinimalExercise({
      targetTotalReps: 40,
      currentSetCount: 4,
    });
    const result = getPrescribedSetsAndReps(exercise, 1);
    expect(result.sets).toBe(4);
    expect(result.reps).toBe(10); // 40/4 = 10
  });

  it('returns default 3×10 for unknown progression type', () => {
    const exercise = makeLinearExercise();
    exercise.progression = { type: 'Unknown' } as any;
    const result = getPrescribedSetsAndReps(exercise, 1);
    expect(result.sets).toBe(3);
    expect(result.reps).toBe(10);
  });
});
