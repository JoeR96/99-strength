import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  convertToKg,
  roundWeight,
  roundToGymIncrement,
  isValidHevyTemplateId,
} from './hevySyncHelpers';

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
});
