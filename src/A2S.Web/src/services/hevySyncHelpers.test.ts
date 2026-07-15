import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  convertToKg,
  roundWeight,
  roundToGymIncrement,
  isValidHevyTemplateId,
  resolveHevyTemplateId,
  clearHevyTemplateCache,
  formatLastPerformanceNote,
  convertExerciseToHevyRoutine,
} from './hevySyncHelpers';
import { hevyApi } from './hevyApi';
import type { ExerciseDto, RepsPerSetProgressionDto } from '@/types/workout';

vi.mock('./hevyApi', () => ({
  hevyApi: {
    getAllExerciseTemplates: vi.fn(),
    isConfigured: vi.fn().mockReturnValue(true),
  },
}));

function makeRepsPerSetExercise(overrides?: {
  progression?: Partial<RepsPerSetProgressionDto>;
  lastPerformance?: ExerciseDto['lastPerformance'];
}): ExerciseDto {
  return {
    id: 'ex-1',
    name: 'Lateral Raise (Cable)',
    category: 2,
    equipment: 5,
    assignedDay: 1,
    orderInDay: 1,
    hevyExerciseTemplateId: 'ABCD1234',
    lastPerformance: overrides?.lastPerformance ?? null,
    progression: {
      type: 'RepsPerSet',
      repRange: { minimum: 8, maximum: 12 },
      startingSets: 2,
      currentSetCount: 2,
      targetSets: 3,
      currentWeight: 12.5,
      weightUnit: 'Kilograms',
      isUnilateral: false,
      isWeightPending: false,
      pendingWeightConfirmation: false,
      suggestedWeight: null,
      ...overrides?.progression,
    } as RepsPerSetProgressionDto,
  } as unknown as ExerciseDto;
}

describe('hevySyncHelpers', () => {
  describe('convertToKg', () => {
    it('returns same weight for kg unit', () => {
      expect(convertToKg(100, 'kg')).toBe(100);
    });

    it('converts lbs to kg', () => {
      expect(convertToKg(225, 'lbs')).toBeCloseTo(102.06, 1);
    });

    it('handles "pounds" unit string', () => {
      expect(convertToKg(225, 'pounds')).toBeCloseTo(102.06, 1);
    });

    it('rounds to 2 decimal places', () => {
      const result = convertToKg(100.123456, 'kg');
      expect(result).toBe(100.12);
    });
  });

  describe('roundWeight', () => {
    it('rounds to 2 decimal places', () => {
      expect(roundWeight(65.1234)).toBe(65.12);
      expect(roundWeight(100)).toBe(100);
      expect(roundWeight(0.005)).toBe(0.01);
    });
  });

  describe('roundToGymIncrement', () => {
    it('rounds to nearest 2.5 kg', () => {
      expect(roundToGymIncrement(61, 'kg')).toBe(60);
      expect(roundToGymIncrement(63.8, 'kg')).toBe(65);
    });

    it('rounds to nearest 5 lbs', () => {
      expect(roundToGymIncrement(133, 'lbs')).toBe(135);
      expect(roundToGymIncrement(131, 'lbs')).toBe(130);
    });

    it('defaults to kg', () => {
      expect(roundToGymIncrement(61)).toBe(60);
    });
  });

  describe('isValidHevyTemplateId', () => {
    it('accepts 8-char hex string', () => {
      expect(isValidHevyTemplateId('ABCD1234')).toBe(true);
      expect(isValidHevyTemplateId('abcdef01')).toBe(true);
    });

    it('rejects non-hex characters', () => {
      expect(isValidHevyTemplateId('GHIJ1234')).toBe(false);
    });

    it('rejects wrong-length strings', () => {
      expect(isValidHevyTemplateId('ABCD123')).toBe(false);
      expect(isValidHevyTemplateId('ABCD12345')).toBe(false);
    });

    it('rejects empty string', () => {
      expect(isValidHevyTemplateId('')).toBe(false);
    });
  });

  describe('resolveHevyTemplateId', () => {
    beforeEach(() => {
      clearHevyTemplateCache();
      vi.mocked(hevyApi.getAllExerciseTemplates).mockReset();
    });

    it('returns a valid stored template ID without lookup', async () => {
      const id = await resolveHevyTemplateId('Anything', 'ABCD1234');
      expect(id).toBe('ABCD1234');
      expect(hevyApi.getAllExerciseTemplates).not.toHaveBeenCalled();
    });

    it('prefers an exact title match over substring matches', async () => {
      vi.mocked(hevyApi.getAllExerciseTemplates).mockResolvedValue([
        { id: 'PAUSE001', title: 'Pause Squat (Barbell)' },
        { id: 'SQUAT001', title: 'Squat (Barbell)' },
      ] as never);

      const id = await resolveHevyTemplateId('Squat (Barbell)', 'not-a-hevy-id');
      expect(id).toBe('SQUAT001');
    });

    it('does not bind to a longer template when the exact title is missing and another candidate is closer', async () => {
      vi.mocked(hevyApi.getAllExerciseTemplates).mockResolvedValue([
        { id: 'SINGLE01', title: 'Single Arm Lat Pulldown (Cable)' },
        { id: 'LATPD001', title: 'Lat Pulldown (Cable) Wide Grip' },
      ] as never);

      const id = await resolveHevyTemplateId('Lat Pulldown (Cable)', 'not-a-hevy-id');
      expect(id).toBe('LATPD001');
    });

    it('fails loudly (empty string) when substring matches are ambiguous', async () => {
      vi.mocked(hevyApi.getAllExerciseTemplates).mockResolvedValue([
        { id: 'AAAA0001', title: 'Curl (Cable) One' },
        { id: 'BBBB0001', title: 'Curl (Cable) Two' },
      ] as never);

      const id = await resolveHevyTemplateId('Curl (Cable)', 'not-a-hevy-id');
      expect(id).toBe('');
    });

    it('uses a single unambiguous substring match', async () => {
      vi.mocked(hevyApi.getAllExerciseTemplates).mockResolvedValue([
        { id: 'CCCC0001', title: 'Seated Cable Row - V Grip (Cable)' },
        { id: 'DDDD0001', title: 'Bench Press (Barbell)' },
      ] as never);

      const id = await resolveHevyTemplateId('Seated Cable Row - V Grip', 'not-a-hevy-id');
      expect(id).toBe('CCCC0001');
    });
  });

  describe('formatLastPerformanceNote', () => {
    it('returns null when there is no last performance', () => {
      expect(formatLastPerformanceNote(makeRepsPerSetExercise())).toBeNull();
    });

    it('formats uniform-weight sets compactly', () => {
      const exercise = makeRepsPerSetExercise({
        lastPerformance: {
          weekNumber: 2,
          completedAt: '2026-07-08T10:00:00Z',
          sets: [
            { setNumber: 1, weight: 12.5, weightUnit: 'Kilograms', reps: 12, wasAmrap: false },
            { setNumber: 2, weight: 12.5, weightUnit: 'Kilograms', reps: 11, wasAmrap: false },
          ],
        },
      });

      expect(formatLastPerformanceNote(exercise)).toBe('Last (W2): 12.5kg × 12/11');
    });

    it('formats mixed-weight sets per set', () => {
      const exercise = makeRepsPerSetExercise({
        lastPerformance: {
          weekNumber: 3,
          completedAt: '2026-07-08T10:00:00Z',
          sets: [
            { setNumber: 1, weight: 20, weightUnit: 'Kilograms', reps: 10, wasAmrap: false },
            { setNumber: 2, weight: 17.5, weightUnit: 'Kilograms', reps: 12, wasAmrap: false },
          ],
        },
      });

      expect(formatLastPerformanceNote(exercise)).toBe('Last (W3): 10×20kg, 12×17.5kg');
    });
  });

  describe('convertExerciseToHevyRoutine notes', () => {
    it('includes rep range, set progress, and progression guidance for RepsPerSet', () => {
      const exercise = makeRepsPerSetExercise();
      const result = convertExerciseToHevyRoutine(exercise, 2, 'ABCD1234');

      expect(result.notes).toContain('Rep range: 8-12');
      expect(result.notes).toContain('Sets: 2/3');
      expect(result.notes).toContain('Hit 12s on every set to add a set');
    });

    it('prepends the NEW WEIGHT warning when a weight bump awaits confirmation', () => {
      const exercise = makeRepsPerSetExercise({
        progression: { pendingWeightConfirmation: true, suggestedWeight: 13.5, currentWeight: 13.5 },
      });
      const result = convertExerciseToHevyRoutine(exercise, 2, 'ABCD1234');

      expect(result.notes).toMatch(/^NEW WEIGHT: try 13.5kg/);
      expect(result.notes).toContain('log what you actually lift');
    });

    it('includes last performance when available', () => {
      const exercise = makeRepsPerSetExercise({
        lastPerformance: {
          weekNumber: 1,
          completedAt: '2026-07-01T10:00:00Z',
          sets: [
            { setNumber: 1, weight: 12.5, weightUnit: 'Kilograms', reps: 12, wasAmrap: false },
            { setNumber: 2, weight: 12.5, weightUnit: 'Kilograms', reps: 12, wasAmrap: false },
          ],
        },
      });
      const result = convertExerciseToHevyRoutine(exercise, 2, 'ABCD1234');

      expect(result.notes).toContain('Last (W1): 12.5kg × 12/12');
    });

    it('advises moving up in weight when already at max sets', () => {
      const exercise = makeRepsPerSetExercise({
        progression: { currentSetCount: 3, targetSets: 3 },
      });
      const result = convertExerciseToHevyRoutine(exercise, 2, 'ABCD1234');

      expect(result.notes).toContain('Sets: 3/3');
      expect(result.notes).toContain('Hit 12s on every set to move up in weight');
    });
  });
});
