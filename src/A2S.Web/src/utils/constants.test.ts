import { describe, it, expect } from 'vitest';
import { KG_PER_LB, lbsToKg, kgToLbs } from './constants';

describe('constants', () => {
  it('KG_PER_LB is approximately 0.4536', () => {
    expect(KG_PER_LB).toBeCloseTo(0.453592, 5);
  });

  describe('lbsToKg', () => {
    it('converts 100 lbs to kg', () => {
      expect(lbsToKg(100)).toBeCloseTo(45.3592, 3);
    });

    it('converts 0 lbs to 0 kg', () => {
      expect(lbsToKg(0)).toBe(0);
    });

    it('converts 225 lbs correctly', () => {
      expect(lbsToKg(225)).toBeCloseTo(102.058, 2);
    });
  });

  describe('kgToLbs', () => {
    it('converts 100 kg to lbs', () => {
      expect(kgToLbs(100)).toBeCloseTo(220.462, 2);
    });

    it('converts 0 kg to 0 lbs', () => {
      expect(kgToLbs(0)).toBe(0);
    });

    it('round-trips: kgToLbs(lbsToKg(x)) ≈ x', () => {
      const original = 135;
      expect(kgToLbs(lbsToKg(original))).toBeCloseTo(original, 5);
    });
  });
});
