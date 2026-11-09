import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  convertToKg,
  isValidHevyTemplateId,
  resolveHevyTemplateId,
  clearHevyTemplateCache,
} from './hevySyncHelpers';
import { hevyApi } from './hevyApi';

vi.mock('./hevyApi', () => ({
  hevyApi: {
    getAllExerciseTemplates: vi.fn(),
    isConfigured: vi.fn().mockReturnValue(true),
  },
}));

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
});
